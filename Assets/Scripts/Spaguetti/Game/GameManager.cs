using App.UseCase;
using Commander;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class GameManager : MonoBehaviour, IGameManager
{
    [SerializeField, InterfaceType(typeof(IPlayerReference))]
    private Object player1;

    private IPlayerReference PlayerRef1 => player1 as IPlayerReference;

// Optionally, adding a private property is recommended for
// automatically casting the object field to the interface.
    private IPlayer Player1 => PlayerRef1.GetPlayer();

    [SerializeField] private OtherPlayer player2;
    [SerializeField] private GameObject gameOverPanel, youWinPanel;
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private string music;
    [SerializeField] private PlayableDirector director;
    private bool _isGameOver;

    private void Awake()
    {
        //set FPS to 60
        Application.targetFrameRate = 60;
        //lock to portrait mode
        Screen.orientation = ScreenOrientation.Portrait;
        //set screen dont sleep
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        //set dont restart app if it was paused
        Application.runInBackground = true;
    }

    private void Start()
    {
        Player1.Configure(this);
        ServiceLocator.Instance.GetService<IMusicController>().StopMusic();

        gameOverPanel.SetActive(false);
        youWinPanel.SetActive(false);
        HideMessage();
        _isGameOver = false;

        ServiceLocator.Instance.GetService<ICommandQueue>().AddPlayer(Player1);
        player2.AddPlayerInQueue(this);

        ServiceLocator.Instance.GetService<ITransitionService>().HideSplashScreen(() =>
        {
            PlayerRef1.Configure(this);

            //get the gamemode to configure the game
            var gameMode = ServiceLocator.Instance.GetService<IStorageData>().LoadDataGlobal(GlobalData.GameMode);
            if (gameMode == GameMode.SinglePlayer.ToString())
            {
            }
            else if (gameMode == GameMode.MultiPlayer.ToString())
            {
            }
            else if (gameMode == GameMode.WithFriends.ToString())
            {
            }
            else
            {
                //player2.Configure(this, (float)director.duration);
            }

            player2.Configure(this, (float)director.duration);
        });
    }

    private void FixedUpdate()
    {
        if (!PlayerRef1.ReadyToPlay() || !player2.ReadyToPlay()) return;
        if ((Player1.IsGameOver() || player2.IsGameOver()) && !_isGameOver)
        {
            Player1.DisableControls();
            _isGameOver = true;
            ServiceLocator.Instance.GetService<IMusicController>().StopMusic();
            Debug.Log("Game Over");
            if (Player1.IsGameOver())
            {
                gameOverPanel.SetActive(true);
                ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("Lose");
                ServiceLocator.Instance.GetService<ILoginUseCase>().AddExperience(false).Forget();
            }
            else if (player2.IsGameOver())
            {
                youWinPanel.SetActive(true);
                ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("Win");
                ServiceLocator.Instance.GetService<ILoginUseCase>().AddExperience(true).Forget();
            }
        }
    }

    private void OnDestroy()
    {
        ServiceLocator.Instance.GetService<ICommandQueue>().RemovePlayer(Player1);
        ServiceLocator.Instance.GetService<ICommandQueue>().RemovePlayer(player2.GetPlayer());
    }

    public void RestartApplication()
    {
        ServiceLocator.Instance.GetService<ITransitionService>().ShowSplashScreen(AddCoinsAndLoadScene);
    }

    private async void AddCoinsAndLoadScene()
    {
        await ServiceLocator.Instance.GetService<IPlayerSessionService>().AddCoins(Player1.GetCoins());
        SceneManager.LoadScene("BallPass");
    }

    public void ShowMessage(string message)
    {
        messageText.text = message;
        messagePanel.SetActive(true);
    }

    public void HideMessage()
    {
        messagePanel.SetActive(false);
        messageText.text = string.Empty;
    }

    public PlayableDirector GetDirectorOpening()
    {
        return director;
    }

    public string GetMusic()
    {
        return music;
    }

    public IPlayer GetFirstPlayer()
    {
        return Player1;
    }

    public bool IsGameOver()
    {
        return _isGameOver;
    }

    public IPlayer GetOtherPlayer()
    {
        return player2.GetPlayer();
    }
}

public interface IGameManager
{
    void ShowMessage(string message);
    void HideMessage();
    PlayableDirector GetDirectorOpening();
    string GetMusic();
    IPlayer GetFirstPlayer();
    bool IsGameOver();
    IPlayer GetOtherPlayer();
}