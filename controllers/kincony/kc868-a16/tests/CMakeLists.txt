cmake_minimum_required(VERSION 3.16)
project(kc868_a16_host_tests C)

enable_testing()
add_executable(kc868_a16_host_tests
    test_phase1.c
    ../main/diagnostics_core.c
    ../main/controller_health_format.c)
target_include_directories(kc868_a16_host_tests PRIVATE ../main)
target_compile_options(kc868_a16_host_tests PRIVATE -Wall -Wextra -Werror -pedantic)
add_test(NAME phase1_diagnostics_and_health COMMAND kc868_a16_host_tests)
