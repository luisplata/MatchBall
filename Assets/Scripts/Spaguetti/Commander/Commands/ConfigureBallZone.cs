using System.Collections.Generic;
using Commander;
using Cysharp.Threading.Tasks;

internal class ConfigureBallZone : ICommand
{
    private readonly IPlayer _player;
    private readonly IReadOnlyList<PowerUpData> _powerUps;

    public ConfigureBallZone(IPlayer player, IReadOnlyList<PowerUpData> powerUps)
    {
        _player = player;
        _powerUps = powerUps;
    }

    public UniTask Execute()
    {
        _player.ConfigureBallZone(_powerUps);
        return UniTask.CompletedTask;
    }
}