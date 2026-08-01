# LOOM — adaptacyjny silnik muzyczny dla Unity

**Wersja:** 0.1 (draft)
**Cel:** reużywalny system proceduralnej muzyki sterowanej mechaniką gry
**Platformy:** PC / konsole
**Stack:** Unity + FMOD (Studio API do miksera i FX, Core API do sekwencjonowania)

---

## 1. Czym to jest, a czym nie jest

**Jest:** silnikiem, w którym mechanika gry mutuje stan sekwencerów, a te generują pełny, wielościeżkowy utwór w czasie rzeczywistym. Muzyka jest funkcją stanu gry, nie playlistą reagującą na stan gry.

**Nie jest:** DAW-em, narzędziem dla gracza-kompozytora, ani systemem odtwarzania gotowych stemów z crossfade'em.

### 1.1 Wymagania (ustalone)

| # | Wymaganie | Konsekwencja architektoniczna |
|---|---|---|
| R1 | Gracz nie edytuje nut wprost — robi to mechanika przez zdarzenia | Warstwa mutacji jako jedyne publiczne API |
| R2 | Silnik musi umieć wszystko na poziomie pojedynczej nuty | Model danych pełny, nawet jeśli gra używa 10% |
| R3 | Timing kwantyzowany **i** swobodny | Kwantyzacja to pole mutacji, nie globalny tryb |
| R4 | Pełny determinizm: ten sam stan gry = ta sama muzyka | Zegar całkowitoliczbowy, RNG bezstanowy, stan = seed + log |
| R5 | Wiele ścieżek autorskich (kod, edytor, MIDI, proceduralnie) | Jeden kanoniczny model danych, wiele frontendów |
| R6 | Forma: zarówno ewoluujący loop, jak i sekcje sterowane grą | Sekcje jako opcjonalna warstwa nad sekwencerami |
| R7 | Wyrazistość reakcji konfigurowalna per mechanika | Profil mutacji z parametrem „głośności" zmiany |
| R8 | Reużywalne w grach zręcznościowych, logicznych, strategicznych | Rdzeń bez zależności od Unity poza cienką warstwą adaptera |
| R9 | Na start: ma działać. Polerowanie później | MVP bez narzędzi edytorskich, ale z czystymi granicami warstw |

---

## 2. Zasady naczelne

Trzy decyzje, od których nie odstępuję, bo doklejenie ich później jest niewykonalne:

**Z1. Czas jest liczbą całkowitą.** Wszystko w systemie operuje na `long tick`. Konwersja na sekundy/próbki następuje wyłącznie na styku z FMOD. Akumulacja floatów dryfuje i po kilkunastu minutach zabija determinizm.

**Z2. Losowość jest bezstanowa i haszowana.** Nigdzie nie ma strumienia PRNG. Każda „losowa" wartość to `hash(seed, trackId, tick, slot)`. Dzięki temu wczytanie save'a w takcie 47 daje bitowo identyczny rezultat bez odtwarzania historii.

**Z3. Rdzeń nie wie, czym gra dźwięki.** Sekwencer emituje abstrakcyjne `NoteEvent`. Backend (FMOD sample, własny syntezator, cokolwiek) implementuje `IInstrument`. Zmiana silnika brzmienia = jedna klasa.

Dodatkowo, mniej twarda, ale ważna:

**Z4. Nuty są zapisywane jako stopnie skali, nie jako wysokości absolutne.** Pozwala to globalnie transponować i reharmonizować cały utwór jednym ruchem, bez dotykania patternów.

---

## 3. Architektura — przegląd

