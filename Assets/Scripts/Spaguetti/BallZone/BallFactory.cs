using System.Collections.Generic;
using UnityEngine;

public class BallFactory : MonoBehaviour
{
    [SerializeField] private BallsConfiguration ballsConfiguration;
    [SerializeField] private List<string> ids;
    private BallsFactory ballsFactory;

    private void Awake()
    {
        ballsFactory = new BallsFactory(Instantiate(ballsConfiguration));
    }

    public Ball CreateBall()
    {
        //if ids is empty, return a random ball
        if (ids.Count == 0)
        {
            return ballsFactory.Create();
        }

        var ball = ballsFactory.Create(ids[^1]);
        ids = ids.GetRange(0, ids.Count - 1);
        return ball;
    }
}