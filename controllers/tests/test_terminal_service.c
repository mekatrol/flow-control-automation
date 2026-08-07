#include <assert.h>
#include <stdio.h>
#include <string.h>

#include "terminal_service.h"

/* Test buffers remain larger than one complete stable menu exchange. */
enum
{
    TEST_OUTPUT_CAPACITY  = 16384,
    TEST_IDLE_TIMEOUT_MS  = 1000,
    TEST_LOGIN_BACKOFF_MS = 100,
};

typedef struct
{
    char output[TEST_OUTPUT_CAPACITY];
    size_t output_size;
    bool is_reboot_requested;
    bool is_storage_initialized;
    size_t settings_change_count;
} terminal_fixture_t;

static const char TEST_USERNAME[]        = "operator";
static const char TEST_PASSWORD[]        = "private-password";
static const char TEST_SUCCESS_MESSAGE[] = "Terminal service tests passed";

/* Accepts staged records so terminal persistence paths can be tested without media. */
static settings_store_result_t stage_record(void *context, const void *record, size_t size)
{
    assert(context != NULL);
    assert(record != NULL);
    assert(size > 0);
    return SETTINGS_STORE_OK;
}

/* Commits staged test records synchronously. */
static settings_store_result_t commit_records(void *context)
{
    assert(context != NULL);
    return SETTINGS_STORE_OK;
}

/* Discards staged test records after an injected failure. */
static void abort_records(void *context)
{
    assert(context != NULL);
}

/* Records live settings application after a durable terminal update. */
static void settings_changed_fixture(void *context)
{
    terminal_fixture_t *fixture = context;
    fixture->settings_change_count++;
}

/* Supplies redacted MQTT status for the authenticated status menu. */
static void get_fixture_mqtt_status(void *context, char *output, size_t capacity)
{
    terminal_fixture_t *fixture = context;
    assert(fixture != NULL);
    (void)snprintf(output, capacity, "state=online publish_queue=0");
}

/* Captures transport writes and rejects deterministic overflow. */
static bool write_fixture(void *context, const char *data, size_t size)
{
    terminal_fixture_t *fixture = context;
    if (fixture->output_size + size >= sizeof(fixture->output))
    {
        return false;
    }
    memcpy(fixture->output + fixture->output_size, data, size);
    fixture->output_size += size;
    fixture->output[fixture->output_size] = '\0';
    return true;
}

/* Supplies redacted stable system information without platform dependencies. */
static void get_fixture_system_info(void *context, char *output, size_t capacity)
{
    terminal_fixture_t *fixture = context;
    assert(fixture != NULL);
    (void)snprintf(output, capacity, "device=test secrets=redacted");
}

/* Records a portable reboot dispatch for confirmation testing. */
static bool reboot_fixture(void *context)
{
    terminal_fixture_t *fixture  = context;
    fixture->is_reboot_requested = true;
    return true;
}

/* Records explicit storage initialization so recovery confirmation is testable without media. */
static bool initialize_storage_fixture(void *context)
{
    terminal_fixture_t *fixture     = context;
    fixture->is_storage_initialized = true;
    return true;
}

/* Sends one complete ASCII line through the same byte interface as a transport. */
static void send_line(terminal_service_t *service, const char *line, uint64_t now_ms)
{
    terminal_service_receive(service, (const uint8_t *)line, strlen(line), now_ms);
    const uint8_t newline = '\n';
    terminal_service_receive(service, &newline, 1, now_ms);
}

/* Sends a CRLF-terminated line to verify terminals that use the network-style line ending. */
static void send_crlf_line(terminal_service_t *service, const char *line, uint64_t now_ms)
{
    terminal_service_receive(service, (const uint8_t *)line, strlen(line), now_ms);
    const uint8_t line_ending[] = {'\r', '\n'};
    terminal_service_receive(service, line_ending, sizeof(line_ending), now_ms);
}

/* Sends one raw control byte through the transport path used by interactive line editing. */
static void send_byte(terminal_service_t *service, uint8_t value, uint64_t now_ms)
{
    terminal_service_receive(service, &value, 1, now_ms);
}