```
┌─────────────────────────────────────────────────────────┐
│  GRA (dowolny typ)                                      │
│  wywołuje: loom.Apply(new Mutation { ... })             │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│  L5  MUTATION LAYER                                     │
│  kolejka mutacji, kwantyzacja, profil wyrazistości,     │
│  log mutacji (= zapis stanu)                            │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│  L4  CONDUCTOR                                          │
│  skala, progresja akordów, sekcja utworu, intensywność  │
│  rozstrzyga: stopień skali → konkretna wysokość         │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│  L3  SEQUENCERS (N tracków)                             │
│  lead / bass / drums / pad / ...                        │
│  każdy: własny pattern, długość, playhead, parametry    │
└────────────────────────┬────────────────────────────────┘
                         │  NoteEvent (tick, degree, vel, dur, params)
┌────────────────────────▼────────────────────────────────┐
│  L2  SCHEDULER                                          │
│  lookahead ~200 ms, tick → dspClock, kolejka zdarzeń    │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│  L1  INSTRUMENTS (IInstrument)                          │
│  FmodSampleInstrument │ NativeSynthInstrument (później) │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│  L0  FMOD MIXER                                         │
│  busy per track, sendy, reverb/shimmer, snapshoty       │
└─────────────────────────────────────────────────────────┘

        ┌──────────────────────────────────────┐
        │  TRANSPORT (przecina wszystkie L)    │
        │  tick counter, tempo map, PPQN       │
        └──────────────────────────────────────┘
```

---

## 4. Transport i zegar

### 4.1 Jednostki

```csharp
public const int PPQN = 960;              // ticków na ćwierćnutę
public struct MusicalTime {
    public long Tick;                     // absolutny, od startu sesji
    public int  Bar   => ...;
    public int  Beat  => ...;
}
```

`PPQN = 960` daje rozdzielczość ~0.5 ms przy 120 BPM — więcej niż wystarczy, a mieści się w `long` na setki godzin.

### 4.2 Tempo map

Tempo nie jest stałą. Trzymam listę zmian tempa `(tick, bpm)` i konwersję tick↔sekundy liczę przez sumowanie segmentów. Dla MVP: jedno tempo, ale API od razu przez tempo mapę, żeby później nie przepisywać.

### 4.3 Scheduler z wyprzedzeniem

Pętla na wątku głównym, wywoływana w `Update()`:

```
1. Odczytaj bieżący dspClock z FMOD
2. Policz tick odpowiadający (dspClock + LOOKAHEAD)
3. Dla każdego tracka: wygeneruj wszystkie zdarzenia w oknie (lastScheduled, targetTick]
4. Przekaż je do instrumentów z docelowym dspClock
5. lastScheduled = targetTick
```

`LOOKAHEAD = 200 ms`. Spike na klatce trwający 150 ms nie powoduje żadnej dziury w dźwięku. To jest cała obrona przed GC i zacięciami — nie potrzebuję własnego wątku audio.

### 4.4 Dlaczego FMOD Core, a nie tylko Studio

**To jest kluczowa decyzja techniczna.** FMOD Studio API planuje zdarzenia z dokładnością do bloku miksera (kilka–kilkanaście ms) — dla perkusji to słyszalny flam.

FMOD **Core** API daje `ChannelControl.setDelay(dspClockStart, dspClockEnd)` — planowanie **próbka po próbce**. Rytm jest wtedy idealny.

**Rozwiązanie hybrydowe:**
- **Core API** → odtwarzanie głosów sekwencera (sample-accurate)
- **Studio API** → mikser, busy, efekty, snapshoty, parametry globalne
- Kanały Core routowane do busów Studio przez `ChannelGroup`

Kosztuje to trochę więcej kodu przy starcie, ale bez tego rytm nigdy nie będzie ciasny.

---

## 5. Determinizm

### 5.1 Losowość haszowana

```csharp
public static class Rng {
    // splitmix64 finalizer — dobra lawina, ~5 ns
    public static ulong Hash(ulong seed, ulong a, ulong b, ulong c) {
        ulong x = seed;
        x ^= a * 0x9E3779B97F4A7C15UL; x = Mix(x);
        x ^= b * 0xBF58476D1CE4E5B9UL; x = Mix(x);
        x ^= c * 0x94D049BB133111EBUL; x = Mix(x);
        return x;
    }
    static ulong Mix(ulong z) {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
    public static float Float01(ulong h) => (h >> 40) * (1f / 16777216f);
}
```

Użycie — probabilistyczny trigger kroku:

