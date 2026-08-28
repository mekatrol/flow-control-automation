#!/usr/bin/env bash

set -euo pipefail

port="${1:-5018}"
if [[ ! "$port" =~ ^[0-9]+$ ]] || ((port < 1 || port > 65535)); then
  echo "Port must be an integer between 1 and 65535." >&2
  exit 2
fi

if command -v lsof >/dev/null 2>&1; then
  mapfile -t process_ids < <(lsof -t -iTCP:"$port" -sTCP:LISTEN 2>/dev/null | sort -u)
elif command -v fuser >/dev/null 2>&1; then
  read -r -a process_ids <<<"$(fuser "$port/tcp" 2>/dev/null || true)"
else
  echo "Unable to inspect port $port: install lsof or fuser." >&2
  exit 1
fi

if ((${#process_ids[@]} == 0)); then
  echo "No process is listening on port $port."
  exit 0
fi

process_list=$(IFS=,; echo "${process_ids[*]}")
ps -o pid,ppid,comm,args -p "$process_list"
kill "${process_ids[@]}"
echo "Stopped the process listening on port $port."
