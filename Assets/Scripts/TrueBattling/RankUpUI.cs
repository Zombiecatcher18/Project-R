using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RankUpUI : MonoBehaviour
{
    public static RankUpUI Instance;

    public GameObject panel;
    public TextMeshProUGUI rankNameText;
    public TextMeshProUGUI slotBonusText;
    public TextMeshProUGUI deckBonusText;
    public Button continueButton;

    [Header("Move Reward")]
    public GameObject moveRewardPanel;
    public TextMeshProUGUI moveRewardNameText;
    public Image moveRewardIcon;

    private void Awake()
    {
        Instance = this;
        Debug.Log("[RANK UI] Awake. Instance set to " + this.name);
        panel.SetActive(false);
    }

    public void ShowRankUp(string rankName, int slotBonus, int deckBonus, AttackMove rewardMove = null)
    {
        Debug.Log($"[RANK UI] ShowRankUp called. rankName={rankName}, slotBonus={slotBonus}, deckBonus={deckBonus}");
        panel.SetActive(true);

        rankNameText.text = $"New Rank: {rankName}";
        slotBonusText.text = $"+{slotBonus} Max Slots";
        deckBonusText.text = $"+{deckBonus} Deck Size";

        if (rewardMove != null)
        {
            moveRewardPanel.SetActive(true);
            moveRewardNameText.text = rewardMove.moveName;
            moveRewardIcon.sprite = rewardMove.icon;
        }
        else
        {
            moveRewardPanel.SetActive(false);
        }   

        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(OnContinue);
    }

    private void OnContinue()
    {
        panel.SetActive(false);
        PlayerStats.Instance.lastUnlockedMove = null;
        BattleManager.Instances.FinishBattleReturnToOverworld();
    }
}

