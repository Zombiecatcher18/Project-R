using UnityEngine;

/// <summary>
/// Lightweight AI personality-based move selection adapter for enemies.
/// Serves as the primary interface between EnemyAttackController and personality-adjusted move mechanics.
/// Heavy logic delegated to PersonalityMoveAdapter for complexity isolation and reusability.
/// 
/// Usage Flow:
/// 1. EnemyAttackController calls GetMoveForUse(EnemyMove baseMove)
/// 2. EnemyBrain fetches personalityProfile (assigned per-enemy in scene)
/// 3. Delegates to PersonalityMoveAdapter.GetMoveForPersonality() for actual adjustments
/// 4. Returns modified move with personality-applied damage/tags/mechanics
/// 
/// References: Used by EnemyAttackController.ExecuteCombo() for move adaptation.
/// Cross-System: Reads EnemyPersonalityProfile for bias/trait configuration.
/// </summary>
public class EnemyBrain : MonoBehaviour
{
    // ===== AI PERSONALITY CONFIGURATION =====
    /// <summary>
    /// The personality profile driving this enemy's move selection and behavior.
    /// Assigned per-enemy instance to enable diverse AI playstyles from same enemy type.
    /// References PersonalityProfile data with bias, trait, and move preference systems.
    /// </summary>
    [Header("AI Personality")]
    public EnemyPersonalityProfile personalityProfile;

    // ===== MOVE SELECTION =====
    /// <summary>
    /// Returns personality-adjusted move for this AI opponent.
    /// 
    /// If personalityProfile is assigned: delegates to PersonalityMoveAdapter for full adaptation
    ///   - Applies damage multipliers based on personality bias
    ///   - Modifies move tags according to preference system
    ///   - Adjusts QTE difficulty if move has QTE component
    /// If personalityProfile is null: returns baseMove unchanged (fallback safety mechanism)
    /// 
    /// Called by: EnemyAttackController.ExecuteCombo()
    /// Depends on: PersonalityMoveAdapter (external adapter class)
    /// </summary>
    public EnemyMove GetMoveForUse(EnemyMove baseMove)
    {
        // Safety: if no personality is assigned, return the base move unchanged.
        if (personalityProfile == null || baseMove == null)
            return baseMove;

        // Delegates to PersonalityMoveAdapter (handles damage scaling, tag modifications, difficulty adjustments).
        return PersonalityMoveAdapter.GetMoveForPersonality(baseMove, personalityProfile);
    }
}
