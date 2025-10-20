using System.Collections.Generic;
using Commander;

public interface IPlayer
{
    string Id { get; }
    void AddCommand(ICommand command);
    bool IsBusy();
    void Configure(IGameManager gameManager, IReadOnlyList<PowerUpData> powerUps);
    int GetCoins();
    bool IsGameOver();
    void DisableControls();
    void ConfigureBallZone(IReadOnlyList<PowerUpData> powerUps);
    IBalance GetBalance();
    IBallZone GetBallZone();
    IGameManager GetGameManager();
    void FreezeGame(bool isFreeze);
    void ClearCommands();
    void EnableControls();
    void ConsumePowerUp(string prefabID);
    IReadOnlyList<PowerUpData> GetPowerUps();
}