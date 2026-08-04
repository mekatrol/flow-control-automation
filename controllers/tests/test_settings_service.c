#include <assert.h>
#include <stdio.h>
#include <string.h>

#include "settings_service.h"

/* Fake limits exceed the current records while remaining bounded in tests. */
enum
{
    FAKE_RECORD_CAPACITY = 2048,
};

typedef struct
{
    uint8_t bootstrap[FAKE_RECORD_CAPACITY];
    size_t bootstrap_size;
    uint8_t settings[FAKE_RECORD_CAPACITY];
    size_t settings_size;
    uint8_t staged_bootstrap[FAKE_RECORD_CAPACITY];
    size_t staged_bootstrap_size;
    uint8_t staged_settings[FAKE_RECORD_CAPACITY];
    size_t staged_settings_size;
    bool is_available;
    bool is_commit_failing;
} fake_store_t;

/* Reads one fake record with the same missing and unavailable distinctions as media. */
static settings_store_result_t get_fake_record(const uint8_t *source, size_t source_size, bool is_available, void *record,
                                               size_t capacity, size_t *size)
{
    if (!is_available)
    {
        return SETTINGS_STORE_UNAVAILABLE;
    }
    if (source_size == 0)
    {
        return SETTINGS_STORE_MISSING;
    }
    if (source_size > capacity)
    {
        return SETTINGS_STORE_CORRUPT;
    }
    memcpy(record, source, source_size);
    *size = source_size;
    return SETTINGS_STORE_OK;
}

/* Reads fake bootstrap metadata. */
static settings_store_result_t get_fake_bootstrap(void *context, void *record, size_t capacity, size_t *size)
{
    fake_store_t *fake = context;
    return get_fake_record(fake->bootstrap, fake->bootstrap_size, fake->is_available, record, capacity, size);
}

/* Reads the fake typed settings record. */
static settings_store_result_t get_fake_settings(void *context, void *record, size_t capacity, size_t *size)
{
    fake_store_t *fake = context;
    return get_fake_record(fake->settings, fake->settings_size, fake->is_available, record, capacity, size);
}

/* Stages fake bootstrap metadata until commit. */
static settings_store_result_t stage_fake_bootstrap(void *context, const void *record, size_t size)
{
    fake_store_t *fake = context;
    if (!fake->is_available || size > sizeof(fake->staged_bootstrap))
    {
        return SETTINGS_STORE_UNAVAILABLE;
    }
    memcpy(fake->staged_bootstrap, record, size);
    fake->staged_bootstrap_size = size;
    return SETTINGS_STORE_OK;
}

/* Stages a fake typed settings record until commit. */
static settings_store_result_t stage_fake_settings(void *context, const void *record, size_t size)
{
    fake_store_t *fake = context;
    if (!fake->is_available || size > sizeof(fake->staged_settings))
    {
        return SETTINGS_STORE_UNAVAILABLE;
    }
    memcpy(fake->staged_settings, record, size);
    fake->staged_settings_size = size;
    return SETTINGS_STORE_OK;
}

/* Atomically publishes both staged fake records or injects a total failure. */
static settings_store_result_t commit_fake(void *context)
{
    fake_store_t *fake = context;
    if (fake->is_commit_failing)
    {
        return SETTINGS_STORE_IO_ERROR;
    }
    if (fake->staged_bootstrap_size > 0)
    {
        memcpy(fake->bootstrap, fake->staged_bootstrap, fake->staged_bootstrap_size);
        fake->bootstrap_size = fake->staged_bootstrap_size;
    }
    if (fake->staged_settings_size > 0)
    {
        memcpy(fake->settings, fake->staged_settings, fake->staged_settings_size);
        fake->settings_size = fake->staged_settings_size;
    }
    fake->staged_bootstrap_size = 0;
    fake->staged_settings_size  = 0;
    return SETTINGS_STORE_OK;
}

