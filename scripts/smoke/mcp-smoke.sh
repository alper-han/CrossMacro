#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: mcp-smoke.sh (--binary <path> | --command <command> | -- <command> [args...]) [options]

Runs the non-effectful CrossMacro MCP smoke contract through MCP Inspector:
  - tools/list discovers the registered v1 tools
  - status.get and help.get succeed
  - invalid macro input returns a structured invalid_arguments tool error
  - unknown tools return the expected tool_not_found protocol error
  - stdout is JSON-only while diagnostics remain on stderr
  - a second Inspector session can restart the server

Options:
  --inspector <command>  Inspector CLI command. Defaults to npx -y @modelcontextprotocol/inspector@2.3.0.
  --binary <path>        MCP executable path; appends the mcp argument.
  --command <command>    Shell command used to start the MCP server.
  --restricted            Start a published binary with mcp --restricted.
  --                   Remaining arguments form the server command.
  -h, --help             Show this help.
USAGE
}

fail() {
  echo "MCP smoke failed: $1" >&2
  if [ "$#" -gt 1 ] && [ -n "${2:-}" ]; then
    printf '%s\n' "$2" >&2
  fi
  exit 1
}

assert_contains() {
  local assertion_name="$1"
  local haystack="$2"
  local needle="$3"
  printf '%s\n' "$haystack" | grep -F -- "$needle" >/dev/null || fail "$assertion_name" "$haystack"
}

assert_json_object() {
  local assertion_name="$1"
  local output="$2"
  printf '%s\n' "$output" | jq -e 'type == "object"' >/dev/null || fail "$assertion_name: stdout was not one JSON object" "$output"
}

assert_json_result() {
  local assertion_name="$1"
  local output="$2"
  printf '%s\n' "$output" | jq -e 'type == "object" and (.result | type == "object")' >/dev/null \
    || fail "$assertion_name: Inspector output was not a JSON result" "$output"
}

if [ "$#" -eq 0 ]; then
  usage >&2
  exit 2
fi

INSPECTOR_COMMAND=(npx -y @modelcontextprotocol/inspector@2.3.0)
SERVER_COMMAND=()
COMMAND_DISPLAY=
USE_SHELL_COMMAND=0
RESTRICTED=0
SERVER_TARGET_SET=0

while [ "$#" -gt 0 ]; do
  case "$1" in
    -h|--help)
      usage
      exit 0
      ;;
    --inspector)
      [ "$#" -ge 2 ] || fail "--inspector requires a command"
      read -r -a INSPECTOR_COMMAND <<< "$2"
      shift 2
      ;;
    --binary)
      [ "$#" -ge 2 ] || fail "--binary requires a path"
      [ "$SERVER_TARGET_SET" -eq 0 ] || fail "only one MCP server target may be provided"
      SERVER_COMMAND=("$2" mcp)
      COMMAND_DISPLAY="$2 mcp"
      SERVER_TARGET_SET=1
      shift 2
      ;;
    --command)
      [ "$#" -ge 2 ] || fail "--command requires a command string"
      [ "$SERVER_TARGET_SET" -eq 0 ] || fail "only one MCP server target may be provided"
      USE_SHELL_COMMAND=1
      COMMAND_DISPLAY="$2"
      SERVER_TARGET_SET=1
      shift 2
      ;;
    --restricted)
      RESTRICTED=1
      shift
      ;;
    --)
      shift
      [ "$#" -gt 0 ] || fail "-- requires a server command"
      [ "$SERVER_TARGET_SET" -eq 0 ] || fail "only one MCP server target may be provided"
      SERVER_COMMAND=("$@")
      COMMAND_DISPLAY="$*"
      SERVER_TARGET_SET=1
      shift "$#"
      ;;
    *)
      fail "unknown argument: $1"
      ;;
  esac

done

[ "$SERVER_TARGET_SET" -eq 1 ] || fail "missing MCP server target"

if [ "$RESTRICTED" -eq 1 ] && [ "$USE_SHELL_COMMAND" -eq 1 ]; then
  fail "--restricted is supported with --binary or an explicit -- command"
fi

if [ "$RESTRICTED" -eq 1 ]; then
  SERVER_COMMAND+=(--restricted)
fi

