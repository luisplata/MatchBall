using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class InputClickHandle : MonoBehaviour, IPointerClickHandler
{
    public Action OnClickDown;
    public Action OnClickUp;
    private IBall _ball;
    private Collider2D _collider2D;

    private void Awake()
    {
        _collider2D = GetComponent<Collider2D>();
    }

    private void OnMouseUp()
    {
        if (!_ball.CanBeClicked()) return;
        OnClickUp?.Invoke();
    }

    public void Configure(IBall ball)
    {
        _ball = ball;
        if (!_ball.CanBeClicked())
        {
            GetComponent<Collider2D>().enabled = false;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_ball.CanBeClicked()) return;
        OnClickDown?.Invoke();
    }

    public void DisableCollider()
    {
        _collider2D.enabled = false;
    }
}