/* Creates a ready credential snapshot without involving storage in authentication-only tests. */
static terminal_service_t get_authenticated_fixture(terminal_fixture_t *fixture, settings_service_t *settings)
{
    memset(settings, 0, sizeof(*settings));
    settings->state                             = SETTINGS_STORAGE_READY;
    settings->snapshot.terminal_username.is_set = true;
    settings->snapshot.terminal_password.is_set = true;
    settings->store.stage_settings              = stage_record;
    settings->store.stage_bootstrap             = stage_record;
    settings->store.commit                      = commit_records;
    settings->store.abort                       = abort_records;
    settings->store.context                     = fixture;
    (void)snprintf(settings->snapshot.terminal_username.value, sizeof(settings->snapshot.terminal_username.value), "%s",
                   TEST_USERNAME);
    (void)snprintf(settings->snapshot.terminal_password.value, sizeof(settings->snapshot.terminal_password.value), "%s",
                   TEST_PASSWORD);
    const terminal_config_t config = {.settings         = settings,
                                      .write            = write_fixture,
                                      .get_system_info  = get_fixture_system_info,
                                      .reboot           = reboot_fixture,
                                      .settings_changed = settings_changed_fixture,
                                      .get_mqtt_status  = get_fixture_mqtt_status,
                                      .context          = fixture,
                                      .idle_timeout_ms  = TEST_IDLE_TIMEOUT_MS,
                                      .login_backoff_ms = TEST_LOGIN_BACKOFF_MS};
    terminal_service_t service;
    terminal_service_init(&service, &config);
    terminal_service_connect(&service, 0);
    return service;
}

/* Verifies broker settings and status are available through the authenticated terminal. */
static void test_mqtt_configuration_and_status(void)
{
    terminal_fixture_t fixture = {0};
    settings_service_t settings;
    terminal_service_t service             = get_authenticated_fixture(&fixture, &settings);
    settings.snapshot.mqtt_username.is_set = true;
    settings.snapshot.mqtt_password.is_set = true;
    (void)snprintf(settings.snapshot.mqtt_username.value, sizeof(settings.snapshot.mqtt_username.value), "broker-user");
    (void)snprintf(settings.snapshot.mqtt_password.value, sizeof(settings.snapshot.mqtt_password.value), "broker-secret");
    (void)snprintf(settings.snapshot.mqtt_broker.host, sizeof(settings.snapshot.mqtt_broker.host), "old-broker.example.test");
    (void)snprintf(settings.snapshot.mqtt_broker.client_id, sizeof(settings.snapshot.mqtt_broker.client_id), "old-client");
    settings.snapshot.mqtt_broker.port = 1883;
    send_line(&service, TEST_USERNAME, 1);
    send_line(&service, TEST_PASSWORD, 2);
    send_line(&service, "2", 3);
    send_line(&service, "3", 4);
    assert(service.state == TERMINAL_STATE_MQTT_MENU);
    assert(strstr(fixture.output, "broker-user") != NULL);
    assert(strstr(fixture.output, "old-broker.example.test") != NULL);
    assert(strstr(fixture.output, "old-client") != NULL);
    assert(strstr(fixture.output, "broker-secret") == NULL);
    send_line(&service, "2", 5);
    assert(strstr(fixture.output, "Current broker host: old-broker.example.test") != NULL);
    send_line(&service, "/cancel", 6);
    assert(strcmp(settings.snapshot.mqtt_broker.host, "old-broker.example.test") == 0);
    assert(service.state == TERMINAL_STATE_MQTT_MENU);
    send_line(&service, "2", 7);
    send_line(&service, "broker.example.test", 8);
    send_line(&service, "3", 9);
    send_crlf_line(&service, "8883", 10);
    assert(strstr(fixture.output, "Invalid or overlength input") == NULL);
    send_line(&service, "4", 11);
    send_line(&service, "controller-unit", 12);
    send_line(&service, "5", 13);
    send_line(&service, "6", 14);
    assert(strcmp(settings.snapshot.mqtt_broker.host, "broker.example.test") == 0);
    assert(settings.snapshot.mqtt_broker.port == 8883);
    assert(strcmp(settings.snapshot.mqtt_broker.client_id, "controller-unit") == 0);
    assert(settings.snapshot.mqtt_broker.is_tls_enabled);
    assert(settings.snapshot.mqtt_broker.enabled);
    assert(fixture.settings_change_count == 5);
    send_line(&service, "7", 15);
    assert(strstr(fixture.output, "state=online publish_queue=0") != NULL);
    assert(strstr(fixture.output, TEST_PASSWORD) == NULL);
    send_line(&service, "8", 16);
    assert(service.state == TERMINAL_STATE_SETTINGS_MENU);
}

