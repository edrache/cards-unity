# Config

ScriptableObject tuning assets live here.

Expected homes:

- `GameConfig`: slot count and gameplay sizing values.
- `AnimConfig`: DOTween timing and motion tuning.
- `CardVisualConfig`: card dimensions and visual measurements.

Gameplay and presentation code should receive config references rather than hardcoding tuning values.

Default assets are stored under `Assets/Resources/Config/`:

- `DefaultGameConfig`
- `DefaultAnimConfig`
- `DefaultCardVisualConfig`

Runtime systems should prefer serialized references assigned in scenes or prefabs. Bootstrap code may load the defaults with `Resources.Load<T>("Config/Default...")` until a dedicated composition root owns those references.
