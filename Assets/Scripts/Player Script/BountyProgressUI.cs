using UnityEngine;
using TMPro;

public class BountyProgressUI : MonoBehaviour
{
    public GameObject panel;
    public TextMeshProUGUI bountyText;
    public TextMeshProUGUI progressText;

    public void Show(int gained, int current, int required)
    {
        panel.SetActive(true);
        bountyText.text = $"+{gained} Bounty!";
        progressText.text = $"{current} / {required} to next level";
    }

    public void OnContinue()
    {
        panel.SetActive(false);

        // unlock movement
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            var controller = player.GetComponent<ExamplePlayerController>();
            if (controller != null)
            {
                controller.movementLocked = false;
                controller.EnableInput(true);
            }
        }

        BattleManager.Instances.FinishBattleReturnToOverworld();
    }
}
