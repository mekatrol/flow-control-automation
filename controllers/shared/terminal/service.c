#include "terminal/service.h"

#include <stdio.h>
#include <string.h>

static const char PROMPT_SETUP_USERNAME[] = "First-run setup - username: ";
static const char PROMPT_SETUP_PASSWORD[] = "First-run setup - password: ";
static const char PROMPT_LOGIN_USERNAME[] = "Username: ";
static const char PROMPT_LOGIN_PASSWORD[] = "Password: ";
static const char MAIN_MENU[]             = "\r\n1. System Info\r\n2. Settings\r\n3. Diagnostics\r\n4. Reboot device\r\n> ";
static const char SETTINGS_MENU[] =
    "\r\n1. Wi-Fi credentials\r\n2. Terminal credentials\r\n3. MQTT configuration\r\n4. Device hostname\r\n"
    "5. RS485 configuration\r\n6. Protocol key\r\n7. Reset configuration\r\n0. Back\r\n> ";
static const char PROTOCOL_KEY_PROMPT[] =
    "Protocol key is write-only. Enter exactly 64 hexadecimal characters (/cancel to keep current): ";
static const char PROTOCOL_KEY_INVALID[]  = "Invalid protocol key; exactly 64 hexadecimal characters are required.\r\n";
static const char PROTOCOL_KEY_COMPLETE[] = "Protocol key updated and active sessions invalidated.\r\n";
static const char PROTOCOL_KEY_FAILED[]   = "Protocol key update failed; previous key remains active.\r\n";
static const char RS485_MENU_FORMAT[] =
    "\r\nRS485 configuration\r\n1. Controller address: %u\r\n2. Baud rate: %u bps\r\n3. Back\r\n> ";
static const char RS485_ADDRESS_PROMPT_FORMAT[] =
    "Current controller address: %u\r\nNew address 0-65535 (/cancel to keep current): ";
static const char RS485_BAUD_PROMPT_FORMAT[] =
    "Current baud rate: %u bps\r\nNew baud rate 300-3000000 (/cancel to keep current): ";
static const char RS485_INVALID_ADDRESS[] = "Invalid RS485 address.\r\n";
static const char RS485_INVALID_BAUD[]    = "Invalid RS485 baud rate.\r\n";
static const char RS485_UPDATE_COMPLETE[] = "RS485 settings applied.\r\n";
static const char RS485_COMMIT_FAILED[]   = "RS485 update failed; previous settings remain active.\r\n";
static const char MQTT_MENU_FORMAT[] =
    "\r\nMQTT configuration\r\n"
    "1. Credentials (username: %s, password: %s)\r\n2. Broker host: %s\r\n3. Broker port: %u\r\n"
    "4. Client ID: %s\r\n5. Toggle TLS: %s\r\n6. Enable/disable: %s\r\n7. Status\r\n8. Back\r\n> ";
static const char MQTT_HOST_PROMPT_FORMAT[] = "Current broker host: %s\r\nNew broker host (/cancel to keep current): ";
static const char MQTT_PORT_PROMPT_FORMAT[] = "Current broker port: %u\r\nNew broker port 1-65535 (/cancel to keep current): ";
static const char MQTT_CLIENT_ID_PROMPT_FORMAT[] = "Current client ID: %s\r\nNew client ID (/cancel to keep current): ";
static const char MQTT_INVALID_PORT[]            = "Invalid MQTT port.\r\n";
static const char MQTT_COMMIT_FAILED[]           = "MQTT update failed; previous settings remain active.\r\n";
static const char MQTT_UPDATE_COMPLETE[]         = "MQTT settings applied.\r\n";
static const char MQTT_STATUS_UNAVAILABLE[]      = "MQTT status unavailable.\r\n";
static const char VALUE_UNSET[]                  = "<unset>";
static const char VALUE_CONFIGURED[]             = "configured";
static const char VALUE_NOT_CONFIGURED[]         = "not configured";
static const char VALUE_ENABLED[]                = "enabled";
static const char VALUE_DISABLED[]               = "disabled";
static const char CANCEL_INPUT[]                 = "/cancel";
static const char CANCELLED[]                    = "Change cancelled.\r\n";
static const char INVALID_SELECTION[]            = "Invalid selection.\r\n> ";
static const char INVALID_INPUT[]                = "Invalid or overlength input.\r\n";
static const char AUTHENTICATION_FAILED[]        = "Authentication failed.\r\n";
static const char STORAGE_UNAVAILABLE[]          = "Settings storage unavailable.\r\n";
static const char STORAGE_UNAVAILABLE_FORMAT[]   = "Settings storage unavailable: %s.\r\n";
static const char DIAGNOSTICS_HEADER[]           = "Diagnostics mode. Enter /menu to return.\r\n";
static const char RESET_PROMPT[]                 = "Clear all credentials and settings? Type YES to confirm: ";
static const char REBOOT_PROMPT[]                = "Reboot device? Type YES to confirm: ";
static const char REBOOT_UNSUPPORTED[]           = "System reboot not supported by this device.\r\n";
static const char RECOVERY_MENU[] =
    "\r\nRecovery mode - persistent settings are unavailable.\r\n1. System Info\r\n2. Reboot device\r\n> ";
static const char RECOVERY_INITIALIZE_MENU[] = "\r\nRecovery mode - settings media requires initialization.\r\n1. System Info\r\n"
                                               "2. Initialize settings storage\r\n3. Reboot device\r\n> ";
static const char INITIALIZE_PROMPT[] =
    "This clears only the reserved controller settings sectors. Type ERASE SETTINGS to confirm: ";
