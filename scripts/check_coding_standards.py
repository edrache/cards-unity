#!/usr/bin/env python3
"""Validate the dependency-free, mechanically enforceable LOOM coding rules."""

from __future__ import annotations

import json
import re
import sys
import xml.etree.ElementTree as ElementTree
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
LOOM_ROOT = REPOSITORY_ROOT / "Assets" / "Scripts" / "Loom"
SCENES_ROOT = REPOSITORY_ROOT / "Assets" / "Scenes"

PASCAL_CASE = re.compile(r"^[A-Z][A-Za-z0-9]*$")
CAMEL_CASE = re.compile(r"^[a-z][A-Za-z0-9]*$")
LOWER_SNAKE_CASE = re.compile(r"^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$")
UI_NAME = re.compile(
    r"^[a-z][a-z0-9]*(?:-[a-z0-9]+)*(?:--[a-z0-9]+(?:-[a-z0-9]+)*)?$"
)
NAMESPACE = re.compile(r"^namespace\s+(?P<name>[A-Za-z][A-Za-z0-9.]*)\s*$", re.MULTILINE)
TOP_LEVEL_TYPE = re.compile(
    r"^ {4}(?:(?:public|internal)\s+)?"
    r"(?:(?:abstract|partial|readonly|ref|sealed|static)\s+)*"
    r"(?P<kind>class|enum|interface|struct)\s+(?P<name>[A-Za-z][A-Za-z0-9]*)\b",
    re.MULTILINE,
)
TOP_LEVEL_DELEGATE = re.compile(
    r"^ {4}(?:(?:public|internal)\s+)?delegate\s+"
    r"[A-Za-z][A-Za-z0-9_.,<>?\[\]]*\s+(?P<name>[A-Za-z][A-Za-z0-9]*)\s*\(",
    re.MULTILINE,
)
PRIVATE_FIELD = re.compile(
    r"^\s{8,}private\s+"
    r"(?P<modifiers>(?:(?:const|readonly|static|volatile)\s+)*)"
    r"[A-Za-z_][A-Za-z0-9_.,<>?\[\]]*\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:=|;)",
    re.MULTILINE,
)

EXPECTED_ASSEMBLIES = {
    "Core/Loom.Core.asmdef": {
        "name": "Loom.Core",
        "references": [],
        "includePlatforms": [],
        "noEngineReferences": True,
        "optionalUnityReferences": None,
    },
    "Fmod/Loom.Fmod.asmdef": {
        "name": "Loom.Fmod",
        "references": ["Loom.Core", "FMODUnity"],
        "includePlatforms": [],
        "noEngineReferences": False,
        "optionalUnityReferences": None,
    },
    "Unity/Loom.Unity.asmdef": {
        "name": "Loom.Unity",
        "references": ["Loom.Core", "Loom.Fmod"],
        "includePlatforms": [],
        "noEngineReferences": False,
        "optionalUnityReferences": None,
    },
    "Demo/Loom.Demo.asmdef": {
        "name": "Loom.Demo",
        "references": ["Loom.Core", "Loom.Fmod", "Loom.Unity", "FMODUnity"],
        "includePlatforms": [],
        "noEngineReferences": False,
        "optionalUnityReferences": None,
    },
    "Tests/EditMode/Loom.Tests.EditMode.asmdef": {
        "name": "Loom.Tests.EditMode",
        "references": ["Loom.Core", "Loom.Fmod", "Loom.Unity", "FMODUnity"],
        "includePlatforms": ["Editor"],
        "noEngineReferences": False,
        "optionalUnityReferences": ["TestAssemblies"],
    },
    "Tests/PlayMode/Loom.Tests.PlayMode.asmdef": {
        "name": "Loom.Tests.PlayMode",
        "references": [
            "Loom.Core",
            "Loom.Fmod",
            "Loom.Unity",
            "Loom.Demo",
            "FMODUnity",
        ],
        "includePlatforms": [],
        "noEngineReferences": False,
        "optionalUnityReferences": ["TestAssemblies"],
    },
}


