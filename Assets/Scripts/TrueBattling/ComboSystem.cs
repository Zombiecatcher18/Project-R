using UnityEngine;

public static class CounterSystem
{
    public static bool TryTriggerCounter(EnemyAttackController controller, QTEResult result)
    {
        Debug.Log($"[Counter] QTE result received: {result}");

        // Only trigger on MISS
        if (result != QTEResult.Miss)
        {
            Debug.Log("[Counter] Result is not MISS → Counter aborted.");
            return false;
        }

        var runtime = controller.runtime;
        if (runtime == null)
        {
            Debug.LogError("[Counter] ERROR: runtime is NULL.");
            return false;
        }

        if (runtime.info == null)
        {
            Debug.LogError("[Counter] ERROR: runtime.info is NULL.");
            return false;
        }

        Debug.Log($"[Counter] Attempting counter for enemy: {runtime.info.name}");

        // ============================
        // 1. PERSONALITY COUNTER CHANCE
        // ============================
        float baseChance = runtime.personality != null
            ? runtime.personality.baseCounterChance
            : 0f;

        Debug.Log($"[Counter] Personality base counter chance = {baseChance:F2}");

        // ============================
        // 2. SYNERGY MODIFIER
        // ============================
        float synergyFactor = Mathf.Clamp(1f + runtime.synergyScore * 0.5f, 0.5f, 2f);
        Debug.Log($"[Counter] Synergy score = {runtime.synergyScore:F2}, synergy factor = {synergyFactor:F2}");

        float finalChance = Mathf.Clamp01(baseChance * synergyFactor);
        Debug.Log($"[Counter] Final counter chance after synergy = {finalChance:F2}");

        // ============================
        // 3. RANDOM ROLL
        // ============================
        float roll = Random.value;
        Debug.Log($"[Counter] Roll = {roll:F2}");

        if (roll > finalChance)
        {
            Debug.Log("[Counter] Counter FAILED (roll too high).");
            return false;
        }

        Debug.Log("[Counter] Counter SUCCESS (roll passed).");

        // ============================
        // 4. PICK COUNTER MOVE
        // ============================
        EnemyMove counterMove = PickCounterMove(runtime);

        if (counterMove == null)
        {
            Debug.LogWarning("[Counter] No counter-tagged move found → Counter aborted.");
            return false;
        }

        Debug.Log($"[Counter] Selected counter move: {counterMove.moveName}");

        // ============================
        // 5. EXECUTE COUNTER ATTACK
        // ============================
        Debug.Log("[Counter] Triggering counter attack via DoEnemyAttack()");

        BattleManager.Instances.playerWasCountered = true;

        controller.StartCoroutine(controller.DoEnemyAttack(counterMove, BattleManager.Instances));
        return true;
    }

    private static EnemyMove PickCounterMove(EnemyRuntimeData runtime)
    {
        Debug.Log("[Counter] Searching for counter moves in enemy deck...");

        EnemyMove bestFast = null;
        EnemyMove bestAny = null;

        foreach (var m in runtime.info.moves)
        {
            if (m == null)
            {
                Debug.Log("[Counter] Skipping NULL move.");
                continue;
            }

            if (!m.isCounter)
            {
                Debug.Log($"[Counter] Move {m.moveName} is NOT a counter move.");
                continue;
            }

            Debug.Log($"[Counter] Found counter move: {m.moveName}");

            // Prefer FAST counter moves
            if (m.HasTag(MoveTag.Fast))
            {
                Debug.Log($"[Counter] {m.moveName} is FAST counter move.");

                if (bestFast == null || m.baseDamage > bestFast.baseDamage)
                {
                    Debug.Log($"[Counter] {m.moveName} is new BEST FAST counter move.");
                    bestFast = m;
                }
            }

            // Track best overall counter move
            if (bestAny == null || m.baseDamage > bestAny.baseDamage)
            {
                Debug.Log($"[Counter] {m.moveName} is new BEST ANY counter move.");
                bestAny = m;
            }
        }

        if (bestFast != null)
        {
            Debug.Log($"[Counter] FINAL PICK = FAST counter move: {bestFast.moveName}");
            return bestFast;
        }

        if (bestAny != null)
        {
            Debug.Log($"[Counter] FINAL PICK = strongest counter move: {bestAny.moveName}");
            return bestAny;
        }

        Debug.LogWarning("[Counter] No counter moves found at all.");
        return null;
    }
}
