using Commander;
using Cysharp.Threading.Tasks;

internal class PlayMusic : ICommand
{
    private readonly string _music;

    public PlayMusic(string music)
    {
        _music = music;
    }

    public UniTask Execute()
    {
        ServiceLocator.Instance.GetService<IMusicController>().PlayMusic(_music);
        return UniTask.CompletedTask;
    }
}