class Validation:
    def __init__(self) -> None:
        self.errors: list[str] = []
        self.checked_files: set[Path] = set()

    def add_error(self, path: Path, message: str, line: int | None = None) -> None:
        relative_path = path.relative_to(REPOSITORY_ROOT).as_posix()
        location = f"{relative_path}:{line}" if line is not None else relative_path
        self.errors.append(f"{location}: {message}")

    def check_text_file(self, path: Path) -> str | None:
        self.checked_files.add(path)
        raw = path.read_bytes()

        if raw.startswith(b"\xef\xbb\xbf"):
            self.add_error(path, "UTF-8 byte-order marks are not allowed")
        if b"\r" in raw:
            self.add_error(path, "use LF line endings")
        if raw and not raw.endswith(b"\n"):
            self.add_error(path, "file must end with a newline")

        try:
            text = raw.decode("utf-8-sig")
        except UnicodeDecodeError as exception:
            self.add_error(path, f"file is not valid UTF-8: {exception}")
            return None

        for line_number, line in enumerate(text.splitlines(), start=1):
            if "\t" in line:
                self.add_error(path, "tabs are not allowed", line_number)
            if line.endswith(" ") or line.endswith("\t"):
                self.add_error(path, "trailing whitespace is not allowed", line_number)

        return text


def expected_namespace(path: Path) -> str:
    relative_parent = path.relative_to(LOOM_ROOT).parent
    return "Loom." + ".".join(relative_parent.parts)


def remove_comments_and_strings(text: str) -> str:
    without_block_comments = re.sub(r"/\*.*?\*/", "", text, flags=re.DOTALL)
    without_line_comments = re.sub(r"//.*$", "", without_block_comments, flags=re.MULTILINE)
    return re.sub(r'"(?:\\.|[^"\\])*"', '""', without_line_comments)