/* Discards staged fake records to model transaction rollback. */
static void abort_fake(void *context)
{
    fake_store_t *fake          = context;
    fake->staged_bootstrap_size = 0;
    fake->staged_settings_size  = 0;
}

/* Builds the store contract used by each isolated test. */
static settings_store_t get_fake_store(fake_store_t *fake)
{
    return (settings_store_t){.get_bootstrap   = get_fake_bootstrap,
                              .stage_bootstrap = stage_fake_bootstrap,
                              .get_settings    = get_fake_settings,
                              .stage_settings  = stage_fake_settings,
                              .commit          = commit_fake,
                              .abort           = abort_fake,
                              .context         = fake};
}

/* Assigns a nullable test value while preserving null versus empty. */
static settings_nullable_string_t get_nullable(bool is_set, const char *value)
{
    settings_nullable_string_t result = {.is_set = is_set};
    if (value != NULL)
    {
        (void)snprintf(result.value, sizeof(result.value), "%s", value);
    }
    return result;
}

/* Verifies first initialization preserves null, empty, and non-empty defaults. */
static void test_initialization_preserves_value_meaning(void)
{
    fake_store_t fake                  = {.is_available = true};
    const settings_store_t store       = get_fake_store(&fake);
    const settings_defaults_t defaults = {
        .wifi_ssid         = get_nullable(false, NULL),
        .wifi_password     = get_nullable(true, ""),
        .terminal_username = get_nullable(true, "operator"),
        .terminal_password = get_nullable(false, NULL),
    };
    settings_service_t service;
    assert(settings_service_initialize(&service, &store, &defaults) == SETTINGS_STORAGE_READY);
    const controller_settings_t snapshot = settings_service_get_snapshot(&service);
    assert(!snapshot.wifi_ssid.is_set);
    assert(snapshot.wifi_password.is_set && snapshot.wifi_password.value[0] == '\0');
    assert(snapshot.terminal_username.is_set && strcmp(snapshot.terminal_username.value, "operator") == 0);
    assert(!snapshot.terminal_password.is_set);
}

/* Verifies later boots use persisted values instead of replacement defaults. */
static void test_persisted_settings_win_after_reflash(void)
{
    fake_store_t fake                  = {.is_available = true};
    const settings_store_t store       = get_fake_store(&fake);
    const settings_defaults_t original = {.terminal_username = get_nullable(true, "original")};
    settings_service_t first;
    assert(settings_service_initialize(&first, &store, &original) == SETTINGS_STORAGE_READY);
    controller_settings_t changed = settings_service_get_snapshot(&first);
    changed.terminal_username     = get_nullable(true, "persisted");
    assert(settings_service_commit(&first, &changed) == SETTINGS_STORE_OK);

    const settings_defaults_t reflashed = {.terminal_username = get_nullable(true, "replacement")};
    settings_service_t second;
    assert(settings_service_initialize(&second, &store, &reflashed) == SETTINGS_STORAGE_READY);
    assert(strcmp(settings_service_get_snapshot(&second).terminal_username.value, "persisted") == 0);
}

/* Verifies failed commits expose the previous complete credential pair. */
static void test_failed_atomic_update_keeps_previous_pair(void)
{
    fake_store_t fake                  = {.is_available = true};
    const settings_store_t store       = get_fake_store(&fake);
    const settings_defaults_t defaults = {.mqtt_username = get_nullable(true, "old-user"),
                                          .mqtt_password = get_nullable(true, "old-password")};
    settings_service_t service;
    assert(settings_service_initialize(&service, &store, &defaults) == SETTINGS_STORAGE_READY);
    controller_settings_t replacement = settings_service_get_snapshot(&service);
    replacement.mqtt_username         = get_nullable(true, "new-user");
    replacement.mqtt_password         = get_nullable(true, "new-password");
    fake.is_commit_failing            = true;
    assert(settings_service_commit(&service, &replacement) == SETTINGS_STORE_IO_ERROR);
    fake.is_commit_failing = false;

    settings_service_t recovered;
    assert(settings_service_initialize(&recovered, &store, &defaults) == SETTINGS_STORAGE_READY);
    const controller_settings_t snapshot = settings_service_get_snapshot(&recovered);
    assert(strcmp(snapshot.mqtt_username.value, "old-user") == 0);
    assert(strcmp(snapshot.mqtt_password.value, "old-password") == 0);
}

