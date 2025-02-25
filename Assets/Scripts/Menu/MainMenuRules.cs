using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuRules : MonoBehaviour
{
    [SerializeField] private Button storeButton, homeButton, ballPass, settingsButton;
    [SerializeField] private GameObject helpPanel;
    
    [SerializeField, InterfaceType(typeof(IWaitToLoadInfo))]
    private Object[] waitToLoadObjects;
    
    private List<IWaitToLoadInfo> _waitToLoadInfos = new();
    

    private async void Start()
    {
        storeButton.onClick.AddListener(OpenStore);
        homeButton.onClick.AddListener(OpenHome);
        ballPass.onClick.AddListener(OpenBallPass);
        settingsButton.onClick.AddListener(Settings);
        
        foreach (var waitToLoadObject in waitToLoadObjects)
        {
            _waitToLoadInfos.Add(waitToLoadObject as IWaitToLoadInfo);
        }
        await UniTask.WaitUntil(() => _waitToLoadInfos.All(x => x.IsReady));
        ServiceLocator.Instance.GetService<ITransitionService>().HideSplashScreen();
        
        //move panel from left 0 to right -1000 with dotween
        helpPanel.transform.position = new Vector3(-1000, helpPanel.transform.position.y, helpPanel.transform.position.z);
        ServiceLocator.Instance.GetService<IMusicController>().PlayMusic("Mystery");
        
    }

    private void Settings()
    {
        //Debug.Log("Settings");
    }

    private void OpenBallPass()
    {
        //Debug.Log("Open Ball Pass");
        ServiceLocator.Instance.GetService<ITransitionService>().ShowSplashScreen(() =>
        {
            SceneManager.LoadScene("BallPass");
        });
    }

    private void OpenHome()
    {
        //Debug.Log("Open Home");
    }

    private void OpenStore()
    {
        //Debug.Log("Open Store");
    }
}