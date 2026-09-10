#!/usr/bin/env python3
"""给 ~/.claude/settings.json 追加 permissions 白名单（幂等，可重复跑）"""
import json
import pathlib

p = pathlib.Path.home() / ".claude" / "settings.json"
d = json.loads(p.read_text()) if p.exists() else {}
allow = d.setdefault("permissions", {}).setdefault("allow", [])
for item in [
    "Bash(curl http://localhost:5219*)",
    "Bash(bash /Users/liaobin/Desktop/cnc/restart_api.sh*)",
    "Bash(python3 /Users/liaobin/Desktop/cnc/smoke_*.py*)",
    "Bash(dotnet build*)",
    "Bash(lsof *)",
]:
    if item not in allow:
        allow.append(item)
p.write_text(json.dumps(d, indent=2, ensure_ascii=False) + "\n")
print(p.read_text())
