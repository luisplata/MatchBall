using UnityEngine.EventSystems;

public class SfxControllerHoverOut : SfxController, IPointerExitHandler
{
    public void OnPointerExit(PointerEventData eventData)
    {
        PlaySfx();
    }
}