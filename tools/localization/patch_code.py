#!/usr/bin/env python3
"""Rewrites user-facing string literals in C# into strongly typed lookups.

Only whole literals that exist in strings.tsv are replaced, and lines that look
like control flow, keys or command parameters are skipped to avoid changing
behaviour instead of text.
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
src = root / "src/NotepadMoreOS"

mapping = {}
for line in (Path(__file__).parent / "strings.tsv").read_text(encoding="utf-8").splitlines():
    if not line.strip() or line.lstrip().startswith("#"):
        continue
    key, en, _ = line.split("|", 2)
    mapping.setdefault(en, key)

files = [
    "ViewModels/MainWindowViewModel.cs",
    "ViewModels/FindReplaceViewModel.cs",
    "ViewModels/FindInFilesViewModel.cs",
    "ViewModels/DocumentViewModel.cs",
    "Views/MainWindow.NativeMenu.cs",
    "Views/Dialogs/AboutWindow.axaml.cs",
    "Views/Dialogs/GoToLineWindow.axaml.cs",
    "Views/Dialogs/SaveChangesWindow.axaml.cs",
    "Views/Dialogs/FindInFilesWindow.axaml.cs",
]

# Never touch identifiers, keys, comparisons or command parameters.
skip = re.compile(r"(case |switch|nameof|CommandParameter|==|!=|TryGetValue|Dictionary|\bget;|\bkey\b)")

literal = re.compile(r'"(?P<value>[^"\\]*)"')
total = 0
for relative in files:
    path = src / relative
    lines = path.read_text(encoding="utf-8").splitlines(keepends=True)
    out = []
    for line in lines:
        if skip.search(line) or '$"' in line:
            out.append(line)
            continue

        def substitute(match):
            key = mapping.get(match.group("value"))
            if key is None:
                return match.group(0)
            return f"Strings.{key}"

        updated = literal.sub(substitute, line)
        if updated != line:
            total += 1
        out.append(updated)

    path.write_text("".join(out), encoding="utf-8")

print(f"{total} lines updated")
