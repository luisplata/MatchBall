using System.Collections.Generic;

public interface IPlayerReference
{
    IPlayer GetPlayer();
    void Configure(IGameManager gameManager, IReadOnlyList<PowerUpData> powerUps);
    bool ReadyToPlay();
}