```csharp
float roll = Rng.Float01(Rng.Hash(songSeed, trackId, (ulong)tick, SLOT_TRIGGER));
if (roll < step.Probability) EmitNote(...);
```

Każde zastosowanie losowości dostaje własny stały `SLOT_*`, żeby dwa różne użycia w tym samym ticku nie były skorelowane.

### 5.2 Zapis stanu

```csharp
[Serializable] public class LoomSave {
    public ulong Seed;
    public long  StartTick;
    public List<MutationRecord> Log;   // (tick, verb, target, payload)
}
```

Odtworzenie: ustaw seed, przewiń log, ustaw playhead. Kilka kilobajtów zamiast serializacji całych sekwencerów. Dostajesz przy okazji powtórki i synchronizację sieciową za darmo.

**Higiena log-u:** mutacje idempotentne (`SetX`) zamiast przyrostowych (`AddX`) wszędzie, gdzie to możliwe. Log da się wtedy kompaktować — zostawiasz ostatnią mutację na dany target.

### 5.3 Czego pilnować

- Zero `UnityEngine.Random`, zero `System.Random`, zero `DateTime.Now`
- Zero iteracji po `Dictionary` w kolejności wyliczeniowej — tylko posortowane listy
- Kolejność aplikowania mutacji w tym samym ticku: stabilne sortowanie po `(priority, sequenceNumber)`
- Float w logice muzycznej jest OK dla save/load na tej samej platformie. Dla lockstep multiplayera cross-platform trzeba by przejść na fixed-point — **odkładam to, ale nie zamykam drogi**: parametry trzymam jako `float`, ale kwantyzuję je do 1/1000 przy zapisie do logu

---

## 6. Model danych

Jeden kanoniczny zestaw typów. Wszystkie frontendy autorskie (kod, edytor, MIDI import, generator) produkują **te same** obiekty.

```csharp
public struct Step {
    public sbyte Degree;        // stopień skali; -128 = pauza
    public sbyte Octave;        // przesunięcie oktawowe
    public byte  Velocity;      // 0-127
    public ushort Duration;     // w tickach
    public float Probability;   // 0-1
    public byte  Condition;     // np. "co 2. przebieg", "1 z 4"
    public byte  Ratchet;       // powtórzenia w obrębie kroku
    public ParamLock[] Locks;   // opcjonalne, zwykle null
}

public class Pattern : ScriptableObject {
    public Step[] Steps;
    public int StepsPerBeat = 4;   // rozdzielczość
    public int Length;              // liczba kroków (≠ 16! patrz polimetria)
}

public class Scale : ScriptableObject {
    public int[] Intervals;         // np. [0,2,3,5,7,8,10] eolska
    public string Name;
}

public class HarmonyPlan : ScriptableObject {
    public ChordStep[] Chords;      // (root, quality, długość w taktach)
}

public class TrackDefinition : ScriptableObject {
    public string Id;               // "lead", "bass", "drums"
    public InstrumentBinding Instrument;
    public Pattern[] PatternBank;
    public TrackRole Role;          // wpływa na to, jak Conductor rozstrzyga nuty
}
```

**Polimetria za darmo:** każdy track ma własną `Length`. Bass o 16 krokach i lead o 12 rozjeżdżają się i wracają do siebie co 48 — bez żadnego dodatkowego kodu. To jeden z tańszych sposobów na „muzyka nigdy się nie powtarza".

---

## 7. Conductor

Warstwa, która sprawia, że wszystkie tracki brzmią razem, a nie obok siebie.

### 7.1 Odpowiedzialności

```csharp
public class Conductor {
    public Scale CurrentScale;
    public HarmonyPlan Harmony;
    public int Section;              // indeks sekcji (opcjonalne)
    public float Intensity;          // 0-1, globalny sterownik

    // rdzeń: stopień skali → MIDI note
    public int Resolve(int degree, int octave, long tick, TrackRole role);
}
```