run_inspector() {
  local method="$1"
  shift
  local stdout_file="$1"
  shift
  local stderr_file="$1"
  shift
  local exit_code=0
  local allow_tool_error=0
  local require_tool_error_exit=0

  if [ "$method" = "tools/call" ] && printf '%s\n' "$*" | grep -F -- '--tool-name macro.inspect' >/dev/null; then
    allow_tool_error=1
  fi

  if [ "$method" = "tools/call" ] && printf '%s\n' "$*" | grep -F -- '--tool-name unknown' >/dev/null; then
    allow_tool_error=1
    require_tool_error_exit=1
  fi

  if [ "$USE_SHELL_COMMAND" = "1" ]; then
    "${INSPECTOR_COMMAND[@]}" --cli sh -c "$COMMAND_DISPLAY" -- --method "$method" --format json "$@" >"$stdout_file" 2>"$stderr_file" || exit_code=$?
  else
    "${INSPECTOR_COMMAND[@]}" --cli "${SERVER_COMMAND[@]}" -- --method "$method" --format json "$@" >"$stdout_file" 2>"$stderr_file" || exit_code=$?
  fi

  if [ "$require_tool_error_exit" -eq 1 ] && [ "$exit_code" -eq 0 ]; then
    fail "Inspector command unexpectedly succeeded for $method" "$(<"$stdout_file")\n$(<"$stderr_file")"
  fi

  if [ "$exit_code" -ne 0 ] && [ "$allow_tool_error" -ne 1 ]; then
    fail "Inspector command exited $exit_code for $method" "$(<"$stdout_file")\n$(<"$stderr_file")"
  fi
}

work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

run_inspector tools/list "$work_dir/tools.stdout" "$work_dir/tools.stderr"
tools_output="$(<"$work_dir/tools.stdout")"
assert_json_result 'tools/list result' "$tools_output"
assert_contains 'tools/list tool count' "$tools_output" '"tools"'
tool_count="$(jq -r '.result.tools | length' "$work_dir/tools.stdout")"
[ "$tool_count" -eq 61 ] || fail "expected 61 MCP tools, found $tool_count" "$tools_output"
jq -e '.result.tools | map(.name) | index("command.execute") and index("status.get") and index("help.get")' \
  "$work_dir/tools.stdout" >/dev/null || fail 'tools/list required tools' "$tools_output"

run_inspector tools/call "$work_dir/status.stdout" "$work_dir/status.stderr" \
  --tool-name status.get
status_output="$(<"$work_dir/status.stdout")"
assert_json_result 'status.get result' "$status_output"
jq -e '.result.isError != true and (.result.structuredContent.runtime == "mcp")' \
  "$work_dir/status.stdout" >/dev/null || fail 'status.get was not successful' "$status_output"

run_inspector tools/call "$work_dir/help.stdout" "$work_dir/help.stderr" \
  --tool-name help.get
help_output="$(<"$work_dir/help.stdout")"
assert_json_result 'help.get result' "$help_output"
jq -e '.result.isError != true and (.result.structuredContent.transport == "local-stdio")' \
  "$work_dir/help.stdout" >/dev/null || fail 'help.get was not successful' "$help_output"

if [ "$RESTRICTED" -eq 1 ]; then
  jq -e '.result.structuredContent.isRestricted == true and (.result.structuredContent.availableTools | any(.name == "status.get" and .enabled == true)) and (.result.structuredContent.availableTools | any(.name == "command.execute" and .enabled == false))' \
    "$work_dir/help.stdout" >/dev/null || fail 'restricted help did not expose the expected tool policy' "$help_output"
fi

run_inspector tools/call "$work_dir/invalid.stdout" "$work_dir/invalid.stderr" \
  --tool-name macro.inspect \
  --tool-args-json '{"macroPath":"relative.macro"}'
invalid_output="$(<"$work_dir/invalid.stdout")"
assert_json_result 'invalid macro result' "$invalid_output"
jq -e '.result.isError == true and (.result.structuredContent.outcome.errors[0].code == "invalid_arguments")' \
  "$work_dir/invalid.stdout" >/dev/null || fail 'invalid macro request was not a structured error' "$invalid_output"

run_inspector tools/call "$work_dir/unknown.stdout" "$work_dir/unknown.stderr" \
  --tool-name unknown
unknown_output="$(<"$work_dir/unknown.stdout")"
[ -z "$unknown_output" ] || fail 'unknown tool unexpectedly wrote protocol output to stdout' "$unknown_output"
assert_contains 'unknown tool error code' "$(<"$work_dir/unknown.stderr")" '"code":"tool_not_found"'
assert_contains 'unknown tool error message' "$(<"$work_dir/unknown.stderr")" "Tool 'unknown' not found on server."

for output_file in "$work_dir"/*.stdout; do
  [ "$(basename "$output_file")" = "unknown.stdout" ] && continue
  assert_json_object "stdout JSON purity for $output_file" "$(<"$output_file")"
done

run_inspector tools/call "$work_dir/restart.stdout" "$work_dir/restart.stderr" \
  --tool-name help.get
restart_output="$(<"$work_dir/restart.stdout")"
assert_json_result 'restart help.get result' "$restart_output"
jq -e '.result.isError != true' "$work_dir/restart.stdout" >/dev/null \
  || fail 'server did not support a clean restart' "$restart_output"

echo "MCP smoke: OK ($tool_count tools, structured safety/error checks, stdout purity, restart)"