def check_csharp(validation: Validation, path: Path) -> None:
    text = validation.check_text_file(path)
    if text is None:
        return

    if not PASCAL_CASE.fullmatch(path.stem):
        validation.add_error(path, "C# file names must use PascalCase")

    for line_number, line in enumerate(text.splitlines(), start=1):
        if len(line) > 120:
            validation.add_error(
                path,
                f"C# line has {len(line)} characters; maximum is 120",
                line_number,
            )
        if re.search(r"\)\s*\{\s*$", line):
            validation.add_error(path, "opening braces must use Allman layout", line_number)
        if re.match(r"^\s*(?:do|else|finally|try)\s*\{\s*$", line):
            validation.add_error(path, "opening braces must use Allman layout", line_number)

    is_assembly_info = path.name == "AssemblyInfo.cs"
    if not is_assembly_info:
        namespace_matches = list(NAMESPACE.finditer(text))
        required_namespace = expected_namespace(path)
        if len(namespace_matches) != 1:
            validation.add_error(path, "declare exactly one block-scoped namespace")
        else:
            namespace_match = namespace_matches[0]
            actual_namespace = namespace_match.group("name")
            if actual_namespace != required_namespace:
                validation.add_error(
                    path,
                    f"namespace must be {required_namespace}, found {actual_namespace}",
                )

            following_text = text[namespace_match.end() :]
            next_code_line = next(
                (line.strip() for line in following_text.splitlines() if line.strip()),
                None,
            )
            if next_code_line != "{":
                validation.add_error(path, "namespace opening brace must be on the next line")

    type_entries = [
        (match.group("kind"), match.group("name")) for match in TOP_LEVEL_TYPE.finditer(text)
    ]
    type_entries.extend(("delegate", match.group("name")) for match in TOP_LEVEL_DELEGATE.finditer(text))

    if not is_assembly_info:
        if not type_entries:
            validation.add_error(path, "C# file must contain a top-level type")
        elif path.stem not in {name for _, name in type_entries}:
            validation.add_error(
                path,
                f"file name must match a top-level type; found {', '.join(name for _, name in type_entries)}",
            )

    for kind, name in type_entries:
        if not PASCAL_CASE.fullmatch(name):
            validation.add_error(path, f"type name {name} must use PascalCase")
        if kind == "interface" and not re.fullmatch(r"I[A-Z][A-Za-z0-9]*", name):
            validation.add_error(path, f"interface name {name} must use an I prefix")

    relative_parts = path.relative_to(LOOM_ROOT).parts
    if relative_parts[0] == "Tests" and path.name != "AssemblyInfo.cs":
        if not path.stem.endswith("Tests"):
            validation.add_error(path, "test fixture files must end with Tests.cs")
        for _, name in type_entries:
            if not name.endswith("Tests"):
                validation.add_error(path, f"top-level test fixture {name} must end with Tests")

    for field_match in PRIVATE_FIELD.finditer(text):
        modifiers = set(field_match.group("modifiers").split())
        name = field_match.group("name")
        if "const" in modifiers or {"static", "readonly"}.issubset(modifiers):
            if not PASCAL_CASE.fullmatch(name):
                validation.add_error(path, f"constant/static readonly field {name} must use PascalCase")
        elif not CAMEL_CASE.fullmatch(name):
            validation.add_error(path, f"private field {name} must use camelCase without a prefix")

    if relative_parts[0] == "Core":
        code = remove_comments_and_strings(text)
        forbidden_tokens = {
            "UnityEngine.": "Core must not reference UnityEngine",
            "FMOD.": "Core must not reference FMOD",
            "FMODUnity": "Core must not reference FMODUnity",
            "Loom.Fmod": "Core must not reference Loom.Fmod",
            "Loom.Unity": "Core must not reference Loom.Unity",
            "Loom.Demo": "Core must not reference Loom.Demo",
            "System.Random": "deterministic Core code must not use System.Random",
            "DateTime.Now": "deterministic Core code must not use wall-clock time",
            "DateTime.UtcNow": "deterministic Core code must not use wall-clock time",
            "Environment.TickCount": "deterministic Core code must not use wall-clock time",
            "Stopwatch.GetTimestamp": "deterministic Core code must not use wall-clock time",
        }
        for token, message in forbidden_tokens.items():
            if token in code:
                validation.add_error(path, message)


def check_assembly_definitions(validation: Validation) -> None:
    actual_paths = {
        path.relative_to(LOOM_ROOT).as_posix() for path in LOOM_ROOT.rglob("*.asmdef")
    }
    expected_paths = set(EXPECTED_ASSEMBLIES)

    for missing_path in sorted(expected_paths - actual_paths):
        validation.add_error(LOOM_ROOT / missing_path, "required assembly definition is missing")
    for unexpected_path in sorted(actual_paths - expected_paths):
        validation.add_error(
            LOOM_ROOT / unexpected_path,
            "assembly is not declared in the coding-standard dependency map",
        )

    for relative_path, expected in EXPECTED_ASSEMBLIES.items():
        path = LOOM_ROOT / relative_path
        if not path.exists():
            continue
        text = validation.check_text_file(path)
        if text is None:
            continue
        try:
            data = json.loads(text)
        except json.JSONDecodeError as exception:
            validation.add_error(path, f"invalid JSON: {exception}")
            continue

        name = expected["name"]
        if data.get("name") != name:
            validation.add_error(path, f"assembly name must be {name}")
        if data.get("rootNamespace") != name:
            validation.add_error(path, f"rootNamespace must be {name}")
        if data.get("references") != expected["references"]:
            validation.add_error(
                path,
                f"references must be {expected['references']!r}",
            )
        if data.get("includePlatforms") != expected["includePlatforms"]:
            validation.add_error(
                path,
                f"includePlatforms must be {expected['includePlatforms']!r}",
            )
        if data.get("excludePlatforms") != []:
            validation.add_error(path, "excludePlatforms must be empty")
        if data.get("allowUnsafeCode") is not False:
            validation.add_error(path, "allowUnsafeCode must be false")
        if data.get("overrideReferences") is not False:
            validation.add_error(path, "overrideReferences must be false")
        if data.get("autoReferenced") is not True:
            validation.add_error(path, "autoReferenced must be true")
        if data.get("noEngineReferences") is not expected["noEngineReferences"]:
            validation.add_error(
                path,
                f"noEngineReferences must be {expected['noEngineReferences']!r}",
            )

        expected_optional_references = expected["optionalUnityReferences"]
        if expected_optional_references is None:
            if "optionalUnityReferences" in data:
                validation.add_error(path, "runtime assemblies must not declare optionalUnityReferences")
        elif data.get("optionalUnityReferences") != expected_optional_references:
            validation.add_error(
                path,
                f"optionalUnityReferences must be {expected_optional_references!r}",
            )


