using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyRules : MonoBehaviour
{
    [SerializeField] private ShowProfileInLobby showProfileInLobby;
    [SerializeField] private ShowPowerUpsInLobby showPowerUpsInLobby;
    [SerializeField] private Slider slider;
    [SerializeField] private float timeToStart;
    [SerializeField] private int sceneToLoad;
    private float _deltaLocal;

    private void Start()
    {
        ServiceLocator.Instance.GetService<ITransitionService>().HideSplashScreen(() =>
        {
            this.tt().Add(() =>
                {
                    showProfileInLobby.Configure();
                    showPowerUpsInLobby.Configure();
                }).Add(() => { showProfileInLobby.ShowProfile(); }).Add(5).Add(() =>
                {
                    showProfileInLobby.HideProfile();
                })
                .Add(() => { showPowerUpsInLobby.ShowPowerUps(); }).Loop(h =>
                {
                    _deltaLocal += h.deltaTime;
                    slider.value = _deltaLocal / timeToStart;
                    if (_deltaLocal > timeToStart)
                    {
                        h.Break();
                    }
                }).Add(() =>
                {
                    ServiceLocator.Instance.GetService<ITransitionService>()
                        .ShowSplashScreen(() => { SceneManager.LoadScene(sceneToLoad); });
                });
        });
    }
}