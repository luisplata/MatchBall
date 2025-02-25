using UnityEngine.EventSystems;

public class SfxControllerHoverIn : SfxController, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        PlaySfx();
    }
}