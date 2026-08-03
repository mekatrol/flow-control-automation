cmake_minimum_required(VERSION 3.16)

set(CONTROLLER_BOARD "$ENV{CONTROLLER_BOARD}")
if(CONTROLLER_BOARD STREQUAL "")
    set(CONTROLLER_BOARD "kincony-kc868-a16")
endif()

if(CONTROLLER_BOARD STREQUAL "kincony-kc868-a16")
    set(CONTROLLER_BOARD_PATH "kincony/kc868-a16")
else()
    message(FATAL_ERROR "Unknown controller board: ${CONTROLLER_BOARD}")
endif()

set(SDKCONFIG_DEFAULTS
    "${CMAKE_CURRENT_LIST_DIR}/boards/${CONTROLLER_BOARD_PATH}/sdkconfig.defaults")

include($ENV{IDF_PATH}/tools/cmake/project.cmake)
project(flow_controller)

# Enforce repository source policies during every firmware build so violations cannot reach an image.
add_custom_target(check_source_policy ALL
    COMMAND "${CMAKE_CURRENT_LIST_DIR}/scripts/check-source-policy.sh" "${CMAKE_CURRENT_LIST_DIR}"
    COMMENT "Checking controller source policies")
