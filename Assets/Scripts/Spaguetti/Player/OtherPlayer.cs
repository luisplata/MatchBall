using System.Collections.Generic;
using Commander;
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


    public void Configure(IGameManager gameManager, float timeToWait, IReadOnlyList<PowerUpData> powerUps)
    {
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

    public void AddPlayerInQueue(IGameManager gameManager, int randomToUsePowerUp)
    {
        Player2 = new MachinePlayer(this, gameManager, freezePlayerMono, randomToUsePowerUp);
        ServiceLocator.Instance.GetService<ICommandQueue>().AddPlayer(Player2);
    }

    public IPlayer GetPlayer()
    {
        return Player2;
    }
}