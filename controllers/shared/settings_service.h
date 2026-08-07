#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/* Bounded credential lengths prevent persistent or runtime allocation growth. */
enum
{
    SETTINGS_USERNAME_CAPACITY       = 65,
    SETTINGS_PASSWORD_CAPACITY       = 129,
    SETTINGS_WIFI_SSID_CAPACITY      = 33,
    SETTINGS_HOSTNAME_CAPACITY       = 64,
    SETTINGS_MQTT_HOST_CAPACITY      = 129,
    SETTINGS_MQTT_CLIENT_ID_CAPACITY = 65,
};

/* Store results preserve distinctions that are significant during recovery. */
typedef enum
{
    SETTINGS_STORE_OK,
    SETTINGS_STORE_UNAVAILABLE,
    SETTINGS_STORE_MISSING,
    SETTINGS_STORE_CORRUPT,
    SETTINGS_STORE_INCOMPATIBLE,
    SETTINGS_STORE_FULL,
    SETTINGS_STORE_WRITE_PROTECTED,
    SETTINGS_STORE_IO_ERROR,
} settings_store_result_t;

/* Bootstrap states distinguish safe initialization from media requiring attention. */
typedef enum
{
    SETTINGS_STORAGE_UNAVAILABLE,
    SETTINGS_STORAGE_UNINITIALIZED,
    SETTINGS_STORAGE_INITIALIZATION_INTERRUPTED,
    SETTINGS_STORAGE_READY,
    SETTINGS_STORAGE_INCOMPATIBLE,
    SETTINGS_STORAGE_CORRUPT,
    SETTINGS_STORAGE_FOREIGN,
} settings_storage_state_t;

/* A nullable string represents null independently from an explicitly empty value. */
typedef struct
{
    bool is_set;
    char value[SETTINGS_PASSWORD_CAPACITY];
} settings_nullable_string_t;

/* MQTT broker settings are persisted together with credentials for atomic reconfiguration. */
typedef struct
{
    bool enabled;
    bool is_tls_enabled;
    uint16_t port;
    char host[SETTINGS_MQTT_HOST_CAPACITY];
    char client_id[SETTINGS_MQTT_CLIENT_ID_CAPACITY];
} settings_mqtt_broker_t;

/* RS485 node settings are persisted together so address and line rate cannot be torn. */
typedef struct
{
    uint16_t address;
    uint32_t baud_rate;
} settings_rs485_t;

/* Typed settings are committed together so credential pairs cannot be torn. */
typedef struct
{
    settings_nullable_string_t wifi_ssid;
    settings_nullable_string_t wifi_password;
    settings_nullable_string_t terminal_username;
    settings_nullable_string_t terminal_password;
    settings_nullable_string_t mqtt_username;
    settings_nullable_string_t mqtt_password;
    settings_nullable_string_t hostname;
    settings_mqtt_broker_t mqtt_broker;
    settings_rs485_t rs485;
    bool is_user_reset;
} controller_settings_t;

/* Platform defaults retain Kconfig null versus explicitly empty semantics. */
typedef controller_settings_t settings_defaults_t;

/* Store operations stage and atomically publish one opaque versioned record. */
typedef struct
{
    settings_store_result_t (*get_bootstrap)(void *context, void *record, size_t capacity, size_t *size);
    settings_store_result_t (*stage_bootstrap)(void *context, const void *record, size_t size);
    settings_store_result_t (*get_settings)(void *context, void *record, size_t capacity, size_t *size);
    settings_store_result_t (*stage_settings)(void *context, const void *record, size_t size);
    settings_store_result_t (*commit)(void *context);
    void (*abort)(void *context);
    void *context;
} settings_store_t;

typedef struct
{
    settings_store_t store;
    settings_defaults_t defaults;
    controller_settings_t snapshot;
    settings_storage_state_t state;
    uint32_t generation;
    uint32_t schema_version;
} settings_service_t;

/* Initializes or recovers settings without treating suspect media as blank. */
settings_storage_state_t settings_service_initialize(settings_service_t *service, const settings_store_t *store,
                                                     const settings_defaults_t *defaults);

/* Gets a copy of the current typed settings snapshot without exposing the store. */
controller_settings_t settings_service_get_snapshot(const settings_service_t *service);

/* Atomically replaces the complete typed settings snapshot. */
settings_store_result_t settings_service_commit(settings_service_t *service, const controller_settings_t *settings);

/* Atomically commits a ready blank generation that must never be reseeded from build defaults. */
settings_store_result_t settings_service_reset(settings_service_t *service);

/* Gets the stable diagnostic name for a settings storage state. */
const char *settings_get_storage_state_name(settings_storage_state_t state);
