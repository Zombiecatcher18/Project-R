using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Triggers enemy encounter and initiates battle scene transition.
/// Attached to trigger collider in exploration scenes to define battle encounter areas.
/// 
/// Encounter System:
/// 1. Player enters trigger zone (detected via OnTriggerEnter with Player tag)
/// 2. Encounter fires once (prevented by `triggered` flag)
/// 3. Populates BattleData with enemy group and arena settings
/// 4. Transitions to BattleScene for combat
/// 
/// Integration:
/// - enemyGroup: Array of EnemyInfo scriptable objects defining enemy party composition
/// - BattleData: Static data passed to battle scene (enemyParty, arenaColor, returnPosition)
/// - SceneManager: Loads BattleScene for combat
/// 
/// Usage Pattern:
/// 1. Create empty GameObject with trigger collider in exploration scene
/// 2. Attach EnemyEncounter component
/// 3. Assign enemyGroup in inspector with desired enemies (1-4 typical)
/// 4. Set trigger collider as Trigger (not solid)
/// 5. Player enters trigger → battle begins
/// </summary>
public class EnemyEncounter : MonoBehaviour
{
    /// <summary>
    /// Array of enemy configuration templates for this encounter.
    /// Each element is EnemyInfo scriptable object defining stats, moves, level scaling.
    /// Multiple enemies in array create multi-enemy battle (party).
    /// Example: [Goblin_Weak, Goblin_Weak, Orc_Normal] = 2 goblins + 1 orc
    /// </summary>
    public EnemyInfo[] enemyGroup;

    /// <summary>
    /// Prevent duplicate encounter triggers (one-shot flag).
    /// Set true on first trigger, prevents OnTriggerEnter from firing multiple times.
    /// Prevents player from re-entering trigger and spawning multiple battles.
    /// </summary>
    private bool triggered = false;

    /// <summary>
    /// Physics callback: Player entered trigger zone.
    /// 
    /// Actions (if first trigger):
    /// 1. Set triggered = true (one-shot prevention)
    /// 2. Validate player tag (safety check)
    /// 3. Set BattleData.arenaColor from first enemy (visual theme)
    /// 4. Set BattleData.playerReturnPosition to player position (for post-battle spawn)
    /// 5. Load BattleScene for combat
    /// 
    /// Called by: Physics system on trigger enter event
    /// Result: Immediate scene transition to battle (non-blocking)
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;  // Already triggered, prevent duplicate

        if (other.CompareTag("Player"))
        {
            triggered = true;

            // Setup battle data for scene transition
            BattleData.arenaColor = enemyGroup[0].capsuleColor;  // Arena visual theme from first enemy
            BattleData.playerReturnPosition = other.transform.position;  // Return position after battle ends

            // TODO: Uncomment when party system fully integrated
            // EnemyParty party = new EnemyParty(enemyGroup);
            // BattleData.enemyParty = party;

            SceneManager.LoadScene("BattleScene");
        }
    }
}
