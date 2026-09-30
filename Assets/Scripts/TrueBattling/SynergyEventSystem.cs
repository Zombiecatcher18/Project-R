using UnityEngine;

public static class SynergyEventSystem
{
    public static void EvaluateEvent(EnemyAttackController actor, EnemyMove move, BattleManager battle)
    {
        if (actor == null || actor.runtime == null)
            return;

        var runtime = actor.runtime;
        var personality = runtime.personality;
        if (personality == null || move == null)
            return;

        float synergy = runtime.synergyScore;
        var type = personality.personalityType;

        // ---------------------------------------------------------
        // 1. TEAM-UP EVENTS (High synergy)
        // ---------------------------------------------------------
        if (synergy >= 1.0f)
        {
            if (move.HasTag(MoveTag.ArmorPiercing) || move.ignoresDefense)
                Debug.Log($"[SynergyEvent] {actor.GetEnemyName()} coordinates a precision strike! Allies prepare follow-up.");

            if (move.isMultiHit)
                Debug.Log($"[SynergyEvent] {actor.GetEnemyName()} unleashes a combo that hypes the team!");
        }

        // ---------------------------------------------------------
        // 2. PRAISE EVENTS (Moderate synergy)
        // ---------------------------------------------------------
        if (synergy > 0.25f && synergy < 1.0f)
        {
            if (type == EnemyPersonalityType.MilitaryAdmiral && move.ignoresDefense)
                Debug.Log($"[SynergyEvent] Admiral approves: '{actor.GetEnemyName()} executed that perfectly.'");

            if (type == EnemyPersonalityType.Explorer && move.isMultiHit)
                Debug.Log($"[SynergyEvent] Explorer cheers: '{actor.GetEnemyName()} is getting wild! I love it!'");
        }

        // ---------------------------------------------------------
        // 3. JUDGMENT EVENTS (Low synergy)
        // ---------------------------------------------------------
        if (synergy < -0.25f && synergy > -1.5f)
        {
            if (type == EnemyPersonalityType.ArrogantNoble)
                Debug.Log($"[SynergyEvent] Arrogant Noble scoffs at {actor.GetEnemyName()}: 'Such sloppy technique… embarrassing.'");

            if (type == EnemyPersonalityType.MilitaryAdmiral && move.hasFakeout)
                Debug.Log($"[SynergyEvent] Admiral disapproves: 'Fakeouts? Stick to discipline, {actor.GetEnemyName()}.'");
        }

        // ---------------------------------------------------------
        // 4. ANTI-SYNERGY EVENTS (Nemesis pairs)
        // ---------------------------------------------------------
        if (runtime.forceSabotageThisTurn)
        {
            if (type == EnemyPersonalityType.Explorer)
                Debug.Log($"[SynergyEvent] Explorer ignores {actor.GetEnemyName()}: 'You're boring! I'm doing my own thing!'");

            if (type == EnemyPersonalityType.ArrogantNoble)
                Debug.Log($"[SynergyEvent] Arrogant Noble snaps: 'Ugh! Must I fight alongside this… creature?'");
        }

        // ---------------------------------------------------------
        // 5. SABOTAGE EVENTS (Active sabotage behavior)
        // ---------------------------------------------------------
        if (runtime.forceSabotageThisTurn)
        {
            if (type == EnemyPersonalityType.Explorer)
                Debug.Log($"[SabotageEvent] Explorer disrupts the flow: 'Plans? Structure? Nah, I'm freestyling!'");

            if (type == EnemyPersonalityType.ArrogantNoble)
                Debug.Log($"[SabotageEvent] Arrogant Noble fumes: 'Your incompetence is ruining my image!'");
        }
    }
}
