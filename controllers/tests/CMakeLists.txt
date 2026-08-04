cmake_minimum_required(VERSION 3.16)
project(controller_host_tests C)

enable_testing()

# Apply the same source-policy gate to host builds so local validation matches firmware builds.
add_custom_target(check_source_policy ALL
    COMMAND "${CMAKE_CURRENT_LIST_DIR}/../scripts/check-source-policy.sh" "${CMAKE_CURRENT_LIST_DIR}/.."
    COMMENT "Checking controller source policies")

add_executable(diagnostics_core_tests
    test_diagnostics_and_health.c
    ../shared/diagnostics_core.c)
target_include_directories(diagnostics_core_tests PRIVATE ../shared)
target_compile_options(diagnostics_core_tests PRIVATE -Wall -Wextra -Werror -pedantic)
add_test(NAME diagnostics_core COMMAND diagnostics_core_tests)

add_executable(controller_health_format_tests
    test_controller_health_format.c
    ../shared/controller_health_format.c)
target_include_directories(controller_health_format_tests PRIVATE ../shared)
target_compile_options(controller_health_format_tests PRIVATE -Wall -Wextra -Werror -pedantic)
add_test(NAME controller_health_format COMMAND controller_health_format_tests)

add_executable(network_manager_tests
    test_network_manager.c
    ../shared/network_manager.c)
target_include_directories(network_manager_tests PRIVATE ../shared)
target_compile_options(network_manager_tests PRIVATE -Wall -Wextra -Werror -pedantic)
add_test(NAME network_manager COMMAND network_manager_tests)

add_executable(wifi_link_core_tests
    test_wifi_link_core.c
    ../shared/wifi_link_core.c)
target_include_directories(wifi_link_core_tests PRIVATE ../shared)
target_compile_options(wifi_link_core_tests PRIVATE -Wall -Wextra -Werror -pedantic)
add_test(NAME wifi_link_core COMMAND wifi_link_core_tests)

add_executable(ethernet_link_core_tests
    test_ethernet_link_core.c
    ../shared/ethernet_link_core.c)
target_include_directories(ethernet_link_core_tests PRIVATE ../shared)
target_compile_options(ethernet_link_core_tests PRIVATE -Wall -Wextra -Werror -pedantic)
add_test(NAME ethernet_link_core COMMAND ethernet_link_core_tests)

add_executable(mqtt_service_tests
    test_mqtt_service.c
    ../shared/mqtt_service.c)
target_include_directories(mqtt_service_tests PRIVATE ../shared)
target_compile_options(mqtt_service_tests PRIVATE -Wall -Wextra -Werror -pedantic)
add_test(NAME mqtt_service COMMAND mqtt_service_tests)

add_executable(mqtt_api_tests
    test_mqtt_api.c
    ../shared/mqtt_api.c)
target_include_directories(mqtt_api_tests PRIVATE ../shared)
target_compile_options(mqtt_api_tests PRIVATE -Wall -Wextra -Werror -pedantic)
add_test(NAME mqtt_api COMMAND mqtt_api_tests)

add_executable(settings_service_tests
    test_settings_service.c
    ../shared/settings_service.c)
target_include_directories(settings_service_tests PRIVATE ../shared)
target_compile_options(settings_service_tests PRIVATE -Wall -Wextra -Werror -pedantic)
add_test(NAME settings_service COMMAND settings_service_tests)

add_executable(terminal_service_tests
    test_terminal_service.c
    ../shared/terminal_service.c
    ../shared/settings_service.c)
target_include_directories(terminal_service_tests PRIVATE ../shared)
target_compile_options(terminal_service_tests PRIVATE -Wall -Wextra -Werror -pedantic)
add_test(NAME terminal_service COMMAND terminal_service_tests)
