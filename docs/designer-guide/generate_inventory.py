#!/usr/bin/env python3
"""Refresh the source-derived component inventory embedded in index.html.

Run from any directory: python3 docs/designer-guide/generate_inventory.py
The guide's prose is editorial; this small inventory deliberately reports only
serialized source declarations and their code defaults. Prefab/scene overrides
remain Unity data and must be inspected in the relevant Inspector.
"""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
GUIDE = Path(__file__).with_name("index.html")
SOURCE_ROOTS = (ROOT / "Assets/Scripts/Controllers", ROOT / "Assets/Scripts/Rendering")
CLASS_RE = re.compile(r"\b(?:public|internal|private)\s+(?:static\s+)?(?:sealed\s+)?(?:partial\s+)?(?:class|struct)\s+(\w+)")
FIELD_RE = re.compile(
    r"^\s*(?P<attributes>(?:\[[^\]]+\]\s*)+)?"
    r"(?P<access>private|public|protected)\s+(?P<static>static\s+)?(?P<readonly>readonly\s+)?"
    r"(?P<type>[\w<>\[\], ]+?)\s+(?P<names>\w+(?:\s*,\s*\w+)*)"
    r"\s*(?:=\s*(?P<default>[^;]+))?;\s*$"
)


def source_inventory() -> list[dict]:
    entries: dict[str, dict] = {}
    for source_root in SOURCE_ROOTS:
        for path in sorted(source_root.rglob("*.cs")):
            if "/Editor/" in str(path):
                continue
            root_name = path.name.split(".")[0]
            depth = 0
            classes: list[list] = []  # display name, depth before body, body opened
            pending_attributes: list[str] = []
            pending_serializable = False
            for raw in path.read_text(encoding="utf-8").splitlines():
                stripped = raw.strip()
                if stripped.startswith("["):
                    pending_attributes.append(stripped)
                    pending_serializable = pending_serializable or "Serializable" in stripped
                match = FIELD_RE.match(raw)
                found_class = CLASS_RE.search(raw)
                if found_class:
                    class_name = found_class.group(1)
                    if class_name == root_name:
                        display_name = root_name
                        entries.setdefault(display_name, {"name": display_name, "paths": [str(path.relative_to(ROOT))], "fields": []})
                    elif class_name in {"BoulderPlan", "Triangle", "Node"}:
                        # Runtime helpers, not Inspector-authorable types.
                        display_name = None
                    elif pending_serializable:
                        display_name = f"{root_name} / {class_name}"
                    else:
                        display_name = None
                    classes.append([display_name, depth, "{" in raw])
                    pending_serializable = False
                    pending_attributes.clear()
                if match and "=>" not in raw and classes:
                    attributes = " ".join(pending_attributes + [match.group("attributes") or ""])
                    serialized = ("SerializeField" in attributes or
                                  (match.group("access") == "public" and not match.group("static") and not match.group("readonly")))
                    if serialized and classes[-1][0]:
                        constraints = list(dict.fromkeys(re.findall(r"(?:Range|Min)\([^)]*\)", attributes)))
                        tooltip = re.search(r'Tooltip\("([^"]*)"\)', attributes)
                        if tooltip:
                            constraints.append("Note: " + tooltip.group(1))
                        default = (match.group("default") or "Unity default / assigned reference").strip()
                        component = classes[-1][0]
                        entry = entries.setdefault(component, {"name": component, "paths": [], "fields": []})
                        relative = str(path.relative_to(ROOT))
                        if relative not in entry["paths"]:
                            entry["paths"].append(relative)
                        for name in re.split(r"\s*,\s*", match.group("names")):
                            entry["fields"].append({"name": name, "type": match.group("type").strip(), "default": default, "constraint": " | ".join(constraints)})
                    pending_attributes.clear()
                # Count braces after processing a field on the current line.
                depth += raw.count("{") - raw.count("}")
                for item in classes:
                    if depth > item[1]:
                        item[2] = True
                while classes and classes[-1][2] and depth <= classes[-1][1]:
                    classes.pop()
    notes_path = GUIDE.with_name("system-notes.json")
    notes = json.loads(notes_path.read_text(encoding="utf-8")) if notes_path.exists() else {}
    for name, entry in entries.items():
        if name in notes:
            entry["description"] = notes[name]
    return sorted(entries.values(), key=lambda entry: entry["name"].lower())


def main() -> None:
    page = GUIDE.read_text(encoding="utf-8")
    payload = json.dumps(source_inventory(), ensure_ascii=False, separators=(",", ":")).replace("<", "\\u003c")
    updated, count = re.subn(
        r"(<script id=\"source-inventory\" type=\"application/json\">).*?(</script>)",
        lambda m: m.group(1) + payload + m.group(2), page, flags=re.S,
    )
    if count != 1:
        raise SystemExit("Expected exactly one source-inventory script element.")
    GUIDE.write_text(updated, encoding="utf-8")
    print(f"Updated {GUIDE.relative_to(ROOT)} with {len(json.loads(payload))} components.")


if __name__ == "__main__":
    main()