`Resolve` uwzględnia:
- aktualną skalę
- akord obowiązujący w tym takcie (z `HarmonyPlan`)
- rolę tracka — **bass** przyciąga do prymy/kwinty akordu, **lead** ma swobodę, **pad** wybiera składniki akordu

To jest miejsce, w którym „muzyka zawsze brzmi ok". Nawet jeśli mechanika gry wygeneruje kompletnie losowy stopień, Conductor umieści go w harmonii.

### 7.2 Poziom rygoru

```csharp
public float HarmonicStrictness;  // 0 = puszczam wszystko, 1 = tylko składniki akordu
```

Konfigurowalne, bo w niektórych grach rozjazd harmoniczny jest narzędziem (gracz przegrywa → muzyka się rozpada). Ustawiasz `Strictness` na 0.2 i dostajesz kontrolowany chaos.

### 7.3 Forma utworu (R6)

Sekcje są **opcjonalną warstwą**. Dwa tryby, oba obsługiwane:

- **Tryb Organic** — brak sekcji. Conductor tylko trzyma harmonię, forma emerguje z mutacji i polimetrii. Domyślny dla gier logicznych/strategicznych.
- **Tryb Sectional** — `SongStructure` definiuje sekcje (`intro`, `build`, `peak`, `breakdown`), każda z zestawem aktywnych tracków, banków patternów i zakresów parametrów. Gra przełącza sekcje, przejścia kwantyzowane do taktu. Domyślny dla gier zręcznościowych.

Implementacyjnie sekcja to po prostu **preset mutacji**, aplikowany naraz. Nie potrzebuje osobnego silnika.

---

## 8. Warstwa mutacji — serce systemu

To jedyne publiczne API dla gry.

### 8.1 Struktura

```csharp
public struct Mutation {
    public MutationVerb Verb;
    public TargetRef    Target;      // track / step / global
    public MutationValue Value;
    public Quantize     Quantize;    // Immediate | NextBeat | NextBar | NextPhrase
    public Transition   Transition;  // Snap | Ramp
    public int          RampTicks;
    public byte         Priority;
    public MutationProfile Profile;  // wyrazistość (R7)
}
```

### 8.2 Słownik czasowników

| Kategoria | Verb | Przykład użycia w grze |
|---|---|---|
| **Nuty** | `SetStep`, `ToggleStep`, `ClearSteps` | Gracz stawia wieżę → dochodzi nuta w basie |
| | `TransposePattern`, `RotatePattern` | Zmiana poziomu → melodia się przesuwa |
| **Patterny** | `SwapPattern`, `MorphPattern` | Wejście do nowego biomu |
| **Track** | `SetTrackParam` (density, cutoff, level, swing) | Prędkość gracza → gęstość perkusji |
| | `MuteTrack`, `UnmuteTrack`, `SetTrackLength` | Zdobycie przedmiotu → wchodzi lead |
| **Harmonia** | `SetScale`, `SetChordProgression`, `SetStrictness` | Zmiana fazy gry / stan zagrożenia |
| **Globalne** | `SetIntensity`, `SetTempo`, `SetSection` | Ogólny stan napięcia |
| **Jednorazowe** | `TriggerOneShot`, `TriggerFill` | Trafienie, zebranie punktu |

### 8.3 Profil wyrazistości (R7)

Ta sama mutacja może być ledwo słyszalna albo być wydarzeniem. Profil steruje tym deklaratywnie:

```csharp
public struct MutationProfile {
    public float Prominence;      // 0-1
    public bool  AccentOnApply;   // uderzenie akcentujące moment zmiany
    public bool  FillBefore;      // przejście perkusyjne przed zmianą
    public float DuckOthers;      // chwilowe ściszenie reszty
}
```

Presety: `Whisper` (0.1, nic więcej), `Notice` (0.5, accent), `Announce` (1.0, fill + duck + accent).

Dzięki temu ta sama mechanika w grze zręcznościowej i logicznej brzmi zupełnie inaczej, bez zmiany kodu gry.

### 8.4 Kolejka i kwantyzacja

