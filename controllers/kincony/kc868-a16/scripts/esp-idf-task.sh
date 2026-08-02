#!/usr/bin/env bash
set -euo pipefail

# ESP-IDF's Kconfig report may echo string values, including the Wi-Fi
# password. Keep task output free of credentials.
export KCONFIG_REPORT_VERBOSITY=quiet

if [ "$#" -lt 2 ]; then
  echo "usage: $0 <project-dir> <set-target|clean|build|flash|monitor> [serial-port]" >&2
  exit 2
fi

project_dir=$1
action=$2
serial_port=${3:-/dev/ttyACM0}
idf_python=
idf_py=

prepend_path() {
  if [ -d "$1" ]; then
    PATH="$1:$PATH"
  fi
}

configure_versioned_esp_idf() {
  local idf_directory
  local version_directory
  local tools_path
  local candidate

  for idf_directory in "$HOME"/.espressif/v*/esp-idf "$HOME"/esp/esp-idf; do
    if [ ! -f "$idf_directory/tools/idf.py" ]; then
      continue
    fi

    tools_path=${IDF_TOOLS_PATH:-"$HOME/.espressif/tools"}
    version_directory=$(basename "$(dirname "$idf_directory")")
    idf_python="$tools_path/python/$version_directory/venv/bin/python3"

    if [ ! -x "$idf_python" ]; then
      continue
    fi

    export IDF_PATH="$idf_directory"
    export IDF_TOOLS_PATH="$tools_path"
    export IDF_PYTHON_ENV_PATH="$tools_path/python/$version_directory/venv"
    export ESP_IDF_VERSION="${version_directory#v}"

    for candidate in \
      "$tools_path"/xtensa-esp-elf/*/xtensa-esp-elf/bin \
      "$tools_path"/cmake/*/bin \
      "$tools_path"/ninja/* \
      "$tools_path"/esp-clang/*/esp-clang/bin
    do
      prepend_path "$candidate"
    done

    idf_py="$idf_directory/tools/idf.py"
    export PATH
    return 0
  done

  return 1
}

find_esp_idf() {
  if command -v idf.py >/dev/null 2>&1; then
    return
  fi

  if configure_versioned_esp_idf; then
    return
  fi

  if [ -n "${ESP_IDF_EXPORT_SH:-}" ] && [ -f "$ESP_IDF_EXPORT_SH" ]; then
    . "$ESP_IDF_EXPORT_SH" >/dev/null
    return
  fi

  if [ -n "${IDF_PATH:-}" ] && [ -f "$IDF_PATH/export.sh" ]; then
    . "$IDF_PATH/export.sh" >/dev/null
    return
  fi

  echo "idf.py was not found." >&2
  echo "Set IDF_PATH, set ESP_IDF_EXPORT_SH, or configure the Espressif IDF extension." >&2
  exit 127
}

run_idf() {
  find_esp_idf
  if command -v idf.py >/dev/null 2>&1; then
    idf.py "$@"
  else
    "$idf_python" "$idf_py" "$@"
  fi
}

restore_wifi_configuration() {
  local sdkconfig_file=$1
  local saved_ssid=$2
  local saved_password=$3
  local temporary_file

  if [ -z "$saved_ssid" ] && [ -z "$saved_password" ]; then
    return
  fi

  temporary_file=$(mktemp "${sdkconfig_file}.credentials.XXXXXX")
  while IFS= read -r configuration_line; do
    case "$configuration_line" in
      CONFIG_KC868_A16_WIFI_SSID=*)
        if [ -n "$saved_ssid" ]; then
          printf '%s\n' "$saved_ssid"
        else
          printf '%s\n' "$configuration_line"
        fi
        ;;
      CONFIG_KC868_A16_WIFI_PASSWORD=*)
        if [ -n "$saved_password" ]; then
          printf '%s\n' "$saved_password"
        else
          printf '%s\n' "$configuration_line"
        fi
        ;;
      *) printf '%s\n' "$configuration_line" ;;
    esac
  done < "$sdkconfig_file" > "$temporary_file"
  mv "$temporary_file" "$sdkconfig_file"
}

cd "$project_dir"

case "$action" in
  set-target)
    saved_wifi_ssid=
    saved_wifi_password=
    if [ -f sdkconfig ]; then
      saved_wifi_ssid=$(sed -n 's/^\(CONFIG_KC868_A16_WIFI_SSID=.*\)$/\1/p' sdkconfig)
      saved_wifi_password=$(sed -n 's/^\(CONFIG_KC868_A16_WIFI_PASSWORD=.*\)$/\1/p' sdkconfig)
    fi
    run_idf set-target esp32s3
    restore_wifi_configuration sdkconfig "$saved_wifi_ssid" "$saved_wifi_password"
    run_idf reconfigure
    ;;
  clean) run_idf fullclean ;;
  build) run_idf build ;;
  flash) run_idf -p "$serial_port" flash ;;
  monitor) run_idf -p "$serial_port" monitor ;;
  *)
    echo "unknown action: $action" >&2
    exit 2
    ;;
esac
