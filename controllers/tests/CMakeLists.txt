cmake_minimum_required(VERSION 3.16)
project(controller_host_tests C)

enable_testing()

# Keep warnings strict while using the native option vocabulary of each supported host compiler frontend.
if(MSVC)
    add_compile_options(/W4 /WX /wd4456)
    add_compile_definitions(_CRT_SECURE_NO_WARNINGS)
else()
    add_compile_options(-Wall -Wextra -Werror -pedantic)
endif()

# Apply the same source-policy gate to host builds so local validation matches firmware builds.
if(CMAKE_HOST_WIN32)
    add_custom_target(check_source_policy ALL
        COMMAND powershell.exe -NoProfile -ExecutionPolicy Bypass -File
            "${CMAKE_CURRENT_LIST_DIR}/../scripts/check-source-policy.ps1" "${CMAKE_CURRENT_LIST_DIR}/.."
        COMMENT "Checking controller source policies")
else()
    add_custom_target(check_source_policy ALL
        COMMAND "${CMAKE_CURRENT_LIST_DIR}/../scripts/check-source-policy.sh" "${CMAKE_CURRENT_LIST_DIR}/.."
        COMMENT "Checking controller source policies")
endif()

add_executable(diagnostics_core_tests
    test_diagnostics_and_health.c
    ../shared/diagnostics/core.c)
target_include_directories(diagnostics_core_tests PRIVATE ../shared)
add_test(NAME diagnostics_core COMMAND diagnostics_core_tests)

add_executable(controller_health_format_tests
    test_controller_health_format.c
    ../shared/controller/health_format.c)
target_include_directories(controller_health_format_tests PRIVATE ../shared)
add_test(NAME controller_health_format COMMAND controller_health_format_tests)

add_executable(kahn_sort_tests
    test_kahn_sort.c
    ../shared/algorithm/kahn_sort.c)
target_include_directories(kahn_sort_tests PRIVATE ../shared)
add_test(NAME kahn_sort COMMAND kahn_sort_tests)

add_executable(controller_protocol_tests
    test_controller_protocol.c
    ../shared/algorithm/kahn_sort.c
    ../shared/controller/protocol.c
    ../shared/controller/auth.c
    ../shared/flow/service.c
    ../shared/flow/debug.c
    ../shared/flow/executable.c
    ../shared/flow/runtime.c
    ../shared/flow/sha256.c
    ../shared/controller/points.c)
target_include_directories(controller_protocol_tests PRIVATE ../shared)
add_test(NAME controller_protocol COMMAND controller_protocol_tests)

add_executable(controller_auth_tests
    test_controller_auth.c
    ../shared/controller/auth.c)
target_include_directories(controller_auth_tests PRIVATE ../shared)
add_test(NAME controller_auth COMMAND controller_auth_tests)

add_executable(controller_flow_tests
    test_controller_flow.c
    ../shared/flow/service.c)
target_include_directories(controller_flow_tests PRIVATE ../shared)
add_test(NAME controller_flow COMMAND controller_flow_tests)

add_executable(flow_contract_fixture_tests
    test_flow_contract_fixtures.c
    ../shared/algorithm/kahn_sort.c
    ../shared/flow/executable.c
    ../shared/flow/runtime.c
    ../shared/flow/sha256.c)
target_include_directories(flow_contract_fixture_tests PRIVATE ../shared)
target_compile_definitions(flow_contract_fixture_tests PRIVATE
    FLOW_CONTRACT_FIXTURE_DIRECTORY="${CMAKE_CURRENT_LIST_DIR}/../../testdata/contracts/flow-executable-v1")
add_test(NAME flow_contract_fixtures COMMAND flow_contract_fixture_tests)

add_executable(flow_debug_tests
    test_flow_debug.c
    ../shared/algorithm/kahn_sort.c
    ../shared/flow/debug.c
    ../shared/flow/executable.c
    ../shared/flow/runtime.c
    ../shared/flow/sha256.c)
target_include_directories(flow_debug_tests PRIVATE ../shared)
target_compile_definitions(flow_debug_tests PRIVATE
    FLOW_CONTRACT_FIXTURE_DIRECTORY="${CMAKE_CURRENT_LIST_DIR}/../../testdata/contracts/flow-executable-v1")
add_test(NAME flow_debug COMMAND flow_debug_tests)

add_executable(flow_resource_baseline_tests
    test_flow_resource_baseline.c)
target_include_directories(flow_resource_baseline_tests PRIVATE ../shared)
add_test(NAME flow_resource_baseline COMMAND flow_resource_baseline_tests)

add_executable(flow_il_v2_contract_tests
    test_flow_il_v2_contract.c
    ../shared/flow/sha256.c)
target_include_directories(flow_il_v2_contract_tests PRIVATE ../shared)
target_compile_definitions(flow_il_v2_contract_tests PRIVATE
    FLOW_IL_V2_FIXTURE_DIRECTORY="${CMAKE_CURRENT_LIST_DIR}/../../testdata/contracts/flow-il-v2")
add_test(NAME flow_il_v2_contract COMMAND flow_il_v2_contract_tests)

add_executable(controller_points_tests
    test_controller_points.c
    ../shared/controller/points.c)
target_include_directories(controller_points_tests PRIVATE ../shared)
add_test(NAME controller_points COMMAND controller_points_tests)

add_executable(controller_io_tests
    test_controller_io.c
    ../shared/controller/io.c)
target_include_directories(controller_io_tests PRIVATE ../shared)
add_test(NAME controller_io COMMAND controller_io_tests)

add_executable(network_manager_tests
    test_network_manager.c
    ../shared/network/manager.c)
target_include_directories(network_manager_tests PRIVATE ../shared)
add_test(NAME network_manager COMMAND network_manager_tests)

add_executable(wifi_link_core_tests
    test_wifi_link_core.c
    ../shared/wifi/core.c)
target_include_directories(wifi_link_core_tests PRIVATE ../shared)
add_test(NAME wifi_link_core COMMAND wifi_link_core_tests)

add_executable(ethernet_link_core_tests
    test_ethernet_link_core.c
    ../shared/ethernet/core.c)
target_include_directories(ethernet_link_core_tests PRIVATE ../shared)
add_test(NAME ethernet_link_core COMMAND ethernet_link_core_tests)

add_executable(mqtt_service_tests
    test_mqtt_service.c
    ../shared/mqtt/service.c)
target_include_directories(mqtt_service_tests PRIVATE ../shared)
add_test(NAME mqtt_service COMMAND mqtt_service_tests)

add_executable(mqtt_api_tests
    test_mqtt_api.c
    ../shared/mqtt/api.c)
target_include_directories(mqtt_api_tests PRIVATE ../shared)
add_test(NAME mqtt_api COMMAND mqtt_api_tests)

add_executable(rs485_service_tests
    test_rs485_service.c
    ../shared/rs485/service.c)
target_include_directories(rs485_service_tests PRIVATE ../shared)
add_test(NAME rs485_service COMMAND rs485_service_tests)

add_executable(settings_service_tests
    test_settings_service.c
    ../shared/settings/service.c)
target_include_directories(settings_service_tests PRIVATE ../shared)
add_test(NAME settings_service COMMAND settings_service_tests)

add_executable(terminal_service_tests
    test_terminal_service.c
    ../shared/terminal/service.c
    ../shared/settings/service.c)
target_include_directories(terminal_service_tests PRIVATE ../shared)
add_test(NAME terminal_service COMMAND terminal_service_tests)
