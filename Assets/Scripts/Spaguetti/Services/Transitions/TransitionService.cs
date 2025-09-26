using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class TransitionService : MonoBehaviour, ITransitionService
{
    [SerializeField] private CanvasGroup canvasGroupSplashScreen;
    private bool _isShowing;

    private void Awake()
    {
        if (FindObjectsOfType<TransitionService>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        if (ServiceLocator.Instance.RegisterService<ITransitionService>(this))
        {
            //canvasGroupSplashScreen.alpha = 1;
            EnableComponents(false);
            canvasGroupSplashScreen.alpha = 0;
            DontDestroyOnLoad(gameObject);
        }
    }

    private void EnableComponents(bool enable)
    {
        canvasGroupSplashScreen.interactable = enable;
        canvasGroupSplashScreen.blocksRaycasts = enable;
    }

    public async UniTask ShowSplashScreen(Action onTask = null)
    {
        // EnableComponents(true);
        // _isShowing = true;
        // var sequence = DOTween.Sequence();
        // canvasGroupSplashScreen.alpha = 0;
        // sequence.Append(canvasGroupSplashScreen.DOFade(1, 1.5f));
        // sequence.OnComplete(() => {  });
        // await sequence.AsyncWaitForCompletion();
        onTask?.Invoke();
    }

    public async UniTask HideSplashScreen(Action onComplete = null)
    {
        // var sequence = DOTween.Sequence();
        // canvasGroupSplashScreen.alpha = 1;
        // sequence.Append(canvasGroupSplashScreen.DOFade(0, 1.5f));
        // sequence.AppendCallback(() =>
        // {
        //     EnableComponents(false);
        // });
        // await sequence.AsyncWaitForCompletion();
        // _isShowing = false;
        onComplete?.Invoke();
    }

    public async UniTask ShowSplashScreen(Action onTask = null, Action onComplete = null)
    {
        EnableComponents(true);
        _isShowing = true;
        var sequence = DOTween.Sequence();
        sequence.Append(canvasGroupSplashScreen.DOFade(1, 1.5f));
        sequence.OnComplete(() => { onTask?.Invoke(); });
        await sequence.AsyncWaitForCompletion();
        await HideSplashScreen(onComplete);
    }
}

public interface ITransitionService
{
    UniTask ShowSplashScreen(Action onTask = null, Action onComplete = null);
    UniTask ShowSplashScreen(Action onTask = null);
    UniTask HideSplashScreen(Action onComplete = null);
}