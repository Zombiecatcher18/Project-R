using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Spawns and initializes enemy parties at battle start with dynamic scaling.
/// Orchestrates the entire enemy party creation pipeline:
/// 1. Reads BattleData.enemyPartyRuntime (enemy list populated before battle)
/// 2. Scales each enemy's level based on player level using weighted distribution
/// 3. Applies personality-based stat allocation bonuses
/// 4. Applies bounty scaling for additional difficulty
/// 5. Instantiates enemy GameObjects and initializes controllers
/// 
/// Key Systems Integrated:
/// - BattleData: Provides enemyPartyRuntime list of EnemyRuntimeData instances
/// - EnemyAttackController: Initialized with runtime data on each spawned enemy
/// - PlayerStats: Player level read for scaling calculations
/// - BountyManager: Optional bounty difficulty multiplier
/// - EnemyPersonalityProfile: Personality-based stat preference allocation
/// 
/// Called by: Scene initialization coroutine, triggered by BattleManager.StartBattle()
/// </summary>
public class EnemyPartySpawner : MonoBehaviour
{
    // ===== SPAWN POSITIONING =====
    /// <summary>
    /// Origin transform for party formation center point (defaults to spawner position if null).
    /// Party is centered around this point with xSpacing and zSpacing offsets.
    /// </summary>
    [Header("Spawn Settings")]
    public Transform spawnOrigin;

    /// <summary>
    /// Uniform distance between enemies in formation (deprecated; use xSpacing/zSpacing instead).
    /// </summary>
    public float spacing = 2f;

    /// <summary>
    /// X-axis offset per enemy in formation (e.g., 1f spreads party horizontally).
    /// Combined with zSpacing to create 2D formation (e.g., line or triangle).
    /// </summary>
    [Tooltip("Offset per enemy along the X axis")]
    public float xSpacing = 0f;

    /// <summary>
    /// Z-axis offset per enemy in formation (e.g., 2f spreads party along depth).
    /// Creates row-based formations when combined with xSpacing (e.g., 2x2 grid).
    /// </summary>
    [Tooltip("Offset per enemy along the Z axis")]
    public float zSpacing = 2f;

    // ===== PREFAB MAPPING =====
    /// <summary>
    /// Maps enemy type strings to instantiable prefabs.
    /// Each EnemyInfo has enemyType (string) which is matched against enemyType in this list.
    /// Enables flexible enemy type-to-prefab binding without code changes.
    /// </summary>
    [Header("Prefab Mapping")]
    public List<EnemyPrefabMapping> prefabMappings = new List<EnemyPrefabMapping>();

    /// <summary>
    /// Serializable container for enemy type → prefab bindings.
    /// enemyType must match EnemyInfo.enemyType exactly (case-insensitive comparison).
    /// prefab is the enemy GameObject prefab to instantiate with EnemyAttackController.
    /// </summary>
    [System.Serializable]
    public class EnemyPrefabMapping
    {
        public string enemyType;
        public GameObject prefab;
    }

    // ===== INITIALIZATION =====
    /// <summary>
    /// Unity lifecycle: Triggers delayed spawn to ensure BattleData population.
    /// Delay allows BattleManager to populate BattleData.enemyPartyRuntime before spawning.
    /// </summary>
    private void Start()
    {
        StartCoroutine(DelayedSpawn());
    }

    /// <summary>
    /// Short delay coroutine ensures BattleData is ready before spawning.
    /// Prevents race condition where BattleData might not be populated yet.
    /// Delay: 0.1 seconds (sufficient for BattleManager data setup).
    /// </summary>
    private IEnumerator DelayedSpawn()
    {
        yield return new WaitForSeconds(0.1f); // short delay to ensure BattleData is ready
        Debug.Log("[EnemyPartySpawner] DelayedSpawn triggered.");
        SpawnParty();
    }

