#!/usr/bin/env python3
"""Replaces literal UI strings in XAML with strongly typed resource lookups.

Only the attributes listed below are touched, and only when the whole attribute
value matches a string from strings.tsv, so bindings and markup stay intact.
"""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[2]
src = root / "src/NotepadMoreOS"

aliases = {
    "Save all": "Menu_SaveAll",
    "Containing Folder": "Menu_OpenFolder",
}

mapping = {}
for line in (Path(__file__).parent / "strings.tsv").read_text(encoding="utf-8").splitlines():
    if not line.strip() or line.lstrip().startswith("#"):
        continue
    key, en, _ = line.split("|", 2)
    mapping.setdefault(en, key)
mapping.update(aliases)

attributes = r"(?:Text|Header|Content|ToolTip\.Tip|Title|PlaceholderText|AutomationProperties\.Name)"
pattern = re.compile(rf'(?P<name>\b{attributes})="(?P<value>[^"{{}}]*)"')

files = [
    "Views/MainWindow.axaml",
    "Views/WelcomeView.axaml",
    "Views/DocumentView.axaml",
    "Views/Dialogs/AboutWindow.axaml",
    "Views/Dialogs/SettingsWindow.axaml",
    "Views/Dialogs/SaveChangesWindow.axaml",
    "Views/Dialogs/GoToLineWindow.axaml",
    "Views/Dialogs/PromptWindow.axaml",
    "Views/Dialogs/MessageWindow.axaml",
    "Views/Dialogs/FindInFilesWindow.axaml",
]

total = 0
for relative in files:
    path = src / relative
    text = path.read_text(encoding="utf-8")
    replaced = []

    def substitute(match):
        value = match.group("value")
        key = mapping.get(value)
        if key is None:
            return match.group(0)
        replaced.append(value)
        return f'{match.group("name")}="{{x:Static loc:Strings.{key}}}"'

    updated = pattern.sub(substitute, text)

    if replaced:
        if 'xmlns:loc="using:NotepadMoreOS.Resources"' not in updated:
            updated = re.sub(
                r'(<(?P<tag>Window|UserControl)\b)',
                r'\1\n        xmlns:loc="using:NotepadMoreOS.Resources"',
                updated,
                count=1,
            )
        path.write_text(updated, encoding="utf-8")
        total += len(replaced)
        print(f"{relative}: {len(replaced)} strings")

print(f"{total} replacements")