/* Verifies authentication, password masking, stable menus, diagnostics exit, and timeout logout. */
static void test_session_contract(void)
{
    terminal_fixture_t fixture = {0};
    settings_service_t settings;
    terminal_service_t service = get_authenticated_fixture(&fixture, &settings);
    send_line(&service, TEST_USERNAME, 1);
    send_line(&service, TEST_PASSWORD, 2);
    assert(service.state == TERMINAL_STATE_MAIN_MENU);
    assert(strstr(fixture.output, TEST_PASSWORD) == NULL);
    assert(strstr(fixture.output, "1. System Info") != NULL);
    send_byte(&service, (uint8_t)'3', 3);
    send_byte(&service, 127U, 3);
    send_line(&service, "2", 3);
    assert(service.state == TERMINAL_STATE_SETTINGS_MENU);
    assert(strstr(fixture.output, "\b \b") != NULL);
    send_line(&service, "4", 3);
    send_line(&service, "invalid_host", 3);
    assert(service.state == TERMINAL_STATE_EDIT_HOSTNAME);
    assert(strstr(fixture.output, "Invalid hostname") != NULL);
    send_line(&service, "controller-a16-01", 3);
    assert(service.state == TERMINAL_STATE_CONFIRM_HOSTNAME);
    send_line(&service, "NO", 3);
    assert(service.state == TERMINAL_STATE_SETTINGS_MENU);
    send_line(&service, "0", 3);
    send_line(&service, "3", 3);
    terminal_service_emit_diagnostic(&service, "diagnostic-record");
    assert(strstr(fixture.output, "diagnostic-record") != NULL);
    send_line(&service, "/menu", 4);
    assert(service.state == TERMINAL_STATE_MAIN_MENU);
    terminal_service_process(&service, TEST_IDLE_TIMEOUT_MS + 5);
    assert(service.state == TERMINAL_STATE_UNAVAILABLE);
    assert(service.last_disconnect_reason == TERMINAL_DISCONNECT_IDLE_TIMEOUT);
}

/* Verifies authenticated users can atomically update RS485 address and baud rate. */
static void test_rs485_configuration(void)
{
    terminal_fixture_t fixture = {0};
    settings_service_t settings;
    terminal_service_t service        = get_authenticated_fixture(&fixture, &settings);
    settings.snapshot.rs485.address   = 0;
    settings.snapshot.rs485.baud_rate = 115200;
    send_line(&service, TEST_USERNAME, 1);
    send_line(&service, TEST_PASSWORD, 2);
    send_line(&service, "2", 3);
    send_line(&service, "5", 4);
    assert(service.state == TERMINAL_STATE_RS485_MENU);
    assert(strstr(fixture.output, "Controller address: 0") != NULL);
    assert(strstr(fixture.output, "Baud rate: 115200 bps") != NULL);
    send_line(&service, "1", 5);
    send_line(&service, "65536", 6);
    assert(strstr(fixture.output, "Invalid RS485 address") != NULL);
    send_line(&service, "42", 7);
    assert(settings.snapshot.rs485.address == 42);
    send_line(&service, "2", 8);
    send_line(&service, "115", 9);
    assert(strstr(fixture.output, "Invalid RS485 baud rate") != NULL);
    send_line(&service, "9600", 10);
    assert(settings.snapshot.rs485.baud_rate == 9600);
    assert(fixture.settings_change_count == 2);
}

