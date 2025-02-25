using System.Collections.Generic;
using Commander;
using Cysharp.Threading.Tasks;
using UnityEngine;

internal class SwitchBallsCommand : ICommand
{
    private readonly Ball[] _balls;
    private readonly List<GameObject> _ballPoints;

    public SwitchBallsCommand(Ball[] balls, List<GameObject> ballPoints)
    {
        _balls = balls;
        _ballPoints = ballPoints;
    }

    public UniTask Execute()
    {
        (_balls[0], _balls[1]) = (_balls[1], _balls[0]);
        return UniTask.WhenAll(_balls[0].MoveBall(_balls[0].transform.position, _ballPoints[0].transform.position),
            _balls[1].MoveBall(_balls[1].transform.position, _ballPoints[1].transform.position));
    }
}