    // ===== PARTY SPAWNING =====
    /// <summary>
    /// Main spawning orchestrator: iterates BattleData.enemyPartyRuntime and spawns each enemy.
    /// 
    /// Pipeline per enemy:
    /// 1. GetScaledLevel(): Calculate level based on player level (weighted distribution)
    /// 2. ApplyScaling(): Apply base level-up stat increases (HP/ATK/DEF/ComboSlots)
    /// 3. ApplyPersonalityRolls(): Personality-based bonus stat allocation
    /// 4. BountyManager.ApplyBountyScaling(): Difficulty multiplier from bounty system
    /// 5. Instantiate(): Create GameObject from prefab at calculated position
    /// 6. Initialize(): Set EnemyAttackController with runtime data
    /// 
    /// Formation: Centered around spawnOrigin with xSpacing/zSpacing offsets.
    /// Center calculation: -(count-1)*spacing/2 to position party symmetrically.
    /// 
    /// Safety: Validates prefab mappings, handles null BattleData gracefully.
    /// </summary>
    private void SpawnParty()
    {
        Debug.Log("[EnemyPartySpawner] SpawnParty() called.");

        // Validation: ensure BattleData has enemies to spawn
        if (BattleData.enemyPartyRuntime == null || BattleData.enemyPartyRuntime.Count == 0)
        {
            Debug.LogError("[EnemyPartySpawner] No enemies in BattleData.enemyPartyRuntime.");
            return;
        }

        Debug.Log($"[EnemyPartySpawner] Preparing to spawn {BattleData.enemyPartyRuntime.Count} enemies.");

        Vector3 origin = (spawnOrigin != null) ? spawnOrigin.position : transform.position;
        int count = BattleData.enemyPartyRuntime.Count;

        // Center the formation: calculate starting offset so party is centered around origin
        float xOffsetStart = -(count - 1) * xSpacing / 2f;
        float zOffsetStart = -(count - 1) * zSpacing / 2f;

        // Process each enemy in party
        for (int i = 0; i < count; i++)
        {
            var runtime = BattleData.enemyPartyRuntime[i];
            EnemyInfo info = runtime.info;

            // Query current bounty level for difficulty scaling
            int bounty = (BountyManager.Instance != null)
                ? BountyManager.Instance.currentBounty
                : 0;

            // === SCALING PIPELINE ===
            // Step 1: Calculate scaled level using weighted random distribution
            int scaledLevel = GetScaledLevel(
                PlayerStats.Instance.level,
                info.baseLevel,
                info.maxScalingLevel,
                bounty
            );

            int diff = scaledLevel - info.baseLevel;

            // Step 2: Apply base level-up scaling (multiplier-based)
            ApplyScaling(runtime, info, scaledLevel);

            // Step 3: Apply personality-based stat rolls (additive bonus)
            ApplyPersonalityRolls(runtime, info, diff);

            // Step 4: Apply bounty difficulty scaling (multiplier-based)
            if (BountyManager.Instance != null)
                BountyManager.Instance.ApplyBountyScaling(runtime);

            // Store final level for AI decision-making
            runtime.enemyLevel = scaledLevel;

            // Ensure full HP at spawn
            runtime.currentHP = runtime.maxHP;

            Debug.Log($"[Spawner] {info.enemyName} final spawn stats → " +
                      $"Level:{scaledLevel}, HP:{runtime.maxHP}, ATK:{runtime.attack}, DEF:{runtime.defense}, " + $"CounterATK:{runtime.counteratk}, Slots:{runtime.info.maxComboSlots}");

            // === INSTANTIATION ===
            // Step 5: Get prefab for this enemy type
            GameObject prefab = GetPrefabForType(runtime.info.enemyType);
            if (prefab == null)
            {
                Debug.LogError($"[EnemyPartySpawner] No prefab mapped for enemyType: {runtime.info.enemyType}");
                continue;
            }

            // Step 6: Calculate position in formation grid
            Vector3 pos = origin + new Vector3(xOffsetStart + i * xSpacing, 0f, zOffsetStart + i * zSpacing);
            GameObject go = Instantiate(prefab, pos, Quaternion.identity);

            // Step 7: Initialize enemy controller with runtime data
            var controller = go.GetComponent<EnemyAttackController>();
            if (controller != null)
            {
                controller.Initialize(runtime);
                Debug.Log($"[EnemyPartySpawner] Initialized {controller.GetEnemyName()}");
            }
        }

        Debug.Log($"[EnemyPartySpawner] Spawned {count} enemies.");
    }

