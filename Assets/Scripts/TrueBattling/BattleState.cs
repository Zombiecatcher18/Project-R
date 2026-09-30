/// <summary>
/// Enumeration of battle turn states in turn-based combat flow.
/// Used by BattleManager to control state machine and determine active player/enemy.
/// Defines complete combat lifecycle from battle start to completion.
/// </summary>
public enum BattleState
{
    /// <summary>Initial battle setup state; BattleManager initializes parties and prepares UI.</summary>
    START,
    /// <summary>Player is selecting moves and building combo; MoveSelectionUI is active.</summary>
    PLAYER_SELECTING_COMBO,
    /// <summary>Player is executing QTE for selected moves; QTEManager controls flow.</summary>
    PLAYER_QTE,
    /// <summary>Enemy turn processing; EnemyAttackController builds and executes enemy combo.</summary>
    ENEMY_TURN,
    /// <summary>Enemy is executing QTE if move requires it; player responds via QTEManager.</summary>
    ENEMY_QTE,
    /// <summary>Battle concluded; LevelUpUI shown and rewards distributed.</summary>
    END
}
