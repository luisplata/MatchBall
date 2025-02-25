using Commander;
using Cysharp.Threading.Tasks;

internal class ConfigureBallZone : ICommand
{
    private readonly IPlayer _player;

    public ConfigureBallZone(IPlayer player)
    {
        _player = player;
    }

    public UniTask Execute()
    {
        _player.ConfigureBallZone();
        return UniTask.CompletedTask;
    }
}