    // ===== LEVEL SCALING =====
    /// <summary>
    /// Calculates scaled level for enemy using weighted random distribution.
    /// 
    /// Scaling Logic:
    /// 1. If playerLevel <= baseLevel: return baseLevel unchanged (no scaling up)
    /// 2. Calculate level range: [baseLevel+1, maxScalingLevel]
    /// 3. Generate weights for each possible level using quadratic curve
    ///    - Weight = (levelIndex/range)^2 * playerOffset
    ///    - Example: 3-level range, playerOffset=5 gives weights [5/9, 20/9, 45/9] ≈ [0.56, 0.22, 0.22]
    /// 4. Normalize weights to probability distribution
    /// 5. Weighted random roll selects final level
    /// 6. Apply bounty bonus: +0.25 per bounty point (e.g., bounty=10 → +2.5 levels)
    /// 7. Clamp to [baseLevel, maxScalingLevel]
    /// 
    /// Called by: SpawnParty() for each enemy
    /// Parameters:
    ///   - playerLevel: PlayerStats.Instance.level
    ///   - baseLevel: EnemyInfo.baseLevel (minimum)
    ///   - maxScalingLevel: EnemyInfo.maxScalingLevel (cap)
    ///   - bountyLevel: BountyManager.currentBounty (difficulty modifier)
    /// Returns: Final scaled level after all modifiers
    /// </summary>
    private int GetScaledLevel(int playerLevel, int baseLevel, int maxScalingLevel, int bountyLevel)
    {
        Debug.Log($"[Scaling] Player L{playerLevel}, Enemy Base L{baseLevel}, Cap L{maxScalingLevel}, Bounty={bountyLevel}");

        // No scaling if player level doesn't exceed base level
        if (playerLevel <= baseLevel)
        {
            Debug.Log("[Scaling] Player ≤ enemy base level → No scaling.");
            return baseLevel;
        }

        // Calculate level range for weighted selection
        int levelRange = maxScalingLevel - baseLevel;
        int playerOffset = playerLevel - baseLevel;

        // Generate weighted probability distribution for each possible level
        List<int> levels = new List<int>();
        List<float> weights = new List<float>();

        for (int i = 1; i <= levelRange; i++)
        {
            int level = baseLevel + i;
            levels.Add(level);

            // Weight increases quadratically toward higher levels
            // Formula: (i/range)^2 * playerOffset
            // Rationale: encourages meaningful level scaling without caps too early
            float weight = Mathf.Pow((float)i / levelRange, 2f);      // quadratic curve favors higher levels
            weight *= Mathf.Clamp(playerOffset, 1, 10);               // stronger player = stronger weight
            weights.Add(weight);
        }

        // Normalize weights to [0, 1] probability distribution
        float total = 0f;
        foreach (float w in weights) total += w;
        for (int i = 0; i < weights.Count; i++) weights[i] /= total;

        // Roll weighted level selection
        int rolledLevel = WeightedRoll(levels, weights);
        Debug.Log($"[Scaling] Weighted roll → L{rolledLevel}");

        // Apply bounty difficulty boost: +0.25 per bounty point
        int bountyBoost = Mathf.FloorToInt(bountyLevel * 0.25f);
        int boosted = rolledLevel + bountyBoost;

        Debug.Log($"[BountyScaling] Bounty={bountyLevel} gives +{bountyBoost} levels → L{boosted}");

        // Clamp final level to [baseLevel, maxScalingLevel]
        int final = Mathf.Clamp(boosted, baseLevel, maxScalingLevel);
        if (final != boosted)
            Debug.Log($"[BountyScaling] Boost exceeded cap → Clamped to L{final}");

        Debug.Log($"[Scaling] Final scaled level after bounty → L{final}");
        return final;
    }

