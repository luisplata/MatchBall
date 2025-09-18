using Commander;
using Cysharp.Threading.Tasks;

public class WaitTime : ICommand
{
    private readonly float _time;

    public WaitTime(float time)
    {
        _time = time;
    }

    public UniTask Execute()
    {
        return UniTask.Delay((int)(_time * 1000));
    }
}