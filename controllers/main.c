#include "board.h"
#include "controller_runtime.h"
#include "diagnostics.h"
#include "platform.h"

void controller_main(void)
{
    platform_startup_info_t startup;
    char configuration[128];
    platform_get_startup_info(&startup);
    controller_board_format_configuration(configuration, sizeof(configuration));

    diagnostics_emit(DIAGNOSTIC_INFO, "startup", "banner",
                     "firmware=%s version=%s board=%s reset_reason=%s",
                     startup.firmware_name, startup.firmware_version,
                     controller_board_name(), startup.reset_reason);
    diagnostics_emit(DIAGNOSTIC_INFO, "startup", "processor",
                     "model=%s cores=%u revision=%u.%u",
                     startup.processor, startup.processor_cores,
                     startup.silicon_revision_major,
                     startup.silicon_revision_minor);
    diagnostics_emit(DIAGNOSTIC_INFO, "startup", "memory",
                     "flash_bytes=%llu external_ram_bytes=%llu",
                     (unsigned long long)startup.flash_bytes,
                     (unsigned long long)startup.external_ram_bytes);
    diagnostics_emit(DIAGNOSTIC_INFO, "startup", "configuration", "%s",
                     configuration);

    if (!controller_runtime_start()) {
        diagnostics_emit(DIAGNOSTIC_ERROR, "runtime", "start_failed",
                         "controller task could not be created");
        return;
    }
    diagnostics_emit(DIAGNOSTIC_INFO, "runtime", "started",
                     "controller task active; platform entry returning");
}
