#!/usr/bin/env bash
# PostToolUse hook: whitespace-format the .cs file Claude just edited.
# Reads the hook payload on stdin; does nothing for non-.cs files.
f=$(node -e 'let d="";process.stdin.on("data",c=>d+=c).on("end",()=>{const f=((JSON.parse(d).tool_input)||{}).file_path||"";if(/\.cs$/i.test(f))process.stdout.write(f)})')
[ -z "$f" ] && exit 0
dotnet format whitespace "${CLAUDE_PROJECT_DIR:-.}" --folder --include "$f" >/dev/null 2>&1
exit 0
