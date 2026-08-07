#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#include "settings_service.h"

/* Terminal protocol limits bound memory use independently of the transport. */
enum
{
    TERMINAL_LINE_CAPACITY   = 160,
    TERMINAL_OUTPUT_CAPACITY = 1024,
};

/* Stable states form the version-one host automation contract. */
typedef enum
{
    TERMINAL_STATE_UNAVAILABLE,
    TERMINAL_STATE_SETUP_USERNAME,
    TERMINAL_STATE_SETUP_PASSWORD,
    TERMINAL_STATE_LOGIN_USERNAME,
    TERMINAL_STATE_LOGIN_PASSWORD,
    TERMINAL_STATE_MAIN_MENU,
    TERMINAL_STATE_SETTINGS_MENU,
    TERMINAL_STATE_DIAGNOSTICS,
    TERMINAL_STATE_CONFIRM_RESET,
    TERMINAL_STATE_CONFIRM_REBOOT,
    TERMINAL_STATE_EDIT_CREDENTIAL_NAME,
    TERMINAL_STATE_EDIT_CREDENTIAL_SECRET,
    TERMINAL_STATE_CONFIRM_CREDENTIAL,
    TERMINAL_STATE_EDIT_HOSTNAME,
    TERMINAL_STATE_CONFIRM_HOSTNAME,
    TERMINAL_STATE_RECOVERY_MENU,
    TERMINAL_STATE_RECOVERY_CONFIRM_INITIALIZE,
    TERMINAL_STATE_RECOVERY_CONFIRM_REBOOT,
    TERMINAL_STATE_MQTT_MENU,
    TERMINAL_STATE_EDIT_MQTT_HOST,
    TERMINAL_STATE_EDIT_MQTT_PORT,
    TERMINAL_STATE_EDIT_MQTT_CLIENT_ID,
    TERMINAL_STATE_RS485_MENU,
    TERMINAL_STATE_EDIT_RS485_ADDRESS,
    TERMINAL_STATE_EDIT_RS485_BAUD_RATE,
} terminal_state_t;

typedef enum
{
    TERMINAL_CREDENTIAL_NONE,
    TERMINAL_CREDENTIAL_WIFI,
    TERMINAL_CREDENTIAL_TERMINAL,
    TERMINAL_CREDENTIAL_MQTT,
} terminal_credential_target_t;

/* Disconnect reasons are observable without retaining session credentials. */
typedef enum
{
    TERMINAL_DISCONNECT_NONE,
    TERMINAL_DISCONNECT_TRANSPORT,
    TERMINAL_DISCONNECT_IDLE_TIMEOUT,
    TERMINAL_DISCONNECT_CREDENTIAL_CHANGE,
    TERMINAL_DISCONNECT_CONFIGURATION_RESET,
} terminal_disconnect_reason_t;

typedef struct
{
    terminal_state_t state;
    uint32_t authenticated_session_count;
    uint32_t failed_login_count;
    uint32_t output_drop_count;
    terminal_disconnect_reason_t last_disconnect_reason;
} terminal_health_t;

typedef bool (*terminal_write_function_t)(void *context, const char *data, size_t size);
typedef void (*terminal_system_info_function_t)(void *context, char *output, size_t capacity);
typedef bool (*terminal_reboot_function_t)(void *context);
typedef bool (*terminal_initialize_storage_function_t)(void *context);
typedef void (*terminal_settings_changed_function_t)(void *context);
typedef void (*terminal_mqtt_status_function_t)(void *context, char *output, size_t capacity);

typedef struct
{
    settings_service_t *settings;
    terminal_write_function_t write;
    terminal_system_info_function_t get_system_info;
    terminal_reboot_function_t reboot;
    terminal_initialize_storage_function_t initialize_storage;
    terminal_settings_changed_function_t settings_changed;
    terminal_mqtt_status_function_t get_mqtt_status;
    const char *settings_unavailable_reason;
    void *context;
    uint64_t idle_timeout_ms;
    uint32_t login_backoff_ms;
} terminal_config_t;

typedef struct
{
    terminal_config_t config;
    terminal_state_t state;
    terminal_disconnect_reason_t last_disconnect_reason;
    char line[TERMINAL_LINE_CAPACITY];
    size_t line_size;
    char pending_username[SETTINGS_USERNAME_CAPACITY];
    char login_username[SETTINGS_USERNAME_CAPACITY];
    char pending_secret[SETTINGS_PASSWORD_CAPACITY];
    char pending_hostname[SETTINGS_HOSTNAME_CAPACITY];
    uint64_t last_activity_ms;
    uint64_t login_allowed_ms;
    uint32_t authenticated_session_count;
    uint32_t failed_login_count;
    uint32_t output_drop_count;
    bool is_connected;
    bool is_password_input;
    bool is_line_rejected;
    bool is_carriage_return_pending;
    terminal_credential_target_t credential_target;
} terminal_service_t;

/* Initializes a disconnected, bounded terminal service over an abstract transport. */
void terminal_service_init(terminal_service_t *service, const terminal_config_t *config);

/* Starts a fresh unauthenticated session and displays setup or login. */
void terminal_service_connect(terminal_service_t *service, uint64_t now_ms);

/* Clears all sensitive and authenticated state after transport loss. */
void terminal_service_disconnect(terminal_service_t *service, terminal_disconnect_reason_t reason);

/* Consumes bounded ASCII transport bytes without blocking the caller. */
void terminal_service_receive(terminal_service_t *service, const uint8_t *data, size_t size, uint64_t now_ms);

/* Applies idle timeout policy and clears an expired session. */
void terminal_service_process(terminal_service_t *service, uint64_t now_ms);

/* Forwards one diagnostic record only while the authenticated session selected that mode. */
void terminal_service_emit_diagnostic(terminal_service_t *service, const char *record);

/* Gets a redacted point-in-time terminal health snapshot. */
terminal_health_t terminal_service_get_health(const terminal_service_t *service);

/* Gets the stable terminal-state diagnostic name. */
const char *terminal_get_state_name(terminal_state_t state);
