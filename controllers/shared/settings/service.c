#include "settings/service.h"

#include <string.h>

/* Persistent format constants define the versioned controller-owned record contract. */
enum
{
    SETTINGS_FORMAT_VERSION      = 1,
    SETTINGS_SCHEMA_VERSION      = 1,
    BOOTSTRAP_STATE_INITIALIZING = 1,
    BOOTSTRAP_STATE_READY        = 2,
};

static const uint8_t BOOTSTRAP_MAGIC[8] = {'F', 'C', 'S', 'E', 'T', '0', '1', '\0'};
static const uint8_t SETTINGS_MAGIC[8]  = {'F', 'C', 'V', 'A', 'L', '0', '1', '\0'};

typedef struct
{
    uint8_t magic[8];
    uint32_t format_version;
    uint32_t schema_version;
    uint32_t generation;
    uint32_t state;
    uint32_t integrity;
} settings_bootstrap_record_t;

typedef struct
{
    uint8_t magic[8];
    uint32_t schema_version;
    uint32_t generation;
    controller_settings_t settings;
    uint32_t integrity;
} settings_value_record_t;

/* Calculates the portable integrity check used to detect torn or corrupt records. */
static uint32_t get_integrity(const void *data, size_t size)
{
    const uint8_t *bytes = data;
    uint32_t value       = UINT32_C(2166136261);

    for (size_t index = 0; index < size; index++)
    {
        value ^= bytes[index];
        value *= UINT32_C(16777619);
    }

    return value;
}

/* Tests a record integrity field after excluding the field itself from the check. */
static bool is_integrity_valid(const void *record, size_t checked_size, uint32_t expected)
{
    return get_integrity(record, checked_size) == expected;
}

/* Builds bootstrap metadata with its integrity field written last in memory. */
static settings_bootstrap_record_t get_bootstrap_record(uint32_t generation, uint32_t state)
{
    settings_bootstrap_record_t record = {0};
    memcpy(record.magic, BOOTSTRAP_MAGIC, sizeof(record.magic));
    record.format_version = SETTINGS_FORMAT_VERSION;
    record.schema_version = SETTINGS_SCHEMA_VERSION;
    record.generation     = generation;
    record.state          = state;
    record.integrity      = get_integrity(&record, offsetof(settings_bootstrap_record_t, integrity));

    return record;
}

/* Builds the single atomic typed-value record for a generation. */
static settings_value_record_t get_value_record(const controller_settings_t *settings, uint32_t generation)
{
    settings_value_record_t record = {0};
    memcpy(record.magic, SETTINGS_MAGIC, sizeof(record.magic));
    record.schema_version = SETTINGS_SCHEMA_VERSION;
    record.generation     = generation;
    record.settings       = *settings;
    record.integrity      = get_integrity(&record, offsetof(settings_value_record_t, integrity));

    return record;
}

/* Tests whether required store callbacks are available before accessing media. */
static bool is_store_valid(const settings_store_t *store)
{
    return store != NULL && store->get_bootstrap != NULL && store->stage_bootstrap != NULL && store->get_settings != NULL &&
           store->stage_settings != NULL && store->commit != NULL && store->abort != NULL;
}

/* Maps a failed media read to the externally observable storage state. */
static settings_storage_state_t get_read_failure_state(settings_store_result_t result)
{
    if (result == SETTINGS_STORE_UNAVAILABLE)
    {
        return SETTINGS_STORAGE_UNAVAILABLE;
    }

    if (result == SETTINGS_STORE_INCOMPATIBLE)
    {
        return SETTINGS_STORAGE_INCOMPATIBLE;
    }

    return SETTINGS_STORAGE_CORRUPT;
}

/* Writes and verifies initialization records, publishing ready metadata last. */
static settings_storage_state_t initialize_storage(settings_service_t *service, const settings_defaults_t *defaults)
{
    const settings_bootstrap_record_t initializing = get_bootstrap_record(1, BOOTSTRAP_STATE_INITIALIZING);

    if (service->store.stage_bootstrap(service->store.context, &initializing, sizeof(initializing)) != SETTINGS_STORE_OK ||
        service->store.commit(service->store.context) != SETTINGS_STORE_OK)
    {
        service->store.abort(service->store.context);

        return SETTINGS_STORAGE_UNAVAILABLE;
    }

    const settings_value_record_t values    = get_value_record(defaults, initializing.generation);
    const settings_bootstrap_record_t ready = get_bootstrap_record(initializing.generation, BOOTSTRAP_STATE_READY);

    /* Values and the ready marker share one commit, so partial defaults never become consumable. */
    if (service->store.stage_settings(service->store.context, &values, sizeof(values)) != SETTINGS_STORE_OK ||
        service->store.stage_bootstrap(service->store.context, &ready, sizeof(ready)) != SETTINGS_STORE_OK ||
        service->store.commit(service->store.context) != SETTINGS_STORE_OK)
    {
        service->store.abort(service->store.context);

        return SETTINGS_STORAGE_INITIALIZATION_INTERRUPTED;
    }

    service->snapshot       = *defaults;
    service->generation     = ready.generation;
    service->schema_version = ready.schema_version;

    return SETTINGS_STORAGE_READY;
}

