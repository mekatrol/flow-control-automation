#!/usr/bin/env bash
set -euo pipefail

export KCONFIG_REPORT_VERBOSITY=quiet

if [ "$#" -lt 2 ]; then
  echo "usage: $0 <controllers-dir> <set-board|format|clean|build|flash|monitor> [board-or-port]" >&2
  exit 2
fi

controllers_dir=$1
action=$2
argument=${3:-}
selection_file="$controllers_dir/.controller-board"
board=kincony-kc868-a16
idf_python=
idf_py=

if [ -f "$selection_file" ]; then
  board=$(<"$selection_file")
fi

configure_board() {
  case "$board" in
    kincony-kc868-a16)
      target=esp32s3
      ;;
    *)
      echo "unknown controller board: $board" >&2
      exit 2
      ;;
  esac
  build_directory="build-$board"
}

prepend_path() {
  if [ -d "$1" ]; then
    PATH="$1:$PATH"
  fi
}

configure_versioned_esp_idf() {
  local idf_directory version_directory tools_path candidate
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
      "$tools_path"/riscv32-esp-elf/*/riscv32-esp-elf/bin \
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
    # shellcheck disable=SC1090
    . "$ESP_IDF_EXPORT_SH" >/dev/null
    return
  fi
  if [ -n "${IDF_PATH:-}" ] && [ -f "$IDF_PATH/export.sh" ]; then
    # shellcheck disable=SC1091
    . "$IDF_PATH/export.sh" >/dev/null
    return
  fi
  echo "idf.py was not found; configure ESP-IDF in VS Code or set IDF_PATH." >&2
  exit 127
}

run_idf() {
  find_esp_idf
  export CONTROLLER_BOARD="$board"
  if command -v idf.py >/dev/null 2>&1; then
    idf.py -B "$build_directory" "$@"
  else
    "$idf_python" "$idf_py" -B "$build_directory" "$@"
  fi
}

# Redact local network values because ESP-IDF 6 can echo changed string defaults.
run_idf_redacted() {
  run_idf "$@" 2>&1 |
    sed -E \
      -e '/CONTROLLER_MQTT_(HOST|CLIENT_ID)/ s/"[^"]*"/"<redacted>"/g' \
      -e '/CONTROLLER_SETTINGS_MASTER_KEY_HEX/ s/"[^"]*"/"<redacted>"/g' \
      -e '/Using default value from sdkconfig/ s/\("[^"]*"\)/("<redacted>")/g'
}

# Restores local controller settings after set-target regenerates sdkconfig.
restore_controller_configuration() {
  local sdkconfig_file=$1
  local saved_configuration=$2
  local temporary_file
  if [ -z "$saved_configuration" ]; then
    return
  fi
  temporary_file=$(mktemp "${sdkconfig_file}.controller.XXXXXX")
  while IFS= read -r configuration_line; do
    local configuration_key=${configuration_line%%=*}
    local replacement=
    while IFS= read -r saved_line; do
      if [ "${saved_line%%=*}" = "$configuration_key" ]; then
        replacement=$saved_line
        break
      fi
    done <<< "$saved_configuration"
    printf '%s\n' "${replacement:-$configuration_line}"
  done < "$sdkconfig_file" > "$temporary_file"
  mv "$temporary_file" "$sdkconfig_file"
}

configure_board
cd "$controllers_dir"

case "$action" in
  set-board)
    saved_controller_configuration=
    if [ -f sdkconfig ]; then
      saved_controller_configuration=$(sed -n '/^CONFIG_CONTROLLER_.*=/p' sdkconfig)
    fi
    board=${argument:-}
    configure_board
    printf '%s\n' "$board" > "$selection_file"
    run_idf_redacted set-target "$target"
    restore_controller_configuration sdkconfig "$saved_controller_configuration"
    run_idf_redacted reconfigure
    echo "Selected $board; subsequent tasks use $build_directory."
    ;;
  format)
    # Prefer an existing formatter, then expose ESP-IDF's pinned toolchain so every contributor applies the same repository configuration.
    if ! command -v clang-format >/dev/null 2>&1; then
      configure_versioned_esp_idf || true
    fi
    if ! command -v clang-format >/dev/null 2>&1; then
      echo "clang-format was not found; install it or configure the ESP-IDF toolchain." >&2
      exit 127
    fi
    formatter_python=${idf_python:-}
    if [ -z "$formatter_python" ]; then
      formatter_python=$(command -v python3 || command -v python || true)
    fi
    if [ -z "$formatter_python" ]; then
      echo "Python was not found; install it or configure the ESP-IDF toolchain." >&2
      exit 127
    fi
    # Include tracked and new non-ignored sources so formatting works before files are staged or committed.
    while IFS= read -r -d '' source_file; do
      # A refactor can leave a tracked path deleted while its replacement is still unstaged.
      if [ ! -f "$source_file" ]; then
        continue
      fi
      clang-format -i "$source_file"
      "$formatter_python" "$controllers_dir/scripts/format-source.py" "$source_file"
    done < <(git ls-files -z --cached --others --exclude-standard -- '*.c' '*.h')
    ;;
  clean) run_idf_redacted fullclean ;;
  build) run_idf_redacted build ;;
  flash) run_idf_redacted -p "${argument:-/dev/ttyACM0}" flash ;;
  monitor) run_idf -p "${argument:-/dev/ttyACM0}" monitor ;;
  *)
    echo "unknown action: $action" >&2
    exit 2
    ;;
esac
