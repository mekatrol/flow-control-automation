#include "board/config.h"
#include "controller/runtime.h"
#include "diagnostics/service.h"
#include "platform/core.h"

/* Startup diagnostic components and event codes form the stable boot schema. */
static const char COMPONENT_STARTUP[]   = "startup";
static const char COMPONENT_RUNTIME[]   = "runtime";
static const char EVENT_BANNER[]        = "banner";
static const char EVENT_PROCESSOR[]     = "processor";
static const char EVENT_MEMORY[]        = "memory";
static const char EVENT_CONFIGURATION[] = "configuration";
static const char EVENT_START_FAILED[]  = "start_failed";
static const char EVENT_STARTED[]       = "started";

/* Startup message formats describe the bounded fields emitted at boot. */
static const char FORMAT_BANNER[]        = "firmware=%s version=%s board=%s reset_reason=%s";
static const char FORMAT_PROCESSOR[]     = "model=%s cores=%u revision=%u.%u";
static const char FORMAT_MEMORY[]        = "flash_bytes=%llu external_ram_bytes=%llu";
static const char FORMAT_TEXT[]          = "%s";
static const char MESSAGE_START_FAILED[] = "controller task could not be created";
static const char MESSAGE_STARTED[]      = "controller task active; platform entry returning";

/* Startup configuration output is bounded to keep stack use deterministic. */
enum
{
    STARTUP_CONFIGURATION_SIZE = 128
};

/* Emits startup diagnostics, starts the runtime, and returns without waiting. */
void controller_main(void)
{
    platform_startup_info_t startup;
    char configuration[STARTUP_CONFIGURATION_SIZE];

    /* Collect platform and board details before the task starts for one coherent banner. */
    platform_get_startup_info(&startup);
    controller_board_format_configuration(configuration, sizeof(configuration));

    diagnostics_emit(DIAGNOSTIC_INFO, COMPONENT_STARTUP, EVENT_BANNER, FORMAT_BANNER, startup.firmware_name,
                     startup.firmware_version, get_controller_board_name(), startup.reset_reason);
    diagnostics_emit(DIAGNOSTIC_INFO, COMPONENT_STARTUP, EVENT_PROCESSOR, FORMAT_PROCESSOR, startup.processor,
                     startup.processor_cores, startup.silicon_revision_major, startup.silicon_revision_minor);
    diagnostics_emit(DIAGNOSTIC_INFO, COMPONENT_STARTUP, EVENT_MEMORY, FORMAT_MEMORY, (unsigned long long)startup.flash_bytes,
                     (unsigned long long)startup.external_ram_bytes);
    diagnostics_emit(DIAGNOSTIC_INFO, COMPONENT_STARTUP, EVENT_CONFIGURATION, FORMAT_TEXT, configuration);

    if (!controller_runtime_start())
    {
        diagnostics_emit(DIAGNOSTIC_ERROR, COMPONENT_RUNTIME, EVENT_START_FAILED, MESSAGE_START_FAILED);
        return;
    }
    diagnostics_emit(DIAGNOSTIC_INFO, COMPONENT_RUNTIME, EVENT_STARTED, MESSAGE_STARTED);
}
