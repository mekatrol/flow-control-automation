#include "platform_ethernet.h"

#include <stdio.h>

#include "driver/gpio.h"
#include "driver/spi_master.h"
#include "esp_eth.h"
#include "esp_eth_mac_w5500.h"
#include "esp_eth_netif_glue.h"
#include "esp_eth_phy_w5500.h"
#include "esp_event.h"
#include "esp_intr_alloc.h"
#include "esp_mac.h"
#include "esp_netif.h"
#include "esp_netif_ip_addr.h"
#include "freertos/FreeRTOS.h"
#include "freertos/queue.h"

/* Fixed resource limits bound callback memory use and W5500 transaction latency. */
enum {
    ETHERNET_EVENT_QUEUE_DEPTH = 16,
    ETHERNET_SPI_QUEUE_DEPTH = 16,
    ETHERNET_MAC_RX_TASK_STACK_SIZE = 4096,
    W5500_PHY_ADDRESS = 1,
};

/* SPI2 is the board's dedicated W5500 controller and is not shared at runtime. */
static const spi_host_device_t ETHERNET_SPI_HOST = SPI2_HOST;

static QueueHandle_t ethernet_event_queue;
static esp_netif_t *ethernet_network_interface;
static esp_eth_handle_t ethernet_driver;
static esp_eth_netif_glue_handle_t ethernet_netif_glue;
static bool is_driver_started;

/* Ensures the shared GPIO ISR dispatcher exists before W5500 registers IRQ2. */
static bool is_gpio_interrupt_service_ready(void)
{
    const esp_err_t result = gpio_install_isr_service(ESP_INTR_FLAG_LOWMED);
    /* Another feature may own the shared service, which is already usable. */
    return result == ESP_OK || result == ESP_ERR_INVALID_STATE;
}

/* Copies one event into the bounded queue without blocking ESP-IDF callbacks. */
static void enqueue_platform_event(const ethernet_platform_event_t *event)
{
    if (ethernet_event_queue != NULL)
        (void)xQueueSend(ethernet_event_queue, event, 0);
}

/* Tests whether DHCP installed a usable primary IPv4 DNS server. */
static bool is_dns_ready(void)
{
    esp_netif_dns_info_t dns = {0};
    if (esp_netif_get_dns_info(ethernet_network_interface,
                               ESP_NETIF_DNS_MAIN, &dns) != ESP_OK)
        return false;
    return dns.ip.type == ESP_IPADDR_TYPE_V4 && dns.ip.u_addr.ip4.addr != 0;
}

/* Converts ESP-IDF Ethernet callbacks into owned platform events. */
static void handle_ethernet_event(void *context, esp_event_base_t event_base,
                                  int32_t event_id, void *event_data)
{
    (void)context;
    (void)event_data;
    if (event_base != ETH_EVENT) return;
    ethernet_platform_event_t event = {0};
    switch (event_id) {
    case ETHERNET_EVENT_START:
        is_driver_started = true;
        event.type = ETHERNET_PLATFORM_EVENT_DRIVER_STARTED;
        break;
    case ETHERNET_EVENT_CONNECTED:
        event.type = ETHERNET_PLATFORM_EVENT_LINK_UP;
        break;
    case ETHERNET_EVENT_DISCONNECTED:
        event.type = ETHERNET_PLATFORM_EVENT_LINK_DOWN;
        break;
    case ETHERNET_EVENT_STOP:
        is_driver_started = false;
        event.type = ETHERNET_PLATFORM_EVENT_STOPPED;
        break;
    default:
        return;
    }
    enqueue_platform_event(&event);
}

/* Converts interface-specific address callbacks into owned platform events. */
static void handle_ip_event(void *context, esp_event_base_t event_base,
                            int32_t event_id, void *event_data)
{
    (void)context;
    if (event_base != IP_EVENT) return;
    ethernet_platform_event_t event = {0};
    if (event_id == IP_EVENT_ETH_GOT_IP) {
        const ip_event_got_ip_t *got_ip = event_data;
        if (got_ip->esp_netif != ethernet_network_interface) return;
        event.type = ETHERNET_PLATFORM_EVENT_ADDRESS_READY;
        event.dns_ready = is_dns_ready();
        (void)snprintf(event.ipv4_address, sizeof(event.ipv4_address), IPSTR,
                       IP2STR(&got_ip->ip_info.ip));
    } else if (event_id == IP_EVENT_GOT_IP6) {
        const ip_event_got_ip6_t *got_ip6 = event_data;
        if (got_ip6->esp_netif != ethernet_network_interface) return;
        event.type = ETHERNET_PLATFORM_EVENT_ADDRESS_READY;
        event.dns_ready = is_dns_ready();
        (void)snprintf(event.ipv6_address, sizeof(event.ipv6_address), IPV6STR,
                       IPV62STR(got_ip6->ip6_info.ip));
    } else if (event_id == IP_EVENT_ETH_LOST_IP) {
        event.type = ETHERNET_PLATFORM_EVENT_ADDRESS_LOST;
    } else {
        return;
    }
    enqueue_platform_event(&event);
}