/* Verifies bad credentials are counted and throttled without exposing the password. */
static void test_failed_login_is_redacted(void)
{
    terminal_fixture_t fixture = {0};
    settings_service_t settings;
    terminal_service_t service = get_authenticated_fixture(&fixture, &settings);
    send_crlf_line(&service, TEST_USERNAME, 1);
    send_crlf_line(&service, "wrong-secret", 2);
    assert(service.failed_login_count == 1);
    assert(strstr(fixture.output, "wrong-secret") == NULL);
    assert(service.login_allowed_ms == 2 + TEST_LOGIN_BACKOFF_MS);
    assert(service.state == TERMINAL_STATE_LOGIN_USERNAME);
    send_crlf_line(&service, TEST_USERNAME, service.login_allowed_ms);
    send_crlf_line(&service, TEST_PASSWORD, service.login_allowed_ms + 1);
    assert(service.state == TERMINAL_STATE_MAIN_MENU);
}

/* Verifies unavailable storage still permits redacted information and confirmed reboot only. */
static void test_recovery_menu_remains_useful(void)
{
    terminal_fixture_t fixture     = {0};
    settings_service_t settings    = {.state = SETTINGS_STORAGE_UNAVAILABLE};
    const terminal_config_t config = {.settings                    = &settings,
                                      .write                       = write_fixture,
                                      .get_system_info             = get_fixture_system_info,
                                      .reboot                      = reboot_fixture,
                                      .context                     = &fixture,
                                      .idle_timeout_ms             = TEST_IDLE_TIMEOUT_MS,
                                      .login_backoff_ms            = TEST_LOGIN_BACKOFF_MS,
                                      .settings_unavailable_reason = "card_absent"};
    terminal_service_t service;
    terminal_service_init(&service, &config);
    terminal_service_connect(&service, 0);
    assert(service.state == TERMINAL_STATE_RECOVERY_MENU);
    assert(strstr(fixture.output, "Settings storage unavailable: card_absent.") != NULL);
    assert(strstr(fixture.output, "1. System Info") != NULL);
    assert(strstr(fixture.output, "2. Settings") == NULL);
    const size_t output_before_redraw = fixture.output_size;
    send_line(&service, "", 1);
    assert(fixture.output_size > output_before_redraw);
    assert(strstr(fixture.output + output_before_redraw, "Recovery mode") != NULL);
    send_line(&service, "1", 2);
    assert(strstr(fixture.output, "device=test secrets=redacted") != NULL);
    send_line(&service, "2", 3);
    send_line(&service, "YES", 4);
    assert(fixture.is_reboot_requested);
}

/* Verifies foreign media requires the exact destructive confirmation before initialization and reboot. */
static void test_recovery_media_initialization_requires_confirmation(void)
{
    terminal_fixture_t fixture     = {0};
    settings_service_t settings    = {.state = SETTINGS_STORAGE_UNAVAILABLE};
    const terminal_config_t config = {.settings                    = &settings,
                                      .write                       = write_fixture,
                                      .get_system_info             = get_fixture_system_info,
                                      .reboot                      = reboot_fixture,
                                      .initialize_storage          = initialize_storage_fixture,
                                      .context                     = &fixture,
                                      .idle_timeout_ms             = TEST_IDLE_TIMEOUT_MS,
                                      .login_backoff_ms            = TEST_LOGIN_BACKOFF_MS,
                                      .settings_unavailable_reason = "media_invalid_or_foreign"};
    terminal_service_t service;
    terminal_service_init(&service, &config);
    terminal_service_connect(&service, 0);
    assert(strstr(fixture.output, "2. Initialize settings storage") != NULL);
    send_line(&service, "2", 1);
    send_line(&service, "YES", 2);
    assert(!fixture.is_storage_initialized);
    send_line(&service, "2", 3);
    send_line(&service, "ERASE SETTINGS", 4);
    assert(fixture.is_storage_initialized);
    assert(fixture.is_reboot_requested);
}

/* Runs focused portable terminal protocol tests and returns success. */
int main(void)
{
    test_session_contract();
    test_failed_login_is_redacted();
    test_recovery_menu_remains_useful();
    test_recovery_media_initialization_requires_confirmation();
    test_mqtt_configuration_and_status();
    test_rs485_configuration();
    puts(TEST_SUCCESS_MESSAGE);
    return 0;
}
