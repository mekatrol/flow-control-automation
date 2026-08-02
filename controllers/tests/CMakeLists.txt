cmake_minimum_required(VERSION 3.16)
project(controller_host_tests C)

enable_testing()
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