/* Initializes SPI, W5500, esp-netif, and callbacks without waiting for a cable. */
bool platform_ethernet_initialize(const ethernet_link_config_t *config)
{
    esp_err_t result = esp_netif_init();
    if (result != ESP_OK && result != ESP_ERR_INVALID_STATE) return false;
    result = esp_event_loop_create_default();
    if (result != ESP_OK && result != ESP_ERR_INVALID_STATE) return false;
    /* W5500 installs a per-pin callback but expects the global dispatcher first. */
    if (!is_gpio_interrupt_service_ready()) return false;

    ethernet_event_queue = xQueueCreate(ETHERNET_EVENT_QUEUE_DEPTH,
                                        sizeof(ethernet_platform_event_t));
    if (ethernet_event_queue == NULL) return false;

    const spi_bus_config_t bus_config = {
        .mosi_io_num = config->mosi_gpio,
        .miso_io_num = config->miso_gpio,
        .sclk_io_num = config->clock_gpio,
        .quadwp_io_num = -1,
        .quadhd_io_num = -1,
    };
    if (spi_bus_initialize(ETHERNET_SPI_HOST, &bus_config,
                           SPI_DMA_CH_AUTO) != ESP_OK)
        return false;

    spi_device_interface_config_t device_config = {
        .mode = 0,
        .clock_speed_hz = (int)config->spi_clock_hz,
        .spics_io_num = config->chip_select_gpio,
        .queue_size = ETHERNET_SPI_QUEUE_DEPTH,
    };
    eth_w5500_config_t w5500_config =
        ETH_W5500_DEFAULT_CONFIG(ETHERNET_SPI_HOST, &device_config);
    w5500_config.base.int_gpio_num = config->interrupt_gpio;

    eth_mac_config_t mac_config = ETH_MAC_DEFAULT_CONFIG();
    mac_config.rx_task_stack_size = ETHERNET_MAC_RX_TASK_STACK_SIZE;
    eth_phy_config_t phy_config = ETH_PHY_DEFAULT_CONFIG();
    phy_config.phy_addr = W5500_PHY_ADDRESS;
    phy_config.reset_gpio_num = config->reset_gpio;
    esp_eth_mac_t *mac = esp_eth_mac_new_w5500(&w5500_config, &mac_config);
    esp_eth_phy_t *phy = esp_eth_phy_new_w5500(&phy_config);
    if (mac == NULL || phy == NULL) return false;

    const esp_eth_config_t driver_config = ETH_DEFAULT_CONFIG(mac, phy);
    if (esp_eth_driver_install(&driver_config, &ethernet_driver) != ESP_OK)
        return false;
    uint8_t mac_address[ETH_ADDR_LEN];
    /* Use the ESP32's factory Ethernet identity because W5500 has no stored MAC. */
    if (esp_read_mac(mac_address, ESP_MAC_ETH) != ESP_OK ||
        esp_eth_ioctl(ethernet_driver, ETH_CMD_S_MAC_ADDR,
                      mac_address) != ESP_OK)
        return false;

    const esp_netif_config_t netif_config = ESP_NETIF_DEFAULT_ETH();
    ethernet_network_interface = esp_netif_new(&netif_config);
    if (ethernet_network_interface == NULL ||
        esp_netif_set_hostname(ethernet_network_interface,
                               config->hostname) != ESP_OK)
        return false;
    ethernet_netif_glue = esp_eth_new_netif_glue(ethernet_driver);
    if (ethernet_netif_glue == NULL ||
        esp_netif_attach(ethernet_network_interface,
                         ethernet_netif_glue) != ESP_OK)
        return false;
    if (esp_event_handler_register(ETH_EVENT, ESP_EVENT_ANY_ID,
                                   handle_ethernet_event, NULL) != ESP_OK ||
        esp_event_handler_register(IP_EVENT, ESP_EVENT_ANY_ID,
                                   handle_ip_event, NULL) != ESP_OK)
        return false;
    return true;
}

/* Starts the asynchronous Ethernet driver state machine. */
bool platform_ethernet_start(void)
{
    if (is_driver_started) return true;
    return ethernet_driver != NULL && esp_eth_start(ethernet_driver) == ESP_OK;
}

/* Stops the asynchronous Ethernet driver state machine. */
void platform_ethernet_stop(void)
{
    if (is_driver_started) (void)esp_eth_stop(ethernet_driver);
}

/* Gets one owned event without blocking, or reports an empty queue. */
bool platform_ethernet_get_event(ethernet_platform_event_t *event)
{
    return ethernet_event_queue != NULL &&
           xQueueReceive(ethernet_event_queue, event, 0) == pdTRUE;
}