    // ===== BASE STAT SCALING =====
    /// <summary>
    /// Applies base level-up stat increases using EnemyInfo stat multipliers.
    /// 
    /// Formula per stat:
    /// - maxHP += (scaledLevel - baseLevel) * hpPerLevel
    /// - attack += (scaledLevel - baseLevel) * atkPerLevel
    /// - defense += (scaledLevel - baseLevel) * defPerLevel
    /// - maxComboSlots += floor((scaledLevel - baseLevel) * comboSlotsPerLevel)
    /// 
    /// Example: Level 1→5 enemy with hpPerLevel=5:
    ///   maxHP += 4 * 5 = +20 HP
    /// 
    /// Called by: SpawnParty() after GetScaledLevel()
    /// Note: Applied BEFORE personality rolls (personality adds on top)
    /// </summary>
    private void ApplyScaling(EnemyRuntimeData runtime, EnemyInfo info, int scaledLevel)
    {
        int diff = scaledLevel - info.baseLevel;

        Debug.Log($"[Scaling] Applying scaling to {info.enemyName}: Base L{info.baseLevel} → Scaled L{scaledLevel} (diff={diff})");

        // Store old values for debug logging
        int oldHP = runtime.maxHP;
        int oldATK = runtime.attack;
        int oldDEF = runtime.defense;
        int oldSlots = runtime.info.maxComboSlots;

        // Apply multiplier-based increases
        runtime.maxHP += diff * info.hpPerLevel;
        runtime.attack += diff * info.atkPerLevel;
        runtime.defense += diff * info.defPerLevel;
        runtime.counteratk += diff * info.counteratkPerLevel;
        runtime.info.maxComboSlots += Mathf.FloorToInt(diff * info.comboSlotsPerLevel);

        Debug.Log(
            $"[ScalingStats] {info.enemyName} Stats Updated:\n" +
            $"   HP: {oldHP} → {runtime.maxHP} (+{runtime.maxHP - oldHP})\n" +
            $"   ATK: {oldATK} → {runtime.attack} (+{runtime.attack - oldATK})\n" +
            $"   DEF: {oldDEF} → {runtime.defense} (+{runtime.defense - oldDEF})\n" +
            $"   ComboSlots: {oldSlots} → {runtime.info.maxComboSlots}"
        );
    }

    // ===== WEIGHTED RANDOM UTILITY =====
    /// <summary>
    /// Weighted random selection: returns value based on probabilities in weights array.
    /// 
    /// Algorithm: Cumulative probability selection
    /// 1. Roll random [0, 1]
    /// 2. Accumulate weights until cumulative >= roll
    /// 3. Return corresponding value
    /// 
    /// Example: values=[1,2,3], weights=[0.5, 0.3, 0.2]
    ///   roll=0.3 → cumulative: 0.5 ≥ 0.3 → return 1 (50% chance)
    ///   roll=0.7 → cumulative: 0.5+0.3=0.8 ≥ 0.7 → return 2 (30% chance)
    /// 
    /// Called by: GetScaledLevel() for level distribution
    /// </summary>
    private int WeightedRoll(List<int> values, List<float> weights)
    {
        float roll = Random.value;  // [0, 1] inclusive
        float cumulative = 0f;

        for (int i = 0; i < values.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
                return values[i];
        }

        return values[values.Count - 1]; // fallback (should never reach if weights normalized)
    }

