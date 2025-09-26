using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MatchingScene : MonoBehaviour
{
    [SerializeField] private GameObject loading_panel;
    [SerializeField] private Image loading_icon;
    [SerializeField] private float rotationSpeed;
    [SerializeField] private bool isMatching;

    public bool IsMatching => isMatching;

    public void Match()
    {
        isMatching = true;
    }

    private void Start()
    {
        HideLoadingIcon();
        ServiceLocator.Instance.GetService<ITransitionService>().HideSplashScreen(StartMatching);
    }

    public void ShowLoadingIcon()
    {
        loading_panel.gameObject.SetActive(true);
    }

    public void HideLoadingIcon()
    {
        loading_panel.gameObject.SetActive(false);
    }

    private void StartMatching()
    {
        ShowLoadingIcon();
        this.tt().Loop(h =>
        {
            if (h.timeSinceStart > 5)
            {
                isMatching = true;
            }

            if (isMatching)
            {
                h.Break();
            }

            loading_icon.transform.Rotate(Vector3.forward, rotationSpeed * h.deltaTime);
        }).Add(() =>
        {
            ServiceLocator.Instance.GetService<ITransitionService>().ShowSplashScreen(() =>
            {
                SceneManager.LoadScene(4);
            });
        });
    }
}