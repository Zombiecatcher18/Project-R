## Project R
A turn-based RPG with a QTE-driven combo system, built in Unity (C#).
<img width="1919" height="895" alt="Screenshot 2026-01-08 120406" src="https://github.com/user-attachments/assets/f67cab06-eb11-4545-8322-81031ac8756f" />

## About the Project

Project R is an original RPG in active development. Combat is built around **reading the enemy** — players assemble a combo from a hand of moves, execute each move through a timed QTE, and adapt when enemies learn their patterns. The system rewards timing, sequencing, and unpredictability over raw stats.

Development is ongoing, with a public demo planned for Steam and itch.io.

## Systems Implemented

### Turn-Based Battle Loop
- Alternating player/enemy turns with a configurable turn pattern
- **Turn meter** — a tug-of-war momentum resource between player and enemy
- Full battle lifecycle: setup → combat → victory/defeat → return to overworld
- Multi-enemy battles with target selection

### QTE Combo System
- Player selects a combo from a move deck, limited by **slot cost**
- Each move executes through a timed QTE (**Timing bar** or **Button prompt**)
- Per-move results (Perfect / Good / Ok / Miss) determine damage multipliers
- Multi-hit move support with per-hit resolution
- Combo-end bonuses for synergy links, finisher placement, and all-perfect execution
- Combo speed scales with combo length, increasing QTE difficulty as the combo grows

### Move System
- `AttackMove` ScriptableObject data model with:
  - Move categories (Attack, Heal, BuffAttack, BuffDefense, DebuffEnemy, Setup, Finisher, Utility)
  - **Good/bad synergy lists** — moves that combo well or poorly with each other
  - QTE configuration (type, speed, pre-delay, button pool)
  - **Slot cost** for combo economy
- `PlayerMoveDeck` for hand management and weighted draws

### Enemy AI — Pattern Awareness & Prediction
- `EnemyRuntimeData.PatternTracker` builds **awareness** of player behavior across turns:
  - Move spam, opener repeats, finisher repeats, combo signatures, QTE timing predictability
- When awareness crosses a threshold, **prediction mode** activates — enemies begin countering repeated player patterns
- **Counter pressure** scales with predictability and feeds into enemy counter rolls
- **Personality profiles** multiply all AI values (awareness gain, prediction decay, counter curves, meter influence) — behavior is tunable per enemy without code changes

### Enemy Combo System
- Each enemy has its own **move deck** with slot-cost limits
- **AI combo builder** constructs multi-move combos based on personality, synergy, and player state
- Enemies share a team slot bar and deck in multi-enemy battles
- Enemies do **not** perform QTEs themselves — their attacks auto-resolve with crit chances, while the player's QTEs are used for dodging and countering

### Fakeout System
- Some enemy moves can **fake out** — the windup looks identical to a real attack
- Fakeout frequency scales with the player's demonstrated skill (perfect counters tracked over time)
- If the player reacts to a fakeout, they're punished with an immediate follow-up attack
- If the player does nothing, the fakeout passes cleanly

### Synergy & Sabotage
- Enemy teams track **synergy scores** that influence targeting, aggression, and cooperative behavior
- Enemies with positive synergy chain attacks; enemies with negative synergy sabotage each other
- Player moves reference each other for good/bad synergy bonuses during a combo

### Enemy Personality System
- Multiple personality profiles (Aggressive, Trickster, Balanced, Adaptive, Defensive)
- Personality influences move weighting, QTE speed, fakeout frequency, counter behavior, and synergy reactions
- Biases decay over time to keep behavior dynamic

### Progression — Bounty System
- Enemies award **bounty** on death, which accumulates toward level-ups
- Level-ups restore HP and increase the next bounty requirement
- **Rank system** — bounty rank unlocks new moves, increases max slots and deck size
- Per-stat upgrade methods (HP, Attack, Defense, Counter Attack, Critical Chance, Turn Control)
- Higher bounty makes enemies more dangerous, up to per-enemy caps

### UI & Feedback
- Slider-based health bars with smooth interpolation
- Floating damage popups with ease-in / hold / fade-out curves
- Synergy popups, counter popups, and final-damage accumulation displays
- Battle text messaging with configurable durations
- Target indicator with per-enemy anchor points

## Technical Highlights

- **Zero-allocation coroutine helpers** — `WaitCache` reuses `WaitForSeconds` instances across calls to reduce GC pressure during battle loops
- **Cached component references** — player controller, character controller, and camera are cached at battle start to avoid repeated `GetComponent`/`FindWithTag` calls in hot paths
- **Re-entrancy guards** — `enemyTurnRunning` and `battleInitialized` flags prevent duplicate coroutine execution and premature turn advancement
- **Runtime data cloning** — `EnemyRuntimeData` instantiates `EnemyInfo` ScriptableObjects so per-battle mutations (HP loss, stat changes, memory) don't corrupt the source asset
- **Defense formula** — damage reduction uses `100 / (100 + defense)` for diminishing returns
- **Personality-driven AI biases** — all enemy behavior modifiers multiply through `EnemyPersonalityProfile` values, allowing tuning without code changes
- **Pattern tracking with history trimming** — the AI's memory uses fixed-size lists with `TrimHistory()` to bound memory growth over long battles
- **Decoupled move data** — `AttackMove` as ScriptableObjects allows moves to be authored, balanced, and linked for synergy without touching code

## Tech Stack

- **Engine:** Unity 6000.3.0f1
- **Language:** C#
- **Version Control:** Git / GitHub

## Getting Started

1. Clone this repository.
2. Open in Unity Hub with version **6000.3.0f1**.
3. Open the main battle scene to test the combat loop.

## Current Status

Demo in active development for Steam and itch.io. Development content: [YouTube — @zombiecatcher18](https://youtube.com/@zombiecatcher18)

## Contact

- **GitHub:** [github.com/Zombiecatcher18](https://github.com/Zombiecatcher18)
- **YouTube:** [youtube.com/@zombiecatcher18](https://youtube.com/@zombiecatcher18)
- **PORTFOLIO:** https://zombiecatcher18.github.io/Zombiecather18.github.io/