Mutacje trafiają do kolejki priorytetowej posortowanej po docelowym ticku. `Immediate` aplikuje się w najbliższym oknie schedulera. `NextBar` czeka na granicę taktu.

**Pułapka:** przy `Immediate` mutacja może dotyczyć nut już zaplanowanych w lookahead. Dwa wyjścia: albo akceptujesz 200 ms opóźnienia, albo implementujesz **anulowanie zaplanowanych zdarzeń**. Dla MVP: akceptuję opóźnienie, poza `TriggerOneShot`, który idzie poza sekwencerem, wprost do instrumentu.

---

## 9. Instrumenty

### 9.1 Interfejs

```csharp
public interface IInstrument {
    void NoteOn(in NoteEvent e, ulong dspClock);
    void NoteOff(int voiceId, ulong dspClock);
    void SetParam(ParamId id, float value, ulong dspClock);
    void AllNotesOff();
}
```

### 9.2 Backendy

**`FmodSampleInstrument`** (MVP)
- Pula kanałów Core API
- `Sound` per instrument, multisample z mapowaniem zakresów
- Pitch przez `setPitch`, obwiednia głośności przez `setVolumeRamp`
- Planowanie przez `setDelay` → sample-accurate
- Routing do `ChannelGroup` odpowiadającego busowi Studio

**`NativeSynthInstrument`** (faza 2)
- Plugin DSP w C++ ładowany przez FMOD
- 2 oscylatory (saw/pulse/tri) + SVF (TPT) + 2× ADSR + LFO
- Parametry sterowane tak samo jak wbudowane efekty FMOD
- Sens głównie dla **bass** i **lead** — nieskończony zakres wysokości, płynna zmiana barwy

**`GranularInstrument`** (faza 3, opcjonalna)
- Dla padów i tekstur, jeśli sample okażą się za mało żywe

### 9.3 Rekomendowany przydział

| Track | MVP | Docelowo |
|---|---|---|
| Drums | Sample | Sample (bez zmian — samples wygrywają) |
| Bass | Sample | Native synth |
| Lead | Sample | Native synth |
| Pad / Texture | Sample + FX | Granular |

---

## 10. Mikser FMOD

```
Master
├── MUS_Drums    ──┐
├── MUS_Bass     ──┤
├── MUS_Lead     ──┼──> send ──> RVB_Shimmer (return)
├── MUS_Pad      ──┤              [PitchShift +12] → [Reverb] → pętla 0.4 + LPF
└── MUS_FX       ──┘
```

- Jeden bus per track — daje mutacjom kontrolę nad poziomem i FX
- Jeden wspólny return shimmer, nie per-source
- Snapshoty do duckingu podczas dialogów/cutscenek
- Sidechain z kicka na pad przez `SetTrackParam` (tanie, wystarczy modulacja poziomu)

---

## 11. API dla gry — jak to wygląda z zewnątrz

```csharp
// setup
var loom = LoomEngine.Create(songDefinition, seed: worldSeed);
loom.Play();

// gracz zebrał przedmiot
loom.Apply(Mutation.SetTrackParam("lead", ParamId.Level, 1.0f)
    .Quantized(Quantize.NextBar)
    .WithProfile(MutationProfile.Announce));

// ciągły parametr: prędkość gracza → gęstość perkusji
loom.Apply(Mutation.SetTrackParam("drums", ParamId.Density, speed01)
    .Ramped(ticks: PPQN * 4)
    .WithProfile(MutationProfile.Whisper));

// gracz w niebezpieczeństwie
loom.Apply(Mutation.SetScale(Scales.Phrygian).Quantized(Quantize.NextPhrase));
loom.Apply(Mutation.SetStrictness(0.3f).Ramped(PPQN * 8));

// zapis
var save = loom.Serialize();
```

**Adapter dla gier bez własnego kodu muzycznego:** komponent `LoomBinding` w inspektorze, mapujący `UnityEvent` / wartość float z gry na mutację. Pozwala prototypować bez pisania linijki.

---

## 12. Wydajność i wątki

