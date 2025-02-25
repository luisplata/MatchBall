using Commander;
using Cysharp.Threading.Tasks;
using UnityEngine.Playables;

internal class OpeningGame : ICommand
{
    private readonly PlayableDirector _director;
    private readonly bool _mayPlay;

    public OpeningGame(PlayableDirector director, bool mayPlay = true)
    {
        _director = director;
        _mayPlay = mayPlay;
    }

    public UniTask Execute()
    {
        if(_mayPlay) _director.Play();
        return UniTask.WaitForSeconds((float)_director.duration);
    }
}