using UnityEngine;
using TMPro;

public class MapTooltip : MonoBehaviour
{
    public static MapTooltip Instance;

    public RectTransform tooltipRoot;
    public TextMeshProUGUI tooltipText;

    void Awake()
    {
        Instance = this;
        tooltipRoot.gameObject.SetActive(false);
    }

    void Update()
    {
        if (tooltipRoot.gameObject.activeSelf)
        {
            tooltipRoot.position = Input.mousePosition + new Vector3(10f, -40f, 0f);
        }
    }

    public void Show(string text)
    {
        tooltipText.text = text;
        tooltipRoot.gameObject.SetActive(true);
    }

    public void Hide()
    {
        tooltipRoot.gameObject.SetActive(false);
    }
}
