using UnityEngine;

public static class BattleStrategyBrain
{
    public static void EvaluateTurn(EnemyRuntimeData runtime, PlayerStats player)
    {
        // reset
        runtime.strategy = default;

        float playerHpPct = (float)player.currentHealth / player.MaxHealth;
        float enemyHpPct  = (float)runtime.currentHP / runtime.maxHP;

        // 1. Player low HP → finish them
        if (playerHpPct <= 0.35f)
        {
            runtime.strategy.executeBias   += 0.6f;
            runtime.strategy.finisherBias  += 0.5f;
            runtime.strategy.aggressionBias += 0.3f;
            runtime.strategy.goAllInThisTurn = true;
        }

        // 2. Enemy low HP → play safer
        if (enemyHpPct <= 0.3f)
        {
            runtime.strategy.defenseBias   += 0.6f;
            runtime.strategy.fakeoutBias   += 0.3f;
            runtime.strategy.saveBigMovesThisTurn = true;
        }

        // 3. Player has armor → value armor-piercing
        if (player.defense > 0)
        {
            runtime.strategy.armorPierceBias += 0.7f;
        }

        // 4. Took big damage last turn → get angry
        if (runtime.damageTakenLastTurn >= runtime.maxHP * 0.2f)
        {
            runtime.strategy.aggressionBias += 0.4f;
        }

        // 5. High synergy → lean into roles (simple hook for now)
        if (runtime.synergyLevel >= 2)
        {
            runtime.strategy.aggressionBias += 0.2f;
            runtime.strategy.fakeoutBias    += 0.2f;
        }
    }
}

