# LOOM Coding Standard

## 1. Authority and scope

This document is the canonical coding, structure, and naming standard for first-party LOOM work.
It applies to:

- `Assets/Scripts/Loom/`;
- LOOM-owned scenes in `Assets/Scenes/`;
- repository automation under `scripts/` and `.github/workflows/`;
- new LOOM documentation and tests.

Imported packages such as FMOD, Feel, Quibli, Rewired, and TrueShadow keep their upstream style and
must not be reformatted merely to match this document. `docs/loom-design-doc.md` remains a Polish
source document. When this standard conflicts with an architectural or product decision, `AGENTS.md`
and `docs/loom-implementation-plan.md` take precedence and this document must be updated in the same
change.

## 2. Language

- Write code, identifiers, comments, test names, new documentation, automation output, and commit
  messages in English.
- Use comments to explain intent, invariants, ownership, or a non-obvious constraint. Do not narrate
  code that is already clear.
- Add XML documentation to public contracts when lifecycle, units, valid ranges, ownership, timing,
  or failure behavior is not evident from the signature.
- Preserve native external names when they are part of an API or serialized contract, for example
  `FMOD.RESULT`, `bus:/MUS_Synth`, and UXML element names.

## 3. Repository structure and ownership

Runtime code belongs below `Assets/Scripts/Loom/` and follows these dependency boundaries:

| Directory / assembly | Responsibility | Allowed dependencies |
|---|---|---|
| `Core` / `Loom.Core` | Deterministic domain logic and immutable runtime data | .NET/BCL only |
| `Fmod` / `Loom.Fmod` | FMOD clocks, synthesis, routing, and native error handling | `Loom.Core`, FMOD |
| `Unity` / `Loom.Unity` | MonoBehaviours, lifecycle, input, and authoring adapters | `Loom.Core`, `Loom.Fmod`, Unity |
| `Demo` / `Loom.Demo` | Disposable playground UI and scene behavior | All runtime LOOM assemblies |
| `Tests/EditMode` | Fast domain and adapter tests | Required runtime targets and NUnit |
| `Tests/PlayMode` | Runtime, audio, lifecycle, and scene integration tests | Required runtime targets and NUnit |

Rules:

- A namespace mirrors the directory below `Assets/Scripts/Loom`, for example `Core` maps to
  `Loom.Core` and `Tests/EditMode` maps to `Loom.Tests.EditMode`.
- Every assembly definition declares the same value for `name` and `rootNamespace`.
- `Loom.Core` must keep `noEngineReferences: true` and must not reference Unity, FMOD, or another
  runtime LOOM assembly.
- Unity `ScriptableObject` assets are authoring adapters. Convert their data into immutable or
  explicitly owned Core models before playback.
- Do not add direct references that skip or reverse the declared dependency direction.

## 4. File and asset naming

| Artifact | Convention | Example |
|---|---|---|
| C# source | `PascalCase.cs`; the file name matches at least one top-level type | `VoiceHandle.cs` |
| Interface | `I` + `PascalCase` | `IInstrument` |
| Test fixture | `<Subject>Tests.cs` | `NoteContractTests.cs` |
| Assembly definition | Fully qualified assembly name | `Loom.Tests.EditMode.asmdef` |
| UXML, USS, ScriptableObject asset | `PascalCase` | `LoomSynthDemo.uxml` |
| LOOM scene | `scn_<lower_snake_case>.unity` | `scn_loom.unity` |
| Repository script | `lower_snake_case` | `check_coding_standards.py` |
| CI workflow | `lower-kebab-case.yml` | `coding-standards.yml` |

`Assets/Scenes/SampleScene.unity` is a legacy Unity template scene and is the only current exception
to the LOOM scene rule. New exceptions require an explicit checker allowlist entry and a documented
reason.

A file may contain a small, tightly coupled secondary type when separating it would obscure ownership.
The primary public or internal type still matches the file name. `AssemblyInfo.cs` is exempt because it
contains assembly-level attributes rather than a type.

Keep every Unity asset paired with its `.meta` file. Create, move, and rename Unity assets through the
Editor when possible; never regenerate unrelated metadata.

## 5. C# naming

| Symbol | Convention | Example |
|---|---|---|
| Namespace, class, struct, enum, delegate | `PascalCase` | `FmodOscillatorVoice` |
| Interface | `I` + `PascalCase` | `IOscillatorInstrumentVoice` |
| Method, property, event | `PascalCase` | `BeginRelease` |
| Constant | `PascalCase` | `DefaultVoiceCapacity` |
| Static readonly field | `PascalCase` | `WaveformChoices` |
| Private instance or mutable static field | `camelCase`, no underscore prefix | `voiceSlots` |
| Parameter and local | `camelCase` | `sampleRate` |
| Type parameter | `T` or `T` + descriptive name | `TElement` |

