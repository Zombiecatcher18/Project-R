using UnityEngine;

public static class SynergyJudgmentSystem
{
    /// <summary>
    /// Evaluates how the actor's move aligns with their personality "philosophy"
    /// and adjusts synergyScore + sabotage flags in real time.
    /// </summary>
    public static void EvaluateJudgment(
        EnemyAttackController actor,
        EnemyMove move,
        BattleManager battle)
    {
        if (actor == null || actor.runtime == null)
            return;

        // Ignore Judgements from dead actors
        if (actor.IsDead())
        {
            Debug.Log($"[Judgment] SKIPPED — {actor.GetEnemyName()} is dead, no judgment applied.");
            return;
        }

        if (move == null)
        {
            Debug.Log($"[Judgment] SKIPPED — Move was null for {actor.GetEnemyName()}.");
            return;
        }

        var runtime = actor.runtime;
        var personality = runtime.personality;
        if (personality == null || move == null)
            return;

        float delta = 0f;
        var type = personality.personalityType;

        // ---------------------------------------------------------
        // PERSONALITY PHILOSOPHY RULES
        // ---------------------------------------------------------
        switch (type)
        {
            case EnemyPersonalityType.MilitaryAdmiral:
                if (move.ignoresDefense || move.HasTag(MoveTag.ArmorPiercing))
                {
                    delta += 0.15f;
                    Debug.Log("[Judgment] Admiral approves of disciplined strike → +0.15 synergy");
                }
                if (move.hasFakeout)
                {
                    delta -= 0.20f;
                    Debug.Log("[Judgment] Admiral disapproves of trickery → -0.20 synergy");
                }
                break;

            case EnemyPersonalityType.Explorer:
                if (move.isMultiHit || move.hasFakeout)
                {
                    delta += 0.15f;
                    Debug.Log("[Judgment] Explorer loves the chaos → +0.15 synergy");
                }
                if (move.isHeavy)
                {
                    delta -= 0.10f;
                    Debug.Log("[Judgment] Explorer bored by heavy predictable moves → -0.10 synergy");
                }
                break;

            case EnemyPersonalityType.CheapShot:
                if (move.hasFakeout)
                {
                    delta += 0.20f;
                    Debug.Log("[Judgment] CheapShot approves of deception → +0.20 synergy");
                }
                if (move.isHeavy)
                {
                    delta -= 0.10f;
                    Debug.Log("[Judgment] CheapShot hates brute force → -0.10 synergy");
                }
                break;

            case EnemyPersonalityType.Conqueror:
                if (move.HasTag(MoveTag.Execute) || move.baseDamage >= 18)
                {
                    delta += 0.20f;
                    Debug.Log("[Judgment] Conqueror thrilled by overwhelming force → +0.20 synergy");
                }
                if (move.hasFakeout)
                {
                    delta -= 0.15f;
                    Debug.Log("[Judgment] Conqueror hates trickery → -0.15 synergy");
                }
                break;

            case EnemyPersonalityType.NobleWarrior:
                if (move.isHeavy)
                {
                    delta += 0.15f;
                    Debug.Log("[Judgment] NobleWarrior respects a strong, honest strike → +0.15 synergy");
                }
                if (move.hasFakeout)
                {
                    delta -= 0.25f;
                    Debug.Log("[Judgment] NobleWarrior disgusted by dishonorable fakeout → -0.25 synergy");
                }
                break;

            case EnemyPersonalityType.BlindFighter:
                if (move.isCounter)
                {
                    delta += 0.20f;
                    Debug.Log("[Judgment] BlindFighter approves reactive technique → +0.20 synergy");
                }
                if (move.isHeavy)
                {
                    delta -= 0.10f;
                    Debug.Log("[Judgment] BlindFighter dislikes slow telegraphed moves → -0.10 synergy");
                }
                break;

            case EnemyPersonalityType.ArrogantNoble:
                if (move.isHeavy)
                {
                    delta += 0.10f;
                    Debug.Log("[Judgment] Arrogant Noble approves elegant heavy strike → +0.10 synergy");
                }
                if (move.isMultiHit || move.hasFakeout)
                {
                    delta -= 0.20f;
                    Debug.Log("[Judgment] Arrogant Noble offended by messy technique → -0.20 synergy");
                }
                break;
        }

        // ---------------------------------------------------------
        // APPLY SYNERGY CHANGE
        // ---------------------------------------------------------
        runtime.synergyScore += delta;
        runtime.synergyScore = Mathf.Clamp(runtime.synergyScore, -3f, 3f);

        Debug.Log($"[Judgment] New synergyScore for {actor.GetEnemyName()} = {runtime.synergyScore:F2}");

        // ---------------------------------------------------------
        // ESCALATE SABOTAGE IF SYNERGY DROPS TOO LOW
        // ---------------------------------------------------------
        if (runtime.synergyScore <= -1.5f && !runtime.forceSabotageThisTurn)
        {
            runtime.forceSabotageThisTurn = true;
            Debug.Log($"[Judgment] Sabotage escalated! {actor.GetEnemyName()} is now hostile to allies.");
        }

        // ---------------------------------------------------------
        // REPAIR SABOTAGE IF SYNERGY IMPROVES
        // ---------------------------------------------------------
        if (runtime.synergyScore > -1.0f && runtime.forceSabotageThisTurn)
        {
            runtime.forceSabotageThisTurn = false;
            Debug.Log($"[Judgment] Sabotage calmed — {actor.GetEnemyName()} is cooperating again.");
        }
    }
}