def check_ui(validation: Validation) -> None:
    for path in sorted(LOOM_ROOT.rglob("*.uxml")):
        text = validation.check_text_file(path)
        if text is None:
            continue
        if not PASCAL_CASE.fullmatch(path.stem):
            validation.add_error(path, "UXML file names must use PascalCase")
        try:
            root = ElementTree.fromstring(text)
        except ElementTree.ParseError as exception:
            validation.add_error(path, f"invalid UXML: {exception}")
            continue

        for element in root.iter():
            name = element.attrib.get("name")
            if name is not None and not UI_NAME.fullmatch(name):
                validation.add_error(path, f"UXML name '{name}' must use kebab-case")
            for class_name in element.attrib.get("class", "").split():
                if not UI_NAME.fullmatch(class_name):
                    validation.add_error(
                        path,
                        f"UXML class '{class_name}' must use kebab-case or a --modifier",
                    )

    for extension, description in (("*.uss", "USS"), ("*.asset", "asset")):
        for path in sorted(LOOM_ROOT.rglob(extension)):
            validation.check_text_file(path)
            if not PASCAL_CASE.fullmatch(path.stem):
                validation.add_error(path, f"{description} file names must use PascalCase")


def check_scenes(validation: Validation) -> None:
    legacy_scenes = {"SampleScene.unity"}
    for path in sorted(SCENES_ROOT.glob("*.unity")):
        if path.name in legacy_scenes:
            continue
        if not path.stem.startswith("scn_") or not LOWER_SNAKE_CASE.fullmatch(path.stem[4:]):
            validation.add_error(
                path,
                "LOOM scene names must use scn_<lower_snake_case>.unity",
            )


def check_meta_parity(validation: Validation, root: Path) -> None:
    for path in sorted(root.rglob("*")):
        if path.is_dir():
            continue
        if path.suffix == ".meta":
            asset_path = path.with_suffix("")
            if not asset_path.exists():
                validation.add_error(path, "orphaned Unity metadata file")
        else:
            meta_path = Path(str(path) + ".meta")
            if not meta_path.exists():
                validation.add_error(path, "Unity asset is missing its .meta file")


def main() -> int:
    validation = Validation()

    required_text_files = [
        REPOSITORY_ROOT / ".editorconfig",
        REPOSITORY_ROOT / ".github" / "workflows" / "coding-standards.yml",
        REPOSITORY_ROOT / "docs" / "coding-standards.md",
        Path(__file__).resolve(),
    ]
    for path in required_text_files:
        if not path.exists():
            validation.add_error(path, "required coding-standard file is missing")
        else:
            validation.check_text_file(path)

    for path in sorted(LOOM_ROOT.rglob("*.cs")):
        check_csharp(validation, path)

    check_assembly_definitions(validation)
    check_ui(validation)
    check_scenes(validation)
    check_meta_parity(validation, LOOM_ROOT)
    check_meta_parity(validation, SCENES_ROOT)

    if validation.errors:
        print(f"Coding-standard check failed with {len(validation.errors)} error(s):")
        for error in sorted(validation.errors):
            print(f"- {error}")
        return 1

    print(
        "Coding-standard check passed "
        f"({len(validation.checked_files)} text files plus Unity metadata parity)."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
