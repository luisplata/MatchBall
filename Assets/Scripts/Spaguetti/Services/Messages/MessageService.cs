using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MessageService : MonoBehaviour, IMessageService
{
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private GameObject positionTop, positionBottom;
    [SerializeField] private bool _isMoving;
    [SerializeField] private bool _isShowing;
    private Queue<MessageData> _messageQueue = new();

    private void Awake()
    {
        if (FindObjectsOfType<MessageService>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        if (ServiceLocator.Instance.RegisterService<IMessageService>(this))
        {
            messagePanel.transform.position = positionTop.transform.position;
            HideMessage(true).Forget();
            DontDestroyOnLoad(gameObject);
        }
    }

    public async UniTask HideMessage(bool immediate = false)
    {
        if (immediate)
        {
            DisableComponents();
            await UniTask.CompletedTask;
        }
        else
        {
            _isMoving = true;
            await messagePanel.transform.DOMoveY(positionTop.transform.position.y, 0.1f).OnComplete(() =>
            {
                DisableComponents();
                _isMoving = false;
                _isShowing = false;
            }).AsyncWaitForCompletion();
        }
    }

    public async UniTask ShowMessage(string message, float duration = 2f)
    {
        Debug.Log(message);
        _messageQueue.Enqueue(new MessageData { message = message, duration = duration, type = TypeMessage.Success });

        await MessagesLoop();
    }

    private async UniTask MessagesLoop()
    {
        while (_messageQueue.Count > 0)
        {
            if (_isMoving)
            {
                await UniTask.NextFrame();
                continue;
            }

            if (_isShowing)
            {
                await HideMessage();
                continue;
            }

            var data = _messageQueue.Dequeue();
            var image = messagePanel.GetComponent<Image>();
            switch (data.type)
            {
                case TypeMessage.Success:
                    image.color = Color.green;
                    break;
                case TypeMessage.Error:
                    image.color = Color.red;
                    break;
            }

            //Debug.Log("Showing message");
            ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("notification_up");
            await EnableComponents(data.message, data.duration);
            if (!_isMoving && _isShowing && _messageQueue.Count == 0)
            {
                //Debug.Log("Hiding message");
                await HideMessage();
            }
        }
    }

    public async UniTask ErrorMessage(string message, float duration = 2)
    {
        Debug.LogError(message);
        _messageQueue.Enqueue(new MessageData { message = message, duration = duration, type = TypeMessage.Error });

        await MessagesLoop();
    }

    public async UniTask ErrorMessage(Exception error, float duration = 2)
    {
        Debug.LogError(error.Message);
        _messageQueue.Enqueue(
            new MessageData { message = error.Message, duration = duration, type = TypeMessage.Error });

        await MessagesLoop();
    }

    private async UniTask EnableComponents(string message, float duration)
    {
        //Debug.Log(message);
        messageText.text = message;
        messagePanel.SetActive(true);
        _isMoving = true;
        _isShowing = true;
        var sequence = DOTween.Sequence();
        sequence.Append(messagePanel.transform.DOMoveY(positionBottom.transform.position.y, 0.5f));
        sequence.AppendCallback(() => { _isMoving = false; });
        sequence.AppendInterval(duration);
        await sequence.AsyncWaitForCompletion();
    }

    private void DisableComponents()
    {
        messagePanel.SetActive(false);
        messageText.text = "";
    }
}

public interface IMessageService
{
    UniTask HideMessage(bool immediate = false);
    UniTask ShowMessage(string message, float duration = 2f);
    UniTask ErrorMessage(string message, float duration = 2f);
    UniTask ErrorMessage(Exception error, float duration = 2f);
}

public class MessageData
{
    public string message;
    public float duration;
    public TypeMessage type;
}

public enum TypeMessage
{
    Success,
    Error
}