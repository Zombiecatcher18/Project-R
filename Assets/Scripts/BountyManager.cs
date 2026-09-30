using UnityEngine;

public class BountyManager : MonoBehaviour
{
    public static BountyManager Instance;

    [Header("Bounty Settings")]
    public int currentBounty = 0;
    public int baseReward = 10;
    public float enemyScalePerBounty = 0.01f;
    public int maxEnemyScaleMultiplier = 2;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddBounty(int amount)
    {
        if (amount <= 0) amount = baseReward;
        currentBounty += amount;

        Debug.Log($"[BountyManager] Bounty increased by {amount}. Current bounty: {currentBounty}");

        if (PlayerStats.Instance != null)
        {
            // Optional hooks for auto-scaling with bounty
        }
    }

    public void ApplyBountyScaling(EnemyRuntimeData runtime)
    {
        if (runtime == null || runtime.info == null) return;

        float scaleFactor = 1f + (currentBounty * enemyScalePerBounty);
        scaleFactor = Mathf.Min(scaleFactor, maxEnemyScaleMultiplier);

        int oldHP = runtime.maxHP;
        int oldATK = runtime.attack;
        int oldDEF = runtime.defense;

        runtime.maxHP = Mathf.RoundToInt(runtime.maxHP * scaleFactor);
        runtime.attack = Mathf.RoundToInt(runtime.attack * scaleFactor);
        runtime.defense = Mathf.RoundToInt(runtime.defense * scaleFactor);
        runtime.counteratk = Mathf.RoundToInt(runtime.counteratk * scaleFactor);

        runtime.currentHP = runtime.maxHP;

        Debug.Log(
            $"[BountyScaling] {runtime.info.enemyName} scaled by bounty={currentBounty} (x{scaleFactor:F2})\n" +
            $"   HP: {oldHP} → {runtime.maxHP}\n" +
            $"   ATK: {oldATK} → {runtime.attack}\n" +
            $"   DEF: {oldDEF} → {runtime.defense}"
        );
    }

    public bool SpendBounty(int cost, string stat)
    {
        if (currentBounty < cost)
        {
            Debug.Log("[BountyManager] Not enough bounty to upgrade!");
            return false;
        }

        currentBounty -= cost;

        switch (stat)
        {
            case "Attack":
                PlayerStats.Instance.UpgradeATK();
                break;
            case "Defense":
                PlayerStats.Instance.UpgradeDEF();
                break;
            case "HP":
                PlayerStats.Instance.MaxHealth += 10;
                PlayerStats.Instance.currentHealth = PlayerStats.Instance.MaxHealth;
                break;
        }

        Debug.Log($"[BountyManager] Upgraded {stat}. Remaining bounty: {currentBounty}");
        return true;
    }
}