Treat acronyms as words in identifiers: use `Fmod`, `Adsr`, `Dsp`, `Midi`, and `Ui`. Preserve an
external library's spelling only when referencing its own symbol.

Use established suffixes to expose roles: `Controller`, `Settings`, `Exception`, `Tests`, `PlayModeTests`,
and `ContractTests`. Prefer names that describe domain meaning rather than implementation mechanics.

## 6. C# layout and language use

- Encode files as UTF-8 without a byte-order mark, use LF line endings, and end every file with one
  newline.
- Use four spaces for indentation. Do not use tabs or trailing whitespace.
- Keep C# lines at or below 120 characters. Split fluent calls and arguments before weakening names.
- Use block-scoped namespaces and Allman braces. Braces are required for every control-flow body.
- Put `using` directives before the namespace, with `System` directives first and no separated groups.
- Declare accessibility explicitly except on interface members.
- Use `var` when the type is apparent from object creation, a cast, or the right-hand side. Use an
  explicit type for built-ins and when it makes the code easier to scan.
- Prefer expression bodies only for single-line properties or accessors. Use block bodies for methods
  and constructors.
- Use one declaration per line. Keep related members together and order a type as constants, static
  state, instance state, constructors, properties, public methods, internal methods, private methods,
  then nested types where practical.
- Make concrete classes `sealed` unless inheritance is an intentional part of the design. Prefer
  immutable value types and get-only properties for Core data.
- Validate arguments at the public boundary and throw the narrowest contextual exception.
- Check every `FMOD.RESULT`. Include the failed operation and native result in surfaced errors.

The repository `.editorconfig` expresses the mechanically enforceable IDE and Roslyn preferences.
Do not duplicate or override those rules in a personal project configuration.

## 7. Determinism, timing, and performance

- Represent musical time with non-negative `long` ticks. Convert to samples or seconds only at an
  explicit boundary.
- Do not use `UnityEngine.Random`, `System.Random`, wall-clock time, or unordered collection iteration
  in deterministic Core logic.
- Derive random values from stable, named hash inputs and preserve deterministic same-tick ordering.
- Use FMOD DSP clocks as the only scheduling authority at the audio boundary.
- Keep scheduler and note-dispatch hot paths allocation-free after initialization. Preallocate bounded
  storage and make resource ownership explicit.
- A successful note-on returns or retains a stable voice handle. Note-off, stealing, panic, and disposal
  must have deterministic ownership semantics.
- Do not promise bit-identical PCM. The determinism contract covers the logical event stream.

## 8. Tests

- Put pure and adapter contract tests in `Tests/EditMode`; put runtime, scene, lifecycle, and audible
  integration tests in `Tests/PlayMode`.
- Name fixtures `<Subject>Tests` and test methods as behavior statements in `PascalCase`, for example
  `NoteRejectsMidiNumbersOutsideRange`.
- Use Arrange/Act/Assert through whitespace and clear names; do not add section comments when the test
  remains obvious.
- Cover boundaries, invalid values, ownership, deterministic ordering, idempotent cleanup, and failure
  cleanup for every new contract.
- Run the narrowest relevant tests first, followed by the complete milestone-level suite.
- Compilation is insufficient for audio changes. Record audible output, routing, note-off, polyphony,
  timing, lifecycle, and Console evidence as applicable.

## 9. Automated enforcement

Run the repository checker from the root:

```bash
python3 scripts/check_coding_standards.py
```

The checker has no third-party dependencies. It verifies:

- UTF-8, LF, final newlines, indentation hygiene, trailing whitespace, and C# line length;
- C# file/type naming, namespace-to-directory mapping, block namespaces, and selected field naming;
- test fixture file names;
- assembly names, root namespaces, dependency lists, platforms, and the Core engine boundary;
- forbidden nondeterministic APIs in `Loom.Core`;
- UXML `name` and class naming;
- LOOM scene naming and Unity `.meta` parity.

GitHub Actions runs the same command for every push and pull request. A non-zero exit code blocks the
workflow. IDE diagnostics from `.editorconfig` cover richer syntax-aware rules that the dependency-free
checker intentionally does not try to parse.

When adding a rule:

1. Document the rule here.
2. Add `.editorconfig` support where Roslyn can enforce it.
3. Add a deterministic checker rule when it can be validated without parsing ambiguity.
4. Make the existing first-party tree pass; do not hide violations with broad exclusions.
5. Update the implementation plan with verification evidence.
