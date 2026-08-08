#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 1 ] || [ "$#" -gt 2 ]; then
  echo "usage: $0 <controllers-dir> [first-reserved-sector]" >&2
  exit 2
fi

controllers_dir=$1
reserved_sector=${2:-2048}
sdkconfig_file="$controllers_dir/sdkconfig"

if ! [[ "$reserved_sector" =~ ^[1-9][0-9]*$ ]] || [ "$reserved_sector" -gt 2147483647 ]; then
  echo "first reserved sector must be an integer from 1 through 2147483647" >&2
  exit 2
fi
if [ ! -f "$sdkconfig_file" ]; then
  echo "sdkconfig was not found; run the Set board task before provisioning settings storage" >&2
  exit 1
fi
if ! grep -q '^CONFIG_CONTROLLER_SETTINGS_FIRST_RESERVED_SECTOR=' "$sdkconfig_file" ||
  ! grep -q '^CONFIG_CONTROLLER_SETTINGS_MASTER_KEY_HEX=' "$sdkconfig_file"; then
  echo "sdkconfig does not contain the controller settings-storage options; reconfigure the project first" >&2
  exit 1
fi
if ! command -v openssl >/dev/null 2>&1; then
  echo "openssl was not found; install it before generating the settings-storage key" >&2
  exit 127
fi

umask 077
settings_key=$(openssl rand -hex 32)
temporary_file=$(mktemp "${sdkconfig_file}.provision.XXXXXX")

# Removes the generated secret and incomplete replacement on every exit path.
cleanup() {
  settings_key=
  rm -f -- "$temporary_file"
}
trap cleanup EXIT

# Rewrites only the two explicit controller settings-storage values in the active local configuration.
sed \
  -e "s/^CONFIG_CONTROLLER_SETTINGS_FIRST_RESERVED_SECTOR=.*/CONFIG_CONTROLLER_SETTINGS_FIRST_RESERVED_SECTOR=$reserved_sector/" \
  -e "s|^CONFIG_CONTROLLER_SETTINGS_MASTER_KEY_HEX=.*|CONFIG_CONTROLLER_SETTINGS_MASTER_KEY_HEX=\"$settings_key\"|" \
  "$sdkconfig_file" > "$temporary_file"
mv -- "$temporary_file" "$sdkconfig_file"

configured_sector=$(sed -n 's/^CONFIG_CONTROLLER_SETTINGS_FIRST_RESERVED_SECTOR=//p' "$sdkconfig_file")
configured_key_length=$(sed -n 's/^CONFIG_CONTROLLER_SETTINGS_MASTER_KEY_HEX="\([0-9a-f]*\)"$/\1/p' "$sdkconfig_file" | awk '{ print length }')
if [ "$configured_sector" != "$reserved_sector" ] || [ "$configured_key_length" != "64" ]; then
  echo "settings-storage provisioning verification failed" >&2
  exit 1
fi

echo "Provisioned settings storage in sdkconfig: reserved_sector=$configured_sector master_key_hex_length=$configured_key_length"
echo "Build and flash this configuration before initializing the reserved sectors on the controller."