/* Verifies unavailable and foreign media are never initialized automatically. */
static void test_suspect_media_is_not_formatted(void)
{
    const settings_defaults_t defaults = {0};
    fake_store_t unavailable           = {0};
    settings_store_t store             = get_fake_store(&unavailable);
    settings_service_t service;
    assert(settings_service_initialize(&service, &store, &defaults) == SETTINGS_STORAGE_UNAVAILABLE);

    fake_store_t foreign = {.is_available = true};
    store                = get_fake_store(&foreign);
    settings_service_t seeded;
    assert(settings_service_initialize(&seeded, &store, &defaults) == SETTINGS_STORAGE_READY);
    const size_t settings_size_before_foreign_boot = foreign.settings_size;
    memcpy(foreign.bootstrap, "NOTOURS", sizeof("NOTOURS") - 1);
    assert(settings_service_initialize(&service, &store, &defaults) == SETTINGS_STORAGE_FOREIGN);
    assert(foreign.settings_size == settings_size_before_foreign_boot);
}

/* Verifies integrity corruption is reported instead of reseeding defaults. */
static void test_corrupt_ready_record_is_rejected(void)
{
    fake_store_t fake                  = {.is_available = true};
    const settings_store_t store       = get_fake_store(&fake);
    const settings_defaults_t defaults = {0};
    settings_service_t initialized;
    assert(settings_service_initialize(&initialized, &store, &defaults) == SETTINGS_STORAGE_READY);
    fake.bootstrap[fake.bootstrap_size - 1] ^= 1;
    settings_service_t recovered;
    assert(settings_service_initialize(&recovered, &store, &defaults) == SETTINGS_STORAGE_CORRUPT);
}

/* Verifies reset persists a ready blank origin that later build defaults cannot reseed. */
static void test_reset_remains_blank_after_reflash(void)
{
    fake_store_t fake                  = {.is_available = true};
    const settings_store_t store       = get_fake_store(&fake);
    const settings_defaults_t defaults = {.terminal_username = get_nullable(true, "seed-user"),
                                          .terminal_password = get_nullable(true, "seed-password"),
                                          .hostname          = get_nullable(true, "flow-controller")};
    settings_service_t service;
    assert(settings_service_initialize(&service, &store, &defaults) == SETTINGS_STORAGE_READY);
    assert(settings_service_reset(&service) == SETTINGS_STORE_OK);
    const controller_settings_t reset = settings_service_get_snapshot(&service);
    assert(reset.is_user_reset);
    assert(!reset.terminal_username.is_set && !reset.terminal_password.is_set);
    assert(reset.hostname.is_set && strcmp(reset.hostname.value, "flow-controller") == 0);

    settings_service_t reflashed;
    assert(settings_service_initialize(&reflashed, &store, &defaults) == SETTINGS_STORAGE_READY);
    const controller_settings_t recovered = settings_service_get_snapshot(&reflashed);
    assert(recovered.is_user_reset);
    assert(!recovered.terminal_username.is_set && !recovered.terminal_password.is_set);
    assert(recovered.hostname.is_set && strcmp(recovered.hostname.value, "flow-controller") == 0);
}

/* Runs settings contract and recovery tests. */
int main(void)
{
    test_initialization_preserves_value_meaning();
    test_persisted_settings_win_after_reflash();
    test_failed_atomic_update_keeps_previous_pair();
    test_suspect_media_is_not_formatted();
    test_corrupt_ready_record_is_rejected();
    test_reset_remains_blank_after_reflash();
    puts("settings service tests passed");
    return 0;
}
