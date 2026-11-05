using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuRules : MonoBehaviour
{
    [SerializeField] private Button storeButton, homeButton, ballPass, settingsButton;
    [SerializeField] private Button tutorialButton;
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
        if (tutorialButton != null) tutorialButton.onClick.AddListener(StartTutorial);
        
        foreach (var waitToLoadObject in waitToLoadObjects)
        {
            _waitToLoadInfos.Add(waitToLoadObject as IWaitToLoadInfo);
        }
        await UniTask.WaitUntil(() => _waitToLoadInfos.All(x => x.IsReady));
        ServiceLocator.Instance.GetService<ITransitionService>().HideSplashScreen();
        
        //move panel from left 0 to right -1000 with dotween
        helpPanel.transform.position = new Vector3(-2000, helpPanel.transform.position.y, helpPanel.transform.position.z);
        ServiceLocator.Instance.GetService<IMusicController>().PlayMusic("Mystery");
        
    }

    private void Settings()
    {
        //Debug.Log("Settings");
    }

    /// <summary>
    /// Replayable tutorial entry from Instruccions. Writes only the transient mode flag; the durable
    /// completed flag is never touched, so replaying and skipping leaves the account where it was.
    /// </summary>
    public void StartTutorial()
    {
        ServiceLocator.Instance.GetService<ITransitionService>().ShowSplashScreen(() =>
        {
            Presentation.Tutorial.TutorialResume.Clear();
            ServiceLocator.Instance.GetService<IStorageData>().SaveDataGlobal(GlobalData.TutorialMode, "1");
            Debug.Log("[Tutorial] replay requested from Instruccions -> Game in tutorial mode.");
            SceneManager.LoadScene("Game");
        });
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