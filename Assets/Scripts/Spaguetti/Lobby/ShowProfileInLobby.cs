using DG.Tweening;
using UnityEngine;

public class ShowProfileInLobby : MonoBehaviour
{
    [SerializeField] private CanvasGroup profileCanvasGroup;

    private void Start()
    {
        Configure();
        ShowProfile();
        HideProfile();
    }

    public void Configure()
    {
        profileCanvasGroup.alpha = 0;
    }

    public void ShowProfile()
    {
        profileCanvasGroup.gameObject.SetActive(true);
        profileCanvasGroup.DOFade(1, 0.5f);
    }

    public void HideProfile()
    {
        profileCanvasGroup.DOFade(0, 0.5f).onComplete = () => { profileCanvasGroup.gameObject.SetActive(false); };
    }
}