static const char INITIALIZE_FAILED[]             = "Settings storage initialization failed; media remains unavailable.\r\n";
static const char INITIALIZE_COMPLETE[]           = "Settings storage initialized. Rebooting device.\r\n";
static const char CREDENTIAL_NAME_PROMPT_FORMAT[] = "Current username/name: %s\r\nNew username/name (/cancel to keep current): ";
static const char CREDENTIAL_SECRET_PROMPT_FORMAT[] = "Current password: %s\r\nNew password (/cancel to keep current): ";
static const char CREDENTIAL_CONFIRM_PROMPT[]       = "Replace this credential pair? Type YES to confirm: ";
static const char CREDENTIAL_COMMIT_FAILED[]        = "Credential update failed; previous settings remain active.\r\n";
static const char HOSTNAME_PROMPT_FORMAT[]   = "Current device hostname: %s\r\nNew device hostname (/cancel to keep current): ";
static const char HOSTNAME_CONFIRM_PROMPT[]  = "Replace the device hostname? Type YES to confirm: ";
static const char HOSTNAME_INVALID[]         = "Invalid hostname; use 1-63 letters, digits, or hyphens.\r\n";
static const char HOSTNAME_COMMIT_FAILED[]   = "Hostname update failed; previous settings remain active.\r\n";
static const char HOSTNAME_COMMIT_COMPLETE[] = "Hostname updated; reboot to apply it to network interfaces.\r\n";
static const char DIAGNOSTICS_EXIT[]         = "/menu";
static const char CONFIRM_VALUE[]            = "YES";
static const char INITIALIZE_CONFIRM_VALUE[] = "ERASE SETTINGS";
static const char LINE_ENDING[]              = "\r\n";
static const char ERASE_CHARACTER[]          = "\b \b";

/* Broker TCP ports are constrained by the protocol field width. */
enum
{
    MQTT_MINIMUM_PORT       = 1,
    MQTT_MAXIMUM_PORT       = 65535,
    MQTT_DEFAULT_PORT       = 1883,
    RS485_MINIMUM_BAUD_RATE = 300,
    RS485_MAXIMUM_BAUD_RATE = 3000000,
    RS485_MAXIMUM_ADDRESS   = 65535,
    PROTOCOL_KEY_CHARACTERS = 64,
};

/* Tests whether a write-only protocol key is exactly one 256-bit hexadecimal value. */
static bool is_protocol_key_valid(const char *value)
{
    if (strlen(value) != PROTOCOL_KEY_CHARACTERS)
    {
        return false;
    }

    for (size_t index = 0; index < PROTOCOL_KEY_CHARACTERS; index++)
    {
        const char character = value[index];

        if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f') ||
              (character >= 'A' && character <= 'F')))
        {
            return false;
        }
    }

    return true;
}

/* Writes a bounded record and accounts for a slow or disconnected transport. */
static void write_output(terminal_service_t *service, const char *output)
{
    const size_t size = strlen(output);

    if (service->config.write == NULL || !service->config.write(service->config.context, output, size))
    {
        service->output_drop_count++;
    }
}

/* Erases credential-bearing buffers with volatile stores so optimization cannot retain them. */
static void clear_sensitive(terminal_service_t *service)
{
    volatile char *line    = service->line;
    volatile char *pending = service->pending_username;

    for (size_t index = 0; index < sizeof(service->line); index++)
    {
        line[index] = '\0';
    }

    for (size_t index = 0; index < sizeof(service->pending_username); index++)
    {
        pending[index] = '\0';
    }

    volatile char *login = service->login_username;

    for (size_t index = 0; index < sizeof(service->login_username); index++)
    {
        login[index] = '\0';
    }

    volatile char *secret = service->pending_secret;

    for (size_t index = 0; index < sizeof(service->pending_secret); index++)
    {
        secret[index] = '\0';
    }

    volatile char *hostname = service->pending_hostname;

    for (size_t index = 0; index < sizeof(service->pending_hostname); index++)
    {
        hostname[index] = '\0';
    }

    service->line_size = 0;
}

/* Gets a bounded string length without relying on non-standard library extensions. */
static size_t get_bounded_length(const char *value, size_t capacity)
{
    size_t size = 0;

    while (size < capacity && value[size] != '\0')
    {
        size++;
    }

    return size;
}

/* Copies a bounded string and always terminates the destination. */
static void copy_bounded(char *destination, size_t capacity, const char *source)
{
    if (capacity == 0)
    {
        return;
    }

    const size_t source_size = get_bounded_length(source, capacity - 1);
    memcpy(destination, source, source_size);
    destination[source_size] = '\0';
}

/* Compares bounded credentials without revealing an early mismatch through control flow. */
static bool is_credential_equal(const settings_nullable_string_t *expected, const char *actual)
{
    size_t actual_size   = get_bounded_length(actual, SETTINGS_PASSWORD_CAPACITY);
    size_t expected_size = get_bounded_length(expected->value, SETTINGS_PASSWORD_CAPACITY);
    unsigned difference  = (unsigned)(actual_size ^ expected_size) | (expected->is_set ? 0U : 1U);

    for (size_t index = 0; index < SETTINGS_PASSWORD_CAPACITY; index++)
    {
        const unsigned left  = index < expected_size ? (unsigned char)expected->value[index] : 0U;
        const unsigned right = index < actual_size ? (unsigned char)actual[index] : 0U;
        difference |= left ^ right;
    }

    return difference == 0U;
}

