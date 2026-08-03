#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "usage: $0 <controllers-dir>" >&2
  exit 2
fi

controllers_dir=$1
unused_parameter_pattern='^[[:space:]]*\(void\)[[:space:]]*[A-Za-z_][A-Za-z0-9_]*[[:space:]]*;'

cd "$controllers_dir"

# Reject discarded parameter names because C23 unnamed parameters express the callback contract without executable suppression statements.
if grep -R -n -E --include='*.c' --include='*.h' "$unused_parameter_pattern" main.c shared platforms boards tests; then
  echo "error: replace each '(void)parameter;' statement with an unnamed C23 parameter such as 'void * /* context */'." >&2
  exit 1
fi
