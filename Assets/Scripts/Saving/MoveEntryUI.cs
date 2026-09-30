using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MoveEntryUI : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text costText;
    public Button actionButton;
    public Button infoButton;
    private AttackMove move;
    private DeckBuilderUI deckUI;

    public void Setup(AttackMove m, string buttonLabel, System.Action onClick, DeckBuilderUI ui)
    {
        move = m;
        deckUI = ui;

        nameText.text = m.moveName;
        costText.text = "Cost: " + m.slotCost;

        actionButton.GetComponentInChildren<TMP_Text>().text = buttonLabel;

        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(() => onClick());

        infoButton.onClick.RemoveAllListeners();
        infoButton.onClick.AddListener(() => deckUI.ShowMoveInfo(move));
    }

    public void SetButtonColor(Color c)
    {
        var colors = actionButton.colors;
        colors.normalColor = c;
        colors.highlightedColor = c;
        colors.pressedColor = c * 0.9f;
        colors.selectedColor = c;
        actionButton.colors = colors;
    }
}
