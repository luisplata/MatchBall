using UnityEngine.EventSystems;

public class SfxControllerClickDown : SfxController, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        PlaySfx();
    }
}