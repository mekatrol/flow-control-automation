#include <assert.h>
#include <stdio.h>
#include <string.h>

#include "terminal_service.h"

/* Test buffers remain larger than one complete stable menu exchange. */
enum
{
    TEST_OUTPUT_CAPACITY  = 4096,
    TEST_IDLE_TIMEOUT_MS  = 1000,
    TEST_LOGIN_BACKOFF_MS = 100,
};

typedef struct
{
    char output[TEST_OUTPUT_CAPACITY];
    size_t output_size;
    bool is_reboot_requested;
    bool is_storage_initialized;
} terminal_fixture_t;

static const char TEST_USERNAME[]        = "operator";
static const char TEST_PASSWORD[]        = "private-password";
static const char TEST_SUCCESS_MESSAGE[] = "Terminal service tests passed";

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
    (void)snprintf(settings->snapshot.terminal_username.value, sizeof(settings->snapshot.terminal_username.value), "%s",
                   TEST_USERNAME);
    (void)snprintf(settings->snapshot.terminal_password.value, sizeof(settings->snapshot.terminal_password.value), "%s",
                   TEST_PASSWORD);
    const terminal_config_t config = {.settings         = settings,
                                      .write            = write_fixture,
                                      .get_system_info  = get_fixture_system_info,
                                      .reboot           = reboot_fixture,
                                      .context          = fixture,
                                      .idle_timeout_ms  = TEST_IDLE_TIMEOUT_MS,
                                      .login_backoff_ms = TEST_LOGIN_BACKOFF_MS};
    terminal_service_t service;
    terminal_service_init(&service, &config);
    terminal_service_connect(&service, 0);
    return service;
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
    puts(TEST_SUCCESS_MESSAGE);
    return 0;
}