/* Tests a portable DNS hostname label before it can be persisted or passed to a network adapter. */
static bool is_hostname_valid(const char *hostname)
{
    const size_t size = get_bounded_length(hostname, SETTINGS_HOSTNAME_CAPACITY);

    if (size == 0 || size >= SETTINGS_HOSTNAME_CAPACITY || hostname[0] == '-' || hostname[size - 1] == '-')
    {
        return false;
    }

    for (size_t index = 0; index < size; index++)
    {
        const char character = hostname[index];

        if (!((character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z') ||
              (character >= '0' && character <= '9') || character == '-'))
        {
            return false;
        }
    }

    return true;
}

/* Displays the stable main menu after successful authentication. */
static void show_main_menu(terminal_service_t *service)
{
    service->state             = TERMINAL_STATE_MAIN_MENU;
    service->is_password_input = false;
    write_output(service, MAIN_MENU);
}

/* Displays the authenticated MQTT configuration menu without exposing credentials. */
static void show_mqtt_menu(terminal_service_t *service)
{
    char menu[TERMINAL_OUTPUT_CAPACITY];
    const controller_settings_t settings = settings_service_get_snapshot(service->config.settings);
    const settings_mqtt_broker_t *broker = &settings.mqtt_broker;
    const char *username                 = settings.mqtt_username.is_set ? settings.mqtt_username.value : VALUE_UNSET;
    const char *password                 = settings.mqtt_password.is_set ? VALUE_CONFIGURED : VALUE_NOT_CONFIGURED;
    const char *host                     = broker->host[0] != '\0' ? broker->host : VALUE_UNSET;
    const char *client_id                = broker->client_id[0] != '\0' ? broker->client_id : VALUE_UNSET;
    const unsigned port                  = broker->port != 0 ? (unsigned)broker->port : MQTT_DEFAULT_PORT;
    snprintf(menu, sizeof(menu), MQTT_MENU_FORMAT, username, password, host, port, client_id,
             broker->is_tls_enabled ? VALUE_ENABLED : VALUE_DISABLED, broker->enabled ? VALUE_ENABLED : VALUE_DISABLED);
    service->state = TERMINAL_STATE_MQTT_MENU;
    write_output(service, menu);
}

/* Displays the RS485 address and line rate without exposing transport implementation details. */
static void show_rs485_menu(terminal_service_t *service)
{
    char menu[TERMINAL_OUTPUT_CAPACITY];
    const settings_rs485_t rs485 = settings_service_get_snapshot(service->config.settings).rs485;
    snprintf(menu, sizeof(menu), RS485_MENU_FORMAT, (unsigned)rs485.address, (unsigned)rs485.baud_rate);
    service->state = TERMINAL_STATE_RS485_MENU;
    write_output(service, menu);
}

/* Writes an MQTT value prompt containing its current non-secret value and cancellation command. */
static void show_mqtt_text_prompt(terminal_service_t *service, const char *format, const char *current_value)
{
    char prompt[TERMINAL_OUTPUT_CAPACITY];
    snprintf(prompt, sizeof(prompt), format, current_value[0] != '\0' ? current_value : VALUE_UNSET);
    write_output(service, prompt);
}

/* Tests whether the current editor may return without changing persistent settings. */
static bool is_cancelable_editor(terminal_state_t state)
{
    return state == TERMINAL_STATE_EDIT_CREDENTIAL_NAME || state == TERMINAL_STATE_EDIT_CREDENTIAL_SECRET ||
           state == TERMINAL_STATE_CONFIRM_CREDENTIAL || state == TERMINAL_STATE_EDIT_HOSTNAME ||
           state == TERMINAL_STATE_CONFIRM_HOSTNAME || state == TERMINAL_STATE_EDIT_MQTT_HOST ||
           state == TERMINAL_STATE_EDIT_MQTT_PORT || state == TERMINAL_STATE_EDIT_MQTT_CLIENT_ID ||
           state == TERMINAL_STATE_EDIT_RS485_ADDRESS || state == TERMINAL_STATE_EDIT_RS485_BAUD_RATE ||
           state == TERMINAL_STATE_EDIT_PROTOCOL_KEY;
}

/* Cancels an editor and returns to the owning menu without committing staged data. */
static void cancel_editor(terminal_service_t *service)
{
    const bool is_mqtt_editor =
        service->credential_target == TERMINAL_CREDENTIAL_MQTT || service->state == TERMINAL_STATE_EDIT_MQTT_HOST ||
        service->state == TERMINAL_STATE_EDIT_MQTT_PORT || service->state == TERMINAL_STATE_EDIT_MQTT_CLIENT_ID;
    const bool is_rs485_editor =
        service->state == TERMINAL_STATE_EDIT_RS485_ADDRESS || service->state == TERMINAL_STATE_EDIT_RS485_BAUD_RATE;
    clear_sensitive(service);
    service->credential_target = TERMINAL_CREDENTIAL_NONE;
    write_output(service, CANCELLED);

    if (is_mqtt_editor)
    {
        show_mqtt_menu(service);
    }

    else if (is_rs485_editor)
    {
        show_rs485_menu(service);
    }

    else
    {
        service->state = TERMINAL_STATE_SETTINGS_MENU;
        write_output(service, SETTINGS_MENU);
    }
}

/* Commits one complete settings update and notifies the runtime only after durable success. */
static bool is_settings_update_successful(terminal_service_t *service, const controller_settings_t *settings)
{
    const bool is_success = settings_service_commit(service->config.settings, settings) == SETTINGS_STORE_OK;

    if (is_success && service->config.settings_changed != NULL)
    {
        service->config.settings_changed(service->config.context);
    }

    return is_success;
}

/* Reports a broker update outcome and returns to its stable menu. */
static void finish_mqtt_update(terminal_service_t *service, bool is_success)
{
    write_output(service, is_success ? MQTT_UPDATE_COMPLETE : MQTT_COMMIT_FAILED);
    show_mqtt_menu(service);
}

/* Displays only safe recovery operations when persistent authentication is unavailable. */
static void show_recovery_menu(terminal_service_t *service)
{
    service->state             = TERMINAL_STATE_RECOVERY_MENU;
    service->is_password_input = false;
    write_output(service, service->config.initialize_storage != NULL ? RECOVERY_INITIALIZE_MENU : RECOVERY_MENU);
}

/* Writes the portable redacted system-information snapshot used by both menus. */
static void show_system_info(terminal_service_t *service)
{
    char information[TERMINAL_OUTPUT_CAPACITY];

    if (service->config.get_system_info != NULL)
    {
        service->config.get_system_info(service->config.context, information, sizeof(information));
        write_output(service, information);
        write_output(service, LINE_ENDING);
    }
}

/* Redraws the current stable prompt after USB attachment or an empty input line. */
static void redraw_current_view(terminal_service_t *service)
{
    if (service->state == TERMINAL_STATE_RECOVERY_MENU)
    {
        show_recovery_menu(service);
    }

    else if (service->state == TERMINAL_STATE_MAIN_MENU)
    {
        show_main_menu(service);
    }

    else if (service->state == TERMINAL_STATE_LOGIN_USERNAME)
    {
        write_output(service, PROMPT_LOGIN_USERNAME);
    }

    else if (service->state == TERMINAL_STATE_SETUP_USERNAME)
    {
        write_output(service, PROMPT_SETUP_USERNAME);
    }

    else if (service->state == TERMINAL_STATE_SETTINGS_MENU)
    {
        write_output(service, SETTINGS_MENU);
    }

    else if (service->state == TERMINAL_STATE_MQTT_MENU)
    {
        show_mqtt_menu(service);
    }
}

/* Begins a masked atomic credential-pair editor for one settings owner. */
static void start_credential_edit(terminal_service_t *service, terminal_credential_target_t target)
{
    const controller_settings_t settings      = settings_service_get_snapshot(service->config.settings);
    const settings_nullable_string_t *current = NULL;

    if (target == TERMINAL_CREDENTIAL_WIFI)
    {
        current = &settings.wifi_ssid;
    }

    else if (target == TERMINAL_CREDENTIAL_TERMINAL)
    {
        current = &settings.terminal_username;
    }

    else if (target == TERMINAL_CREDENTIAL_MQTT)
    {
        current = &settings.mqtt_username;
    }

    service->credential_target = target;
    service->state             = TERMINAL_STATE_EDIT_CREDENTIAL_NAME;
    show_mqtt_text_prompt(service, CREDENTIAL_NAME_PROMPT_FORMAT,
                          current != NULL && current->is_set ? current->value : VALUE_UNSET);
}

/* Gets whether the current credential target already has a persisted password. */
static bool is_current_password_configured(const terminal_service_t *service, const controller_settings_t *settings)
{
    if (service->credential_target == TERMINAL_CREDENTIAL_WIFI)
    {
        return settings->wifi_password.is_set;
    }

    if (service->credential_target == TERMINAL_CREDENTIAL_TERMINAL)
    {
        return settings->terminal_password.is_set;
    }

    return service->credential_target == TERMINAL_CREDENTIAL_MQTT && settings->mqtt_password.is_set;
}

/* Applies the staged pair to one typed owner and preserves all unrelated settings. */
static bool is_credential_commit_successful(terminal_service_t *service)
{
    controller_settings_t settings     = settings_service_get_snapshot(service->config.settings);
    settings_nullable_string_t *name   = NULL;
    settings_nullable_string_t *secret = NULL;

    if (service->credential_target == TERMINAL_CREDENTIAL_WIFI)
    {
        name   = &settings.wifi_ssid;
        secret = &settings.wifi_password;
    }

    else if (service->credential_target == TERMINAL_CREDENTIAL_TERMINAL)
    {
        name   = &settings.terminal_username;
        secret = &settings.terminal_password;
    }

    else if (service->credential_target == TERMINAL_CREDENTIAL_MQTT)
    {
        name   = &settings.mqtt_username;
        secret = &settings.mqtt_password;
    }

    if (name == NULL || secret == NULL)
    {
        return false;
    }

    name->is_set   = true;
    secret->is_set = true;
    copy_bounded(name->value, sizeof(name->value), service->pending_username);
    copy_bounded(secret->value, sizeof(secret->value), service->pending_secret);
    const bool is_success = is_settings_update_successful(service, &settings);
    memset(&settings, 0, sizeof(settings));

    return is_success;
}

/* Selects setup versus login from nullable persisted terminal credentials. */
static void start_authentication(terminal_service_t *service)
{
    if (service->config.settings == NULL || service->config.settings->state != SETTINGS_STORAGE_READY)
    {
        service->state = TERMINAL_STATE_UNAVAILABLE;

        if (service->config.settings_unavailable_reason != NULL)
        {
            char message[TERMINAL_OUTPUT_CAPACITY];
            snprintf(message, sizeof(message), STORAGE_UNAVAILABLE_FORMAT, service->config.settings_unavailable_reason);
            write_output(service, message);
        }

        else
        {
            write_output(service, STORAGE_UNAVAILABLE);
        }

        show_recovery_menu(service);

        return;
    }

    const controller_settings_t settings = settings_service_get_snapshot(service->config.settings);

    if (!settings.terminal_username.is_set)
    {
        service->state = TERMINAL_STATE_SETUP_USERNAME;
        write_output(service, PROMPT_SETUP_USERNAME);
    }

    else if (!settings.terminal_password.is_set)
    {
        service->state = TERMINAL_STATE_SETUP_PASSWORD;
        copy_bounded(service->pending_username, sizeof(service->pending_username), settings.terminal_username.value);
        service->is_password_input = true;
        write_output(service, PROMPT_SETUP_PASSWORD);
    }

    else
    {
        service->state = TERMINAL_STATE_LOGIN_USERNAME;
        write_output(service, PROMPT_LOGIN_USERNAME);
    }
}

/* Commits first-run terminal credentials as one complete settings generation. */
static void commit_setup(terminal_service_t *service, const char *password)
{
    controller_settings_t settings    = settings_service_get_snapshot(service->config.settings);
    settings.terminal_username.is_set = true;
    settings.terminal_password.is_set = true;
    snprintf(settings.terminal_username.value, sizeof(settings.terminal_username.value), "%s", service->pending_username);
    snprintf(settings.terminal_password.value, sizeof(settings.terminal_password.value), "%s", password);

    if (settings_service_commit(service->config.settings, &settings) == SETTINGS_STORE_OK)
    {
        service->authenticated_session_count = 1;
        clear_sensitive(service);
        show_main_menu(service);
    }

    else
    {
        clear_sensitive(service);
        start_authentication(service);
    }

    memset(&settings, 0, sizeof(settings));
}

/* Handles one complete validated input line according to the current protocol state. */
static void handle_line(terminal_service_t *service, uint64_t now_ms)
{
    controller_settings_t settings = settings_service_get_snapshot(service->config.settings);

    if (is_cancelable_editor(service->state) && strcmp(service->line, CANCEL_INPUT) == 0)
    {
        cancel_editor(service);
    }

    else if (service->state == TERMINAL_STATE_SETUP_USERNAME)
    {
        copy_bounded(service->pending_username, sizeof(service->pending_username), service->line);
        service->state             = TERMINAL_STATE_SETUP_PASSWORD;
        service->is_password_input = true;
        write_output(service, PROMPT_SETUP_PASSWORD);
    }

    else if (service->state == TERMINAL_STATE_SETUP_PASSWORD)
    {
        commit_setup(service, service->line);
    }

    else if (service->state == TERMINAL_STATE_LOGIN_USERNAME)
    {
        copy_bounded(service->login_username, sizeof(service->login_username), service->line);
        service->state             = TERMINAL_STATE_LOGIN_PASSWORD;
        service->is_password_input = true;
        write_output(service, PROMPT_LOGIN_PASSWORD);
    }

    else if (service->state == TERMINAL_STATE_LOGIN_PASSWORD)
    {
        const bool is_authenticated = now_ms >= service->login_allowed_ms &&
                                      is_credential_equal(&settings.terminal_username, service->login_username) &&
                                      is_credential_equal(&settings.terminal_password, service->line);
        clear_sensitive(service);

        if (is_authenticated)
        {
            service->authenticated_session_count = 1;
            show_main_menu(service);
        }

        else
        {
            service->failed_login_count++;
            service->login_allowed_ms = now_ms + service->config.login_backoff_ms;
            write_output(service, AUTHENTICATION_FAILED);
            start_authentication(service);
        }
    }

    else if (service->state == TERMINAL_STATE_MAIN_MENU && strcmp(service->line, "1") == 0)
    {
        show_system_info(service);
        show_main_menu(service);
    }

    else if (service->state == TERMINAL_STATE_RECOVERY_MENU && strcmp(service->line, "1") == 0)
    {
        show_system_info(service);
        show_recovery_menu(service);
    }

    else if (service->state == TERMINAL_STATE_RECOVERY_MENU && service->config.initialize_storage != NULL &&
             strcmp(service->line, "2") == 0)
    {
        service->state = TERMINAL_STATE_RECOVERY_CONFIRM_INITIALIZE;
        write_output(service, INITIALIZE_PROMPT);
    }

    else if (service->state == TERMINAL_STATE_RECOVERY_MENU &&
             strcmp(service->line, service->config.initialize_storage != NULL ? "3" : "2") == 0)
    {
        service->state = TERMINAL_STATE_RECOVERY_CONFIRM_REBOOT;
        write_output(service, REBOOT_PROMPT);
    }

    else if (service->state == TERMINAL_STATE_MAIN_MENU && strcmp(service->line, "2") == 0)
    {
        service->state = TERMINAL_STATE_SETTINGS_MENU;
        write_output(service, SETTINGS_MENU);
    }

    else if (service->state == TERMINAL_STATE_MAIN_MENU && strcmp(service->line, "3") == 0)
    {
        service->state = TERMINAL_STATE_DIAGNOSTICS;
        write_output(service, DIAGNOSTICS_HEADER);
    }

    else if (service->state == TERMINAL_STATE_MAIN_MENU && strcmp(service->line, "4") == 0)
    {
        service->state = TERMINAL_STATE_CONFIRM_REBOOT;
        write_output(service, REBOOT_PROMPT);
    }

    else if (service->state == TERMINAL_STATE_SETTINGS_MENU && strcmp(service->line, "7") == 0)
    {
        service->state = TERMINAL_STATE_CONFIRM_RESET;
        write_output(service, RESET_PROMPT);
    }

    else if (service->state == TERMINAL_STATE_SETTINGS_MENU && strcmp(service->line, "1") == 0)
    {
        start_credential_edit(service, TERMINAL_CREDENTIAL_WIFI);
    }

    else if (service->state == TERMINAL_STATE_SETTINGS_MENU && strcmp(service->line, "2") == 0)
    {
        start_credential_edit(service, TERMINAL_CREDENTIAL_TERMINAL);
    }

    else if (service->state == TERMINAL_STATE_SETTINGS_MENU && strcmp(service->line, "3") == 0)
    {
        show_mqtt_menu(service);
    }

    else if (service->state == TERMINAL_STATE_SETTINGS_MENU && strcmp(service->line, "4") == 0)
    {
        service->state = TERMINAL_STATE_EDIT_HOSTNAME;
        show_mqtt_text_prompt(service, HOSTNAME_PROMPT_FORMAT, settings.hostname.is_set ? settings.hostname.value : VALUE_UNSET);
    }

    else if (service->state == TERMINAL_STATE_SETTINGS_MENU && strcmp(service->line, "5") == 0)
    {
        show_rs485_menu(service);
    }

    else if (service->state == TERMINAL_STATE_SETTINGS_MENU && strcmp(service->line, "6") == 0)
    {
        service->state             = TERMINAL_STATE_EDIT_PROTOCOL_KEY;
        service->is_password_input = true;
        write_output(service, PROTOCOL_KEY_PROMPT);
    }

    else if (service->state == TERMINAL_STATE_SETTINGS_MENU && strcmp(service->line, "0") == 0)
    {
        show_main_menu(service);
    }

    else if (service->state == TERMINAL_STATE_MQTT_MENU && strcmp(service->line, "1") == 0)
    {
        start_credential_edit(service, TERMINAL_CREDENTIAL_MQTT);
    }

    else if (service->state == TERMINAL_STATE_RS485_MENU && strcmp(service->line, "1") == 0)
    {
        char prompt[TERMINAL_OUTPUT_CAPACITY];
        snprintf(prompt, sizeof(prompt), RS485_ADDRESS_PROMPT_FORMAT, (unsigned)settings.rs485.address);
        service->state = TERMINAL_STATE_EDIT_RS485_ADDRESS;
        write_output(service, prompt);
    }

    else if (service->state == TERMINAL_STATE_RS485_MENU && strcmp(service->line, "2") == 0)
    {
        char prompt[TERMINAL_OUTPUT_CAPACITY];
        snprintf(prompt, sizeof(prompt), RS485_BAUD_PROMPT_FORMAT, (unsigned)settings.rs485.baud_rate);
        service->state = TERMINAL_STATE_EDIT_RS485_BAUD_RATE;
        write_output(service, prompt);
    }

    else if (service->state == TERMINAL_STATE_RS485_MENU && (strcmp(service->line, "3") == 0 || strcmp(service->line, "0") == 0))
    {
        service->state = TERMINAL_STATE_SETTINGS_MENU;
        write_output(service, SETTINGS_MENU);
    }

    else if (service->state == TERMINAL_STATE_EDIT_PROTOCOL_KEY)
    {
        service->is_password_input = false;

        if (!is_protocol_key_valid(service->line))
        {
            write_output(service, PROTOCOL_KEY_INVALID);
            service->is_password_input = true;
            write_output(service, PROTOCOL_KEY_PROMPT);
        }

        else
        {
            controller_settings_t updated = settings_service_get_snapshot(service->config.settings);
            updated.protocol_key.is_set   = true;
            copy_bounded(updated.protocol_key.value, sizeof(updated.protocol_key.value), service->line);
            const bool is_success = is_settings_update_successful(service, &updated);
            memset(&updated, 0, sizeof(updated));
            write_output(service, is_success ? PROTOCOL_KEY_COMPLETE : PROTOCOL_KEY_FAILED);
            service->state = TERMINAL_STATE_SETTINGS_MENU;
            write_output(service, SETTINGS_MENU);
        }
    }

    else if (service->state == TERMINAL_STATE_EDIT_RS485_ADDRESS)
    {
        unsigned address = 0;
        char trailing    = '\0';

        if (sscanf(service->line, "%u%c", &address, &trailing) != 1 || address > RS485_MAXIMUM_ADDRESS)
        {
            write_output(service, RS485_INVALID_ADDRESS);
            char prompt[TERMINAL_OUTPUT_CAPACITY];
            snprintf(prompt, sizeof(prompt), RS485_ADDRESS_PROMPT_FORMAT, (unsigned)settings.rs485.address);
            write_output(service, prompt);
        }

        else
        {
            controller_settings_t updated = settings_service_get_snapshot(service->config.settings);
            updated.rs485.address         = (uint16_t)address;
            const bool is_success         = is_settings_update_successful(service, &updated);
            write_output(service, is_success ? RS485_UPDATE_COMPLETE : RS485_COMMIT_FAILED);
            show_rs485_menu(service);
        }
    }

    else if (service->state == TERMINAL_STATE_EDIT_RS485_BAUD_RATE)
    {
        unsigned baud_rate = 0;
        char trailing      = '\0';

        if (sscanf(service->line, "%u%c", &baud_rate, &trailing) != 1 || baud_rate < RS485_MINIMUM_BAUD_RATE ||
            baud_rate > RS485_MAXIMUM_BAUD_RATE)
        {
            write_output(service, RS485_INVALID_BAUD);
            char prompt[TERMINAL_OUTPUT_CAPACITY];
            snprintf(prompt, sizeof(prompt), RS485_BAUD_PROMPT_FORMAT, (unsigned)settings.rs485.baud_rate);
            write_output(service, prompt);
        }

        else
        {
            controller_settings_t updated = settings_service_get_snapshot(service->config.settings);
            updated.rs485.baud_rate       = baud_rate;
            const bool is_success         = is_settings_update_successful(service, &updated);
            write_output(service, is_success ? RS485_UPDATE_COMPLETE : RS485_COMMIT_FAILED);
            show_rs485_menu(service);
        }
    }

    else if (service->state == TERMINAL_STATE_MQTT_MENU && strcmp(service->line, "2") == 0)
    {
        service->state = TERMINAL_STATE_EDIT_MQTT_HOST;
        show_mqtt_text_prompt(service, MQTT_HOST_PROMPT_FORMAT, settings.mqtt_broker.host);
    }

    else if (service->state == TERMINAL_STATE_MQTT_MENU && strcmp(service->line, "3") == 0)
    {
        service->state = TERMINAL_STATE_EDIT_MQTT_PORT;
        char prompt[TERMINAL_OUTPUT_CAPACITY];
        const unsigned port = settings.mqtt_broker.port != 0 ? (unsigned)settings.mqtt_broker.port : MQTT_DEFAULT_PORT;
        snprintf(prompt, sizeof(prompt), MQTT_PORT_PROMPT_FORMAT, port);
        write_output(service, prompt);
    }

    else if (service->state == TERMINAL_STATE_MQTT_MENU && strcmp(service->line, "4") == 0)
    {
        service->state = TERMINAL_STATE_EDIT_MQTT_CLIENT_ID;
        show_mqtt_text_prompt(service, MQTT_CLIENT_ID_PROMPT_FORMAT, settings.mqtt_broker.client_id);
    }

    else if (service->state == TERMINAL_STATE_MQTT_MENU && (strcmp(service->line, "5") == 0 || strcmp(service->line, "6") == 0))
    {
        controller_settings_t updated = settings_service_get_snapshot(service->config.settings);

        if (strcmp(service->line, "5") == 0)
        {
            updated.mqtt_broker.is_tls_enabled = !updated.mqtt_broker.is_tls_enabled;
        }

        else
        {
            updated.mqtt_broker.enabled = !updated.mqtt_broker.enabled;
        }

        finish_mqtt_update(service, is_settings_update_successful(service, &updated));
        memset(&updated, 0, sizeof(updated));
    }

    else if (service->state == TERMINAL_STATE_MQTT_MENU && strcmp(service->line, "7") == 0)
    {
        if (service->config.get_mqtt_status != NULL)
        {
            char status[TERMINAL_OUTPUT_CAPACITY];
            service->config.get_mqtt_status(service->config.context, status, sizeof(status));
            write_output(service, status);
            write_output(service, LINE_ENDING);
        }

        else
        {
            write_output(service, MQTT_STATUS_UNAVAILABLE);
        }

        show_mqtt_menu(service);
    }

    else if (service->state == TERMINAL_STATE_MQTT_MENU && (strcmp(service->line, "8") == 0 || strcmp(service->line, "0") == 0))
    {
        service->state = TERMINAL_STATE_SETTINGS_MENU;
        write_output(service, SETTINGS_MENU);
    }

    else if (service->state == TERMINAL_STATE_EDIT_MQTT_HOST)
    {
        controller_settings_t updated = settings_service_get_snapshot(service->config.settings);
        copy_bounded(updated.mqtt_broker.host, sizeof(updated.mqtt_broker.host), service->line);

        if (service->line[0] == '\0')
        {
            updated.mqtt_broker.enabled = false;
        }

        finish_mqtt_update(service, is_settings_update_successful(service, &updated));
        memset(&updated, 0, sizeof(updated));
    }

    else if (service->state == TERMINAL_STATE_EDIT_MQTT_CLIENT_ID)
    {
        controller_settings_t updated = settings_service_get_snapshot(service->config.settings);
        copy_bounded(updated.mqtt_broker.client_id, sizeof(updated.mqtt_broker.client_id), service->line);
        finish_mqtt_update(service, is_settings_update_successful(service, &updated));
        memset(&updated, 0, sizeof(updated));
    }

    else if (service->state == TERMINAL_STATE_EDIT_MQTT_PORT)
    {
        unsigned port = 0;
        char trailing = '\0';

        if (sscanf(service->line, "%u%c", &port, &trailing) != 1 || port < MQTT_MINIMUM_PORT || port > MQTT_MAXIMUM_PORT)
        {
            write_output(service, MQTT_INVALID_PORT);
            char prompt[TERMINAL_OUTPUT_CAPACITY];
            const unsigned port = settings.mqtt_broker.port != 0 ? (unsigned)settings.mqtt_broker.port : MQTT_DEFAULT_PORT;
            snprintf(prompt, sizeof(prompt), MQTT_PORT_PROMPT_FORMAT, port);
            write_output(service, prompt);
        }

        else
        {
            controller_settings_t updated = settings_service_get_snapshot(service->config.settings);
            updated.mqtt_broker.port      = (uint16_t)port;
            finish_mqtt_update(service, is_settings_update_successful(service, &updated));
            memset(&updated, 0, sizeof(updated));
        }
    }

    else if (service->state == TERMINAL_STATE_EDIT_CREDENTIAL_NAME)
    {
        copy_bounded(service->pending_username, sizeof(service->pending_username), service->line);
        service->state             = TERMINAL_STATE_EDIT_CREDENTIAL_SECRET;
        service->is_password_input = true;
        show_mqtt_text_prompt(service, CREDENTIAL_SECRET_PROMPT_FORMAT,
                              is_current_password_configured(service, &settings) ? VALUE_CONFIGURED : VALUE_NOT_CONFIGURED);
    }

    else if (service->state == TERMINAL_STATE_EDIT_CREDENTIAL_SECRET)
    {
        copy_bounded(service->pending_secret, sizeof(service->pending_secret), service->line);
        service->state             = TERMINAL_STATE_CONFIRM_CREDENTIAL;
        service->is_password_input = false;
        write_output(service, CREDENTIAL_CONFIRM_PROMPT);
    }

    else if (service->state == TERMINAL_STATE_CONFIRM_CREDENTIAL)
    {
        const bool is_confirmed                   = strcmp(service->line, CONFIRM_VALUE) == 0;
        const terminal_credential_target_t target = service->credential_target;
        const bool is_committed                   = is_confirmed && is_credential_commit_successful(service);
        clear_sensitive(service);

        if (is_confirmed && !is_committed)
        {
            write_output(service, CREDENTIAL_COMMIT_FAILED);
        }

        if (is_committed && target == TERMINAL_CREDENTIAL_TERMINAL)
        {
            terminal_service_disconnect(service, TERMINAL_DISCONNECT_CREDENTIAL_CHANGE);
            terminal_service_connect(service, now_ms);
        }

        else
        {
            service->state = TERMINAL_STATE_SETTINGS_MENU;
            write_output(service, SETTINGS_MENU);
        }
    }

    else if (service->state == TERMINAL_STATE_EDIT_HOSTNAME)
    {
        if (!is_hostname_valid(service->line))
        {
            write_output(service, HOSTNAME_INVALID);
            show_mqtt_text_prompt(service, HOSTNAME_PROMPT_FORMAT,
                                  settings.hostname.is_set ? settings.hostname.value : VALUE_UNSET);
        }

        else
        {
            copy_bounded(service->pending_hostname, sizeof(service->pending_hostname), service->line);
            service->state = TERMINAL_STATE_CONFIRM_HOSTNAME;
            write_output(service, HOSTNAME_CONFIRM_PROMPT);
        }
    }

    else if (service->state == TERMINAL_STATE_CONFIRM_HOSTNAME)
    {
        if (strcmp(service->line, CONFIRM_VALUE) == 0)
        {
            controller_settings_t updated = settings_service_get_snapshot(service->config.settings);
            updated.hostname.is_set       = true;
            copy_bounded(updated.hostname.value, sizeof(updated.hostname.value), service->pending_hostname);

            if (is_settings_update_successful(service, &updated))
            {
                write_output(service, HOSTNAME_COMMIT_COMPLETE);
            }

            else
            {
                write_output(service, HOSTNAME_COMMIT_FAILED);
            }

            memset(&updated, 0, sizeof(updated));
        }

        clear_sensitive(service);
        service->state = TERMINAL_STATE_SETTINGS_MENU;
        write_output(service, SETTINGS_MENU);
    }

    else if (service->state == TERMINAL_STATE_CONFIRM_RESET)
    {
        if (strcmp(service->line, CONFIRM_VALUE) == 0 && settings_service_reset(service->config.settings) == SETTINGS_STORE_OK)
        {
            terminal_service_disconnect(service, TERMINAL_DISCONNECT_CONFIGURATION_RESET);
            terminal_service_connect(service, now_ms);
        }

        else
        {
            show_main_menu(service);
        }
    }

    else if (service->state == TERMINAL_STATE_CONFIRM_REBOOT)
    {
        if (strcmp(service->line, CONFIRM_VALUE) == 0 &&
            (service->config.reboot == NULL || !service->config.reboot(service->config.context)))
        {
            write_output(service, REBOOT_UNSUPPORTED);
        }

        show_main_menu(service);
    }

    else if (service->state == TERMINAL_STATE_RECOVERY_CONFIRM_REBOOT)
    {
        if (strcmp(service->line, CONFIRM_VALUE) == 0 &&
            (service->config.reboot == NULL || !service->config.reboot(service->config.context)))
        {
            write_output(service, REBOOT_UNSUPPORTED);
        }

        show_recovery_menu(service);
    }

    else if (service->state == TERMINAL_STATE_RECOVERY_CONFIRM_INITIALIZE)
    {
        if (strcmp(service->line, INITIALIZE_CONFIRM_VALUE) == 0 && service->config.initialize_storage != NULL &&
            service->config.initialize_storage(service->config.context))
        {
            write_output(service, INITIALIZE_COMPLETE);

            if (service->config.reboot == NULL || !service->config.reboot(service->config.context))
            {
                write_output(service, REBOOT_UNSUPPORTED);
            }
        }

        else if (strcmp(service->line, INITIALIZE_CONFIRM_VALUE) == 0)
        {
            write_output(service, INITIALIZE_FAILED);
        }

        show_recovery_menu(service);
    }

    else if (service->state == TERMINAL_STATE_RECOVERY_MENU)
    {
        write_output(service, INVALID_SELECTION);
        show_recovery_menu(service);
    }

    else if (service->state == TERMINAL_STATE_DIAGNOSTICS && strcmp(service->line, DIAGNOSTICS_EXIT) == 0)
    {
        show_main_menu(service);
    }

    else
    {
        write_output(service, INVALID_SELECTION);
    }

    memset(&settings, 0, sizeof(settings));
}

/* Initializes a disconnected, bounded terminal service over an abstract transport. */
void terminal_service_init(terminal_service_t *service, const terminal_config_t *config)
{
    memset(service, 0, sizeof(*service));
    service->config = *config;
    service->state  = TERMINAL_STATE_UNAVAILABLE;
}

/* Starts a fresh unauthenticated session and displays setup or login. */
void terminal_service_connect(terminal_service_t *service, uint64_t now_ms)
{
    clear_sensitive(service);
    service->is_connected     = true;
    service->last_activity_ms = now_ms;
    start_authentication(service);
}

/* Clears all sensitive and authenticated state after transport loss. */
void terminal_service_disconnect(terminal_service_t *service, terminal_disconnect_reason_t reason)
{
    clear_sensitive(service);
    service->is_connected                = false;
    service->authenticated_session_count = 0;
    service->is_password_input           = false;
    service->state                       = TERMINAL_STATE_UNAVAILABLE;
    service->last_disconnect_reason      = reason;
}

/* Consumes bounded ASCII transport bytes without blocking the caller. */
void terminal_service_receive(terminal_service_t *service, const uint8_t *data, size_t size, uint64_t now_ms)
{
    if (!service->is_connected || data == NULL)
    {
        return;
    }

    service->last_activity_ms = now_ms;

    for (size_t index = 0; index < size; index++)
    {
        const uint8_t character = data[index];

        if (character == '\n' && service->is_carriage_return_pending)
        {
            /* Treat a CRLF pair as one submission even when transport reads split the two bytes. */
            service->is_carriage_return_pending = false;
            continue;
        }

        service->is_carriage_return_pending = character == '\r';

        if (character == '\r' || character == '\n')
        {
            if (service->is_line_rejected)
            {
                write_output(service, INVALID_INPUT);
                service->is_line_rejected = false;
            }

            else if (service->line_size > 0 || service->state == TERMINAL_STATE_SETUP_USERNAME ||
                     service->state == TERMINAL_STATE_SETUP_PASSWORD || service->state == TERMINAL_STATE_EDIT_CREDENTIAL_NAME ||
                     service->state == TERMINAL_STATE_EDIT_CREDENTIAL_SECRET)
            {
                service->line[service->line_size] = '\0';

                if (!service->is_password_input)
                {
                    write_output(service, LINE_ENDING);
                }

                handle_line(service, now_ms);
            }

            else
            {
                /* A blank line is a portable reconnect/redraw request for terminals without connection events. */
                redraw_current_view(service);
            }

            service->line_size = 0;
        }

        else if (character == '\b' || character == 127U)
        {
            if (service->line_size > 0)
            {
                service->line_size--;

                if (!service->is_password_input)
                {
                    /* Keep the visible terminal line synchronized with the edited bounded input buffer. */
                    write_output(service, ERASE_CHARACTER);
                }
            }
        }

        else if (character < 32U || character > 126U || service->line_size + 1 >= sizeof(service->line))
        {
            service->is_line_rejected = true;
        }

        else if (!service->is_line_rejected)
        {
            service->line[service->line_size++] = (char)character;

            if (!service->is_password_input)
            {
                char echo[2] = {(char)character, '\0'};
                write_output(service, echo);
            }
        }
    }
}

/* Applies idle timeout policy and clears an expired session. */
void terminal_service_process(terminal_service_t *service, uint64_t now_ms)
{
    if (service->is_connected && service->config.idle_timeout_ms > 0 &&
        now_ms - service->last_activity_ms >= service->config.idle_timeout_ms)
    {
        terminal_service_disconnect(service, TERMINAL_DISCONNECT_IDLE_TIMEOUT);
    }
}

/* Forwards one diagnostic record only while the authenticated session selected that mode. */
void terminal_service_emit_diagnostic(terminal_service_t *service, const char *record)
{
    if (service->is_connected && service->state == TERMINAL_STATE_DIAGNOSTICS)
    {
        write_output(service, record);
        write_output(service, LINE_ENDING);
    }
}

/* Gets a redacted point-in-time terminal health snapshot. */
terminal_health_t terminal_service_get_health(const terminal_service_t *service)
{
    return (terminal_health_t){.state                       = service->state,
                               .authenticated_session_count = service->authenticated_session_count,
                               .failed_login_count          = service->failed_login_count,
                               .output_drop_count           = service->output_drop_count,
                               .last_disconnect_reason      = service->last_disconnect_reason};
}

/* Gets the stable terminal-state diagnostic name. */
const char *terminal_get_state_name(terminal_state_t state)
{
    static const char *const names[] = {"unavailable",
                                        "setup_username",
                                        "setup_password",
                                        "login_username",
                                        "login_password",
                                        "main_menu",
                                        "settings_menu",
                                        "diagnostics",
                                        "confirm_reset",
                                        "confirm_reboot",
                                        "edit_credential_name",
                                        "edit_credential_secret",
                                        "confirm_credential",
                                        "edit_hostname",
                                        "confirm_hostname",
                                        "recovery_menu",
                                        "recovery_confirm_initialize",
                                        "recovery_confirm_reboot",
                                        "mqtt_menu",
                                        "edit_mqtt_host",
                                        "edit_mqtt_port",
                                        "edit_mqtt_client_id",
                                        "rs485_menu",
                                        "edit_rs485_address",
                                        "edit_rs485_baud_rate",
                                        "edit_protocol_key"};

    return state <= TERMINAL_STATE_EDIT_RS485_BAUD_RATE ? names[state] : "unknown";
}
