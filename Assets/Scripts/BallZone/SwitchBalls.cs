using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class SwitchBalls : MonoBehaviour, IPointerClickHandler
{
    public Action OnSwitchClicked;
    public void OnPointerClick(PointerEventData eventData)
    {
        OnSwitchClicked?.Invoke();
    }
}