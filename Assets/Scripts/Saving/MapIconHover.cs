using UnityEngine;
using UnityEngine.EventSystems;

public class MapIconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string tooltipText = "Icon";

    public void OnPointerEnter(PointerEventData eventData)
    {
        MapTooltip.Instance.Show(tooltipText);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        MapTooltip.Instance.Hide();
    }
}
