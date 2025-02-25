using UnityEngine.EventSystems;

public class SfxControllerClick : SfxController, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        PlaySfx();
    }
}