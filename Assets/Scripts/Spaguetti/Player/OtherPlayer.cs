using System.Collections.Generic;
using Commander;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

public class OtherPlayer : MonoBehaviour
{
    [SerializeField, InterfaceType(typeof(IBalance))]
    private Object balance;

    // Optionally, adding a private property is recommended for
    // automatically casting the object field to the interface.
    public IBalance Balance => balance as IBalance;

    [SerializeField, InterfaceType(typeof(IBallZone))]
    private Object ballzone;

    // Optionally, adding a private property is recommended for
    // automatically casting the object field to the interface.
    public IBallZone BallZone => ballzone as IBallZone;

    //Player AI or Multiplayer
    private IPlayer Player2;


    [SerializeField] private FreezePlayerMono freezePlayerMono;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI nameText;

    public TextMeshProUGUI ScoreText => scoreText;
    public TextMeshProUGUI NameText => nameText;


    public void Configure(IGameManager gameManager, float timeToWait, IReadOnlyList<PowerUpData> powerUps,
        string displayName = "AI")
    {
        if (scoreText != null) scoreText.text = "0";
        else Debug.LogWarning("[OtherPlayer] scoreText is not wired.");

        if (nameText != null) nameText.text = displayName;
        else Debug.LogWarning("[OtherPlayer] nameText is not wired.");

        Player2.Configure(gameManager, powerUps);
        ServiceLocator.Instance.GetService<ICommandQueue>().AddPlayer(Player2);
        ServiceLocator.Instance.GetService<ICommandQueue>().AddCommand(new WaitTime(timeToWait), Player2);
        ServiceLocator.Instance.GetService<ICommandQueue>()
            .AddCommand(new ConfigureBallZone(Player2, powerUps), Player2);
        ServiceLocator.Instance.GetService<ICommandQueue>().AddCommand(new PlayerReadyToPlay(Player2), Player2);
        //Debug.Log("OtherPlayer Configure");
    }

    public bool IsGameOver()
    {
        return Player2.IsGameOver();
    }

    public bool ReadyToPlay()
    {
        return Player2.IsReadyToPlay();
    }

    /// <summary>
    /// Seeds P2 with the mediator this match needs. The default keeps the AI path byte-identical;
    /// local PvP passes isHuman and gets the human mediator instead.
    /// </summary>
    public void AddPlayerInQueue(IGameManager gameManager, int randomToUsePowerUp, bool isHuman = false)
    {
        if (isHuman)
        {
            var humanBallZone = ballzone as BallZone;
            if (humanBallZone == null)
            {
                // Fail closed: without the concrete zone P2 can neither play nor spawn orbs.
                Debug.LogError("[OtherPlayer] local PvP needs a concrete BallZone on P2; P2 will not play.");
            }

            Player2 = new HumanP2Player(this, gameManager, freezePlayerMono, humanBallZone);
        }
        else
        {
            Player2 = new MachinePlayer(this, gameManager, freezePlayerMono, randomToUsePowerUp);
        }

        ServiceLocator.Instance.GetService<ICommandQueue>().AddPlayer(Player2);
    }

    public IPlayer GetPlayer()
    {
        return Player2;
    }
}