/* Initializes or recovers settings without treating suspect media as blank. */
settings_storage_state_t settings_service_initialize(settings_service_t *service, const settings_store_t *store,
                                                     const settings_defaults_t *defaults)
{
    memset(service, 0, sizeof(*service));
    service->state = SETTINGS_STORAGE_UNAVAILABLE;

    if (!is_store_valid(store) || defaults == NULL)
    {
        return service->state;
    }

    service->store                        = *store;
    service->defaults                     = *defaults;
    settings_bootstrap_record_t bootstrap = {0};
    size_t bootstrap_size                 = 0;
    const settings_store_result_t bootstrap_result =
        store->get_bootstrap(store->context, &bootstrap, sizeof(bootstrap), &bootstrap_size);

    if (bootstrap_result == SETTINGS_STORE_MISSING)
    {
        service->state = initialize_storage(service, defaults);

        return service->state;
    }

    if (bootstrap_result != SETTINGS_STORE_OK || bootstrap_size != sizeof(bootstrap))
    {
        service->state = get_read_failure_state(bootstrap_result);

        return service->state;
    }

    if (memcmp(bootstrap.magic, BOOTSTRAP_MAGIC, sizeof(bootstrap.magic)) != 0)
    {
        service->state = SETTINGS_STORAGE_FOREIGN;

        return service->state;
    }

    if (!is_integrity_valid(&bootstrap, offsetof(settings_bootstrap_record_t, integrity), bootstrap.integrity))
    {
        service->state = SETTINGS_STORAGE_CORRUPT;

        return service->state;
    }

    if (bootstrap.format_version != SETTINGS_FORMAT_VERSION || bootstrap.schema_version != SETTINGS_SCHEMA_VERSION)
    {
        service->state = SETTINGS_STORAGE_INCOMPATIBLE;

        return service->state;
    }

    if (bootstrap.state == BOOTSTRAP_STATE_INITIALIZING)
    {
        /* Restart seeding from defaults because an initializing generation was never exposed. */
        service->state = initialize_storage(service, defaults);

        return service->state;
    }

    if (bootstrap.state != BOOTSTRAP_STATE_READY)
    {
        service->state = SETTINGS_STORAGE_CORRUPT;

        return service->state;
    }

    settings_value_record_t values              = {0};
    size_t values_size                          = 0;
    const settings_store_result_t values_result = store->get_settings(store->context, &values, sizeof(values), &values_size);

    if (values_result != SETTINGS_STORE_OK || values_size != sizeof(values) ||
        memcmp(values.magic, SETTINGS_MAGIC, sizeof(values.magic)) != 0 ||
        !is_integrity_valid(&values, offsetof(settings_value_record_t, integrity), values.integrity))
    {
        service->state = get_read_failure_state(values_result);

        return service->state;
    }

    if (values.schema_version != bootstrap.schema_version || values.generation != bootstrap.generation)
    {
        service->state = SETTINGS_STORAGE_CORRUPT;

        return service->state;
    }

    service->snapshot       = values.settings;
    service->generation     = values.generation;
    service->schema_version = values.schema_version;
    service->state          = SETTINGS_STORAGE_READY;

    return service->state;
}

/* Gets a copy of the current typed settings snapshot without exposing the store. */
controller_settings_t settings_service_get_snapshot(const settings_service_t *service)
{
    return service->snapshot;
}

/* Atomically replaces the complete typed settings snapshot. */
settings_store_result_t settings_service_commit(settings_service_t *service, const controller_settings_t *settings)
{
    if (service->state != SETTINGS_STORAGE_READY || settings == NULL)
    {
        return SETTINGS_STORE_UNAVAILABLE;
    }

    const uint32_t generation               = service->generation + 1;
    const settings_value_record_t values    = get_value_record(settings, generation);
    const settings_bootstrap_record_t ready = get_bootstrap_record(generation, BOOTSTRAP_STATE_READY);

    if (service->store.stage_settings(service->store.context, &values, sizeof(values)) != SETTINGS_STORE_OK ||
        service->store.stage_bootstrap(service->store.context, &ready, sizeof(ready)) != SETTINGS_STORE_OK)
    {
        service->store.abort(service->store.context);

        return SETTINGS_STORE_IO_ERROR;
    }

    const settings_store_result_t result = service->store.commit(service->store.context);

    if (result != SETTINGS_STORE_OK)
    {
        service->store.abort(service->store.context);

        return result;
    }

    service->snapshot   = *settings;
    service->generation = generation;

    return SETTINGS_STORE_OK;
}

/* Atomically commits a ready blank generation that must never be reseeded from build defaults. */
settings_store_result_t settings_service_reset(settings_service_t *service)
{
    controller_settings_t reset = {
        .hostname = service->defaults.hostname, .rs485 = service->defaults.rs485, .is_user_reset = true};

    return settings_service_commit(service, &reset);
}

/* Gets the stable diagnostic name for a settings storage state. */
const char *settings_get_storage_state_name(settings_storage_state_t state)
{
    static const char *const names[] = {"unavailable", "uninitialized", "initialization_interrupted", "ready", "incompatible",
                                        "corrupt",     "foreign"};

    return state <= SETTINGS_STORAGE_FOREIGN ? names[state] : "unknown";
}
