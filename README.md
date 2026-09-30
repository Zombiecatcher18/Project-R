## Project R
A turn-based RPG with a QTE-driven combo system, built in Unity (C#).
<img width="1919" height="895" alt="Screenshot 2026-01-08 120406" src="https://github.com/user-attachments/assets/f67cab06-eb11-4545-8322-81031ac8756f" />

## About the Project
Project R is an original RPG in active development, featuring a combat system where players build a combo from a hand of moves, then execute each move through a timed input challenge (QTE). Combat rewards timing, sequencing, and reading enemy patterns — not just raw stats.

Development is ongoing, with a public demo planned for Steam and itch.io.

## Systems Implemented
Turn-Based Battle Loop
Alternating player/enemy turns with a configurable turn pattern

Turn meter — a tug-of-war resource between player and enemy momentum

Full battle lifecycle: setup → combat → victory/defeat → return to overworld

## QTE Combo System
Player selects a sequence of moves from a move deck, limited by slot cost

Each move in the combo is executed through a timed QTE (Timing bar or Button prompt)

Per-move QTE results (Perfect / Good / Ok / Miss) determine damage multipliers

Combo-end bonuses for synergy, finisher moves, and all-perfect execution

## Move System
AttackMove ScriptableObject data model with:

Move categories (Attack, Heal, Buff, Debuff, Setup, Finisher, Utility)

Good/bad synergy lists — moves that combo well or poorly with each other

QTE configuration (type, speed, pre-delay, button pool)

Slot cost for combo economy

PlayerMoveDeck for hand management and draws

## Enemy AI (Prediction System)
EnemyRuntimeData.PatternTracker builds awareness of the player's behavior over time

Tracks move spam, opener repeats, finisher repeats, combo signatures, and QTE timing

When awareness crosses a threshold, prediction activates — enemies begin countering the player's repeated patterns

Counter pressure scales with predictability, feeding into enemy counter rolls

Personality profiles modify all AI multipliers (awareness gain, prediction decay, counter curves, meter influence)

## Synergy Chains
Moves reference each other for good/bad synergy bonuses

Enemies track turn-level synergy scores that influence targeting and emotional reactions

Enemy counters spike party synergy, creating a feedback loop between enemy success and enemy aggression

Progression
Bounty system — enemies award bounty on death, which accumulates toward level-ups

Level-ups restore HP and increase bounty requirements

Rank system — bounty rank unlocks new moves, increases slots and deck size

Per-stat upgrade methods (HP, Attack, Defense, Counter Attack, Critical Chance, Turn Control)

## UI & Feedback
Slider-based health bars with smooth interpolation

Floating damage popups with ease-in / hold / fade-out curves

Synergy popups, counter popups, final-damage accumulation displays

Battle text messaging with configurable durations

## Technical Highlights
Zero-allocation coroutine helpers — WaitCache reuses WaitForSeconds instances across calls to reduce garbage collection pressure during battle loops

Cached component references — Player controller, character controller, and camera references are cached at battle start to avoid repeated GetComponent/FindWithTag calls in hot paths

Re-entrancy guards — enemyTurnRunning and battleInitialized flags prevent duplicate coroutine execution and premature turn advancement

Runtime data cloning — EnemyRuntimeData instantiates EnemyInfo ScriptableObjects so per-battle mutations (HP loss, stat changes, memory) don't corrupt the source asset

Defense formula — damage reduction uses 100 / (100 + defense) for diminishing returns

Personality-driven AI biases — all enemy behavior modifiers (aggression, execution preference, fakeout frequency, QTE speed) are multiplied through EnemyPersonalityProfile values, allowing behavior to be tuned per-enemy without code changes

Pattern tracking with history trimming — the AI's memory uses fixed-size lists with TrimHistory() to bound memory growth over long battles

## Tech Stack
Engine: Unity 6000.3.0f1

Language: C#

Version Control: Git / GitHub

## Current Status
Demo in active development for Steam and itch.io. Development content: https://youtube.com/@zombiecatcher18?si=PfYEQViCB00UicuN

## Contact
GitHub: Zombiecatcher18

Portfolio: [link]

YouTube: https://youtube.com/@zombiecatcher18?si=PfYEQViCB00UicuN
