set(SHARED_DIR "${CMAKE_CURRENT_LIST_DIR}/../shared")
set(PLATFORM_DIR "${CMAKE_CURRENT_LIST_DIR}/../platforms/esp-idf")
set(BOARD_DIR "${CMAKE_CURRENT_LIST_DIR}/../boards/${CONTROLLER_BOARD_PATH}")

idf_component_register(
    SRCS
        "../main.c"
        "${SHARED_DIR}/controller_runtime.c"
        "${SHARED_DIR}/controller_health.c"
        "${SHARED_DIR}/controller_health_format.c"
        "${SHARED_DIR}/diagnostics.c"
        "${SHARED_DIR}/diagnostics_core.c"
        "${SHARED_DIR}/ethernet_link.c"
        "${SHARED_DIR}/ethernet_link_core.c"
        "${SHARED_DIR}/mqtt_service.c"
        "${SHARED_DIR}/network_manager.c"
        "${SHARED_DIR}/settings_service.c"
        "${SHARED_DIR}/terminal_service.c"
        "${SHARED_DIR}/wifi_link.c"
        "${SHARED_DIR}/wifi_link_core.c"
        "${PLATFORM_DIR}/app_main.c"
        "${PLATFORM_DIR}/platform.c"
        "${PLATFORM_DIR}/platform_ethernet.c"
        "${PLATFORM_DIR}/platform_mqtt.c"
        "${PLATFORM_DIR}/platform_settings.c"
        "${PLATFORM_DIR}/platform_terminal.c"
        "${PLATFORM_DIR}/platform_wifi.c"
        "${BOARD_DIR}/board.c"
    INCLUDE_DIRS
        "${SHARED_DIR}")
