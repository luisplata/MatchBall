using Commander;
using Cysharp.Threading.Tasks;

public class PlayerReadyToPlay : ICommand
{
    private readonly IPlayer _player;

    public PlayerReadyToPlay(IPlayer player)
    {
        _player = player;
    }

    public UniTask Execute()
    {
        _player.ReadyToPlay();
        return UniTask.CompletedTask;
    }
}