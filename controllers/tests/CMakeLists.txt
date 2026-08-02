cmake_minimum_required(VERSION 3.16)
project(controller_host_tests C)

enable_testing()
add_executable(controller_host_tests
    test_diagnostics_and_health.c
    ../shared/diagnostics_core.c
    ../shared/controller_health_format.c)
target_include_directories(controller_host_tests PRIVATE ../shared)
target_compile_options(controller_host_tests PRIVATE -Wall -Wextra -Werror -pedantic)
add_test(NAME shared_diagnostics_and_health COMMAND controller_host_tests)

add_executable(network_manager_tests
    test_network_manager.c
    ../shared/network_manager.c)
target_include_directories(network_manager_tests PRIVATE ../shared)
target_compile_options(network_manager_tests PRIVATE -Wall -Wextra -Werror -pedantic)
add_test(NAME network_manager COMMAND network_manager_tests)
