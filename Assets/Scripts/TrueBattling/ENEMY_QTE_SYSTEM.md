# Enemy QTE Dodge System

This system allows enemies to perform attacks that require the player to perform a QTE (Quick Time Event) to dodge. The outcome depends on the player's QTE performance.

## How It Works

When an enemy attacks with QTE required:

1. **Perfect QTE** - Player gets a counter attack that damages the enemy
2. **Good QTE** - Player successfully dodges with no damage taken and no counter
3. **Miss QTE** - Attack connects, player takes damage

## Setting Up Enemy Moves

1. Create a new **Enemy Move** asset:
   - Right-click in Project > Create > Battle > Enemy Move
   - Configure the move properties

2. **Key Properties:**
   - `moveName` - Name of the attack (e.g., "Power Punch")
   - `baseDamage` - Base damage value (not used if QTE required)
   - `requiresQTE` - Toggle whether this attack needs a QTE to dodge
   - `qteType` - Choose between "Timing" or "ButtonPrompt"
   - `qteSpeed` - How fast the QTE progresses (higher = faster)
   - `possibleButtons` - Which keys the player can press (for ButtonPrompt only)
   - `damageOnMiss` - Damage player takes if they fail the QTE
   - `counterAttackDamage` - Damage from player's counter (if they get Perfect)

## Example Setup

**BasicPunch (Enemy Move)**
- Move Name: "Jab"
- Requires QTE: Yes
- QTE Type: ButtonPrompt
- QTE Speed: 1.0
- Possible Buttons: Space
- Damage On Miss: 10
- Counter Attack Damage: 12

**PowerSlam (Enemy Move)**
- Move Name: "Power Slam"
- Requires QTE: Yes
- QTE Type: Timing
- QTE Speed: 1.5 (faster timer window)
- Damage On Miss: 15
- Counter Attack Damage: 18

## Assigning Moves to Enemies

1. Open an **Enemy Data** asset (or create one)
2. In the "Moves" array, add your Enemy Move assets
3. The enemy will randomly select from these moves during battle

## How to Configure in Inspector

The system works automatically once moves are assigned. Enemy attacks are controlled by `EnemyAttackController` which:
- Randomly selects a move from the enemy's move list
- Triggers the appropriate QTE based on the move's settings
- Handles the results (damage, counter, etc.)

## Result Feedback

The battle text displays:
- Perfect dodge: "Perfect dodge! You counter for X damage!"
- Good dodge: "You dodged [Move Name]!"
- Miss: "[Move Name] connects! You take X damage!"

## Notes

- The QTE manager automatically displays the correct UI (Button Prompt or Timing) based on the move's QTE type
- Enemy health is synced with `BattleData.enemyCurrentHealth` and updates the health bar automatically
- If an enemy is defeated by a counter attack, the battle ends immediately