| Wątek | Co robi |
|---|---|
| Main | Logika gry, kolejka mutacji, scheduler z lookahead |
| FMOD mixer | Miks, efekty, pluginy DSP |
| FMOD stream | Streaming długich sampli |

Nie potrzebuję własnego wątku audio — lookahead 200 ms załatwia problem. Gdyby scheduler okazał się kosztowny (mało prawdopodobne przy <10 trackach), przenoszę go na `Job` z Burstem.

**Budżet na PC:** system powinien mieścić się w ~2% jednego rdzenia bez własnych pluginów DSP, ~5% z granulatorem.

**Alokacje:** ścieżka schedulera musi być zero-alloc. Wszystko na `struct` + preallokowane bufory, `NoteEvent` w ring bufferze.

---

## 13. Plan wdrożenia

### Faza 0 — szkielet (1–2 tyg.)
Transport, tick clock, Rng, jeden track, jeden pattern, `FmodSampleInstrument`, planowanie przez Core API.
**Kryterium sukcesu:** perkusja gra idealnie w rytm przez 10 minut bez dryfu.

### Faza 1 — wielotorowość (2–3 tyg.)
4 tracki, polimetria, Conductor ze skalą i akordami, stopnie skali zamiast pitchy, mikser FMOD z busami i shimmerem.
**Kryterium:** brzmi jak utwór, nie jak cztery loopy.

### Faza 2 — mutacje (2–3 tyg.)
Warstwa mutacji, kwantyzacja, profile wyrazistości, kolejka. Podpięcie do prostej testowej gry.
**Kryterium:** widać i słychać, że mechanika steruje muzyką.

### Faza 3 — determinizm i zapis (1 tydz.)
Log mutacji, serializacja, test: dwa uruchomienia z tym samym seedem i logiem dają identyczny render.
**Kryterium:** automatyczny test porównujący dwa nagrania offline.

### Faza 4 — treść (2–4 tyg.)
Import MIDI, generator proceduralny, banki patternów, presety brzmień. Prosty edytor patternów w Unity (na tym etapie: minimalny).

### Faza 5+ — opcjonalne
Native synth plugin. Granulator. Pełne narzędzia edytorskie. Dokumentacja i pakowanie.

**Do końca Fazy 3: ~7–9 tygodni** i masz działający, deterministyczny system w grze. Reszta to treść i jakość brzmienia.

---

## 14. Ryzyka

| Ryzyko | Waga | Mitygacja |
|---|---|---|
| Muzyka brzmi generycznie mimo poprawnej architektury | **Wysoka** | Największe ryzyko całego projektu. Architektura nie tworzy muzyki. Zaplanuj czas na strojenie i treść — minimum tyle samo, co na kod |
| Rytm nie jest ciasny | Średnia | Core API + `setDelay`, nie Studio. Zweryfikuj w Fazie 0 |
| Determinizm pęka na floatach | Średnia | Testy automatyczne od Fazy 3, kwantyzacja parametrów w logu |
| Mutacje `Immediate` mają 200 ms opóźnienia | Niska | Oddzielna ścieżka dla one-shotów, omijająca sekwencer |
| Over-engineering pod przyszłe gry, których nie ma | Średnia | Świadomie: buduj pod bieżącą grę, ale trzymaj granice warstw |

---

## 15. Świadomie odłożone

Zgodnie z „na start ma działać":

- Narzędzia edytorskie (poza minimalnym podglądem patternów)
- Native synth i granulator — dopiero gdy sample okażą się niewystarczające
- Fixed-point pod lockstep multiplayer — droga otwarta, ale nie idę nią teraz
- Eksport utworu gracza do pliku
- Wsparcie mobile/WebGL
- Dokumentacja i pakowanie jako asset

---

## 16. Pierwszy krok

Faza 0, konkretnie: `Transport` + `Rng` + jeden `FmodSampleInstrument` grający hi-hat na ćwierćnutach przez Core API z `setDelay`. Zostaw to na 10 minut i porównaj z metronomem.

Jeśli to nie dryfuje — fundament jest dobry i cała reszta to nadbudowa.