    // ===== PERSONALITY-BASED STAT ROLLS =====
    /// <summary>
    /// Applies personality-based bonus stat allocation (additive rolls on top of base scaling).
    /// Allows personality profiles to emphasize different stats (e.g., tank personality favors DEF).
    /// 
    /// Process:
    /// 1. Skip if diff <= 0 (no levels gained, no rolls)
    /// 2. For each level gained (diff iterations):
    ///    - Roll random [min, max] for stat value
    ///    - Use personality to choose target stat (ATK/DEF/HP/SLOTS)
    ///    - Apply bonus to selected stat
    /// 3. Reset currentHP to new maxHP
    /// 
    /// Personality Integration: EnemyPersonalityProfile.statWeights controls probability distribution.
    ///   Example: Tank personality might have {ATK:0.1, DEF:0.8, HP:0.1} weights
    ///   Result: 80% of rolls go to DEF, making tank much tankier
    /// 
    /// Called by: SpawnParty() after ApplyScaling()
    /// Impact: Personality-driven unique stat profiles for AI variety
    /// </summary>
    private void ApplyPersonalityRolls(EnemyRuntimeData runtime, EnemyInfo info, int diff)
    {
        // Safety: skip if no levels gained or no personality assigned
        if (diff <= 0) return;
        if (runtime.personality == null)
        {
            Debug.LogWarning($"[PersonalityRolls] {info.enemyName} has no personality assigned, skipping rolls.");
            return;
        }

        int min = info.statRollMin;
        int max = info.statRollMax;

        Debug.Log($"[PersonalityRolls] {info.enemyName} rolling {diff} times ({min}-{max}) based on personality {runtime.personality.name}.");

        // Roll once per level gained
        for (int i = 0; i < diff; i++)
        {
            int roll = Random.Range(min, max + 1);  // [min, max] inclusive

            // Use personality to determine which stat gets this roll
            string stat = ChoosePersonalityStat(runtime.personality);

            switch (stat)
            {
                case "ATK":
                    runtime.attack += roll;
                    Debug.Log($"[Roll] +{roll} ATK");
                    break;

                case "DEF":
                    runtime.defense += roll;
                    Debug.Log($"[Roll] +{roll} DEF");
                    break;

                case "HP":
                    runtime.maxHP += roll;
                    Debug.Log($"[Roll] +{roll} HP");
                    break;

                case "SLOTS":
                    runtime.info.maxComboSlots += 1; // fixed +1 per roll (or make roll-based if desired)
                    Debug.Log($"[Roll] +1 Combo Slot");
                    break;
            }
        }

        // Ensure current HP matches new max after modifications
        runtime.currentHP = runtime.maxHP;
    }

    // ===== PERSONALITY STAT SELECTION =====
    /// <summary>
    /// Selects stat name using personality's weighted distribution.
    /// 
    /// Process:
    /// 1. Sum all weights from personality.statWeights
    /// 2. Roll [0, totalWeight]
    /// 3. Accumulate weights until cumulative >= roll
    /// 4. Return matched statName (ATK, DEF, HP, or SLOTS)
    /// 
    /// Called by: ApplyPersonalityRolls() for each roll
    /// Personality Control: Defines stat emphasis (e.g., Tank=80% DEF, Balanced=25% each)
    /// </summary>
    private string ChoosePersonalityStat(EnemyPersonalityProfile personality)
    {
        float total = 0f;
        foreach (var s in personality.statWeights)
            total += s.weight;

        float roll = Random.value * total;
        float cumulative = 0f;

        foreach (var s in personality.statWeights)
        {
            cumulative += s.weight;
            if (roll <= cumulative)
                return s.statName;
        }

        return "ATK"; // fallback (should never reach if weights valid)
    }

    // ===== PREFAB LOOKUP =====
    /// <summary>
    /// Retrieves prefab for given enemy type string (case-insensitive).
    /// 
    /// Search: Linear scan through prefabMappings list (typically small, 5-20 entries).
    /// Comparison: Case-insensitive string matching for type flexibility.
    /// Returns: Prefab GameObject or null if not found (caller handles null case).
    /// 
    /// Called by: SpawnParty() for each enemy to determine instantiation template
    /// Performance: O(n) where n = number of prefab mappings (trivial for small lists)
    /// </summary>
    private GameObject GetPrefabForType(string type)
    {
        foreach (var mapping in prefabMappings)
        {
            if (mapping.enemyType.Equals(type, System.StringComparison.OrdinalIgnoreCase))
                return mapping.prefab;
        }

        return null;  // Not found
    }
}
