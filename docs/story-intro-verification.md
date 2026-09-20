# Story intro verification — 2026-09-20

Verified in the open Unity 6000.3.10f1 editor using the relay; all temporary test objects and runtime clones were discarded by returning to Edit Mode.

- The final project compiled and the Unity console reported zero errors/exceptions.
- Deterministic calls on temporary TMP objects checked partial character alpha, full reveal before hold, hold dismissal, outgoing fades, second-line visibility, rich-text character counting, continued alpha animation after mesh resize, exactly one completion event, zero typing speed and cancellation without completion.
- A normal scene startup ran the entire 15-beat intro and restored gameplay automatically. During the intro, time scale was 0, player control was disabled and torch presentation was 0. After completion, time scale was 1 and player control was enabled.
- A virtual Input System keyboard Space event accelerated the observable reveal. This was simulated input, not a physical keyboard or gamepad test.
- A temporary replay used a cloned story asset and a 3-second transition. At the sampled midpoint, torch presentation was 0.4001, blackout alpha 0.5999, time scale 0 and fuel loss 0. Holding the virtual skip button through the transition kept control disabled and fuel unchanged. Releasing it restored control, time scale 1 and torch presentation 1.
- A disabled-at-start controller left time scale unchanged.
- Actual Game View captures were inspected for the black intro, a short beat, the longest beat wrapping across two rows, the torch transition and resumed gameplay. Story depth was corrected for the overlay canvas and saved in Edit Mode. The authored text colour and font were retained.
- All Polish lowercase/uppercase diacritics were found through the assigned font and CaveUI fallback after populating its missing glyphs.
- Handbook links resolve. Source/document whitespace checks pass. Full-repository whitespace checking also reports Unity-generated blank YAML values in serialized assets, including pre-existing imported fonts; those assets were preserved.

No persistent automated test suite was added. Physical controller devices, portrait aspect ratios and arbitrary future story lengths remain unverified.
