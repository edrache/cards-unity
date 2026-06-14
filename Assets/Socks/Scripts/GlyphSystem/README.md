# GlyphSystem

System do wyświetlania glyphów kontrolera (gamepad/klawiatura) w UI. Oparty na Rewired, bez zależności od Zenject i MonsterCouch.Common.

## Wymagania

- Unity 6+ (URP lub Built-in)
- **Rewired** zainstalowany w projekcie
- Skonfigurowany Rewired `InputManager` z akcjami (np. `Jump`, `Confirm`, `Cancel`)

---

## Pliki

| Plik | Opis |
|------|------|
| `GameControlType.cs` | Enum: `Gamepad`, `MouseTouch`, `None` |
| `InputControllerState.cs` | Statyczny stan wejścia (`IsUsingGamepad`, `IsUsingTouch`) |
| `GlyphInputManager.cs` | MonoBehaviour — most między Rewired a systemem glyphów |
| `ControllerEntry.cs` | Serializable — zestaw glyphów dla jednego kontrolera (identyfikowany przez Rewired GUID) |
| `ControllerGlyphEntry.cs` | Serializable — jeden glyph: ID elementu + sprite + nazwa TMP sprite |
| `ControllerGlyphsProvider.cs` | MonoBehaviour — zwraca sprite/entry dla nazwy akcji Rewired |
| `ControllerGlyphDisplayer.cs` | MonoBehaviour — wyświetla glyph w `Image`, ukrywa się gdy brak gamepada |

---

## Setup krok po kroku

### 1. Skopiuj pliki do projektu

Wrzuć cały folder do `Assets/` swojego projektu. Upewnij się, że namespace `GlyphSystem` nie koliduje z istniejącym kodem (możesz go zmienić we wszystkich plikach).

### 2. Przygotuj assety glyphów

Potrzebujesz zestawu sprite'ów — po jednym na każdy przycisk kontrolera (np. `Xbox_A`, `Xbox_B`, `PS_Cross`, `Key_Space`).

### 3. Utwórz GameObject zarządzający systemem

Utwórz nowy GameObject (np. `GlyphSystem`) — najlepiej na scenie persistentnej lub DontDestroyOnLoad.

Dodaj do niego dwa komponenty:

#### `GlyphInputManager`
Nie wymaga żadnej ręcznej konfiguracji w Inspectorze — automatycznie pobiera Rewired Player 0 w `Awake`.

#### `ControllerGlyphsProvider`
Wymaga wypełnienia w Inspectorze:

- **Input Manager** — przeciągnij GameObject z `GlyphInputManager`
- **Controller Entries** — tablica zestawów glyphów, po jednym na kontroler:

  Każdy `ControllerEntry` ma:
  - **Name** — czytelna nazwa (np. `Xbox`, `DualSense`, `Switch`)
  - **Joystick** lub **Template** — asset Rewired `HardwareJoystickMap` / `HardwareJoystickTemplateMap`
    - Znajdziesz je w `Assets/Rewired/Data/` po rozwinięciu pliku `Rewired Input Manager`
    - Przypisz **tylko jeden** z tych dwóch pól
  - **Controller Glyphs** — tablica `ControllerGlyphEntry`:
    - **Element Identifier Id** — ID elementu kontrolera z Rewired (np. `0` = South Button na Xbox/PS)
    - **Element Identifier Name** — opcjonalna nazwa zapasowa (np. `"A"`)
    - **Glyph Icon** — sprite do wyświetlenia
    - **Text Sprite Name** — nazwa sprite'a w atlasie TMP (jeśli używasz glyphów w tekście)

- **Keyboard Mouse Entry** — osobny `ControllerEntry` dla myszy/klawiatury (bez Joystick/Template)

### 4. Dodaj `ControllerGlyphDisplayer` do elementów UI

Na każdym GameObject z `Image` który ma pokazywać glyph:

1. Dodaj komponent `ControllerGlyphDisplayer`
2. Wypełnij pola:
   - **Action Name** — nazwa akcji Rewired (dokładnie tak jak w Rewired Input Manager, np. `"Confirm"`)
   - **Glyph Image** — referencja do komponentu `Image` na tym samym lub dziecku GameObject
   - **Input Manager** — referencja do `GlyphInputManager`
   - **Glyphs Provider** — referencja do `ControllerGlyphsProvider`

Glyph Image jest automatycznie ukrywany (`enabled = false`) gdy gracz używa myszy/klawiatury.

---

## Jak działa lookup glyphów

```
Nazwa akcji Rewired ("Jump")
        ↓
GlyphInputManager pyta Rewired o aktywny kontroler + ID elementu
        ↓
ControllerGlyphsProvider szuka ControllerEntry pasującego do GUID kontrolera
        ↓
W ControllerEntry szuka ControllerGlyphEntry po ID elementu
        ↓
Zwraca Sprite
```

Jeśli aktywny kontroler nie ma dopasowania, system fallbackuje do domyślnego szablonu Rewired (Xbox/gamepad template).

---

## Obsługa konsol

`GlyphInputManager` zawiera GUID-y dla Xbox, PS5 i Nintendo Switch — metoda `GetControllerGuid(RuntimePlatform)` zwraca odpowiedni GUID. Jeśli celujesz w konkretną konsolę, możesz wymusić GUID w `ControllerGlyphsProvider.GetGlyphSprite()`:

```csharp
Guid ps5Guid = _inputManager.GetControllerGuid(RuntimePlatform.PS5);
Sprite sprite = _glyphsProvider.GetGlyphSprite("Confirm", ps5Guid);
```

---

## Rozszerzanie ControllerGlyphDisplayer

Klasa jest `virtual` — możesz dziedziczyć i nadpisać `RefreshGlyph()` żeby dodać własną logikę (np. pokazywanie tylko gdy przycisk jest zaznaczony):

```csharp
public class MyGlyphDisplayer : ControllerGlyphDisplayer
{
    [SerializeField] private Button _button;

    protected override void RefreshGlyph()
    {
        base.RefreshGlyph();
        // dodatkowa logika
    }
}
```
