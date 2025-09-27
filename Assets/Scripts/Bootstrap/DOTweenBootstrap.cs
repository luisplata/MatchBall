using DG.Tweening;
using UnityEngine;

namespace Bootstrap
{
    public static class DOTweenBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
            Debug.Log("DOTween initialized");
        }
    }
}