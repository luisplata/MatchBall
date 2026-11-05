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

    /// <summary>
    /// Replaces the scene-authored script with an explicit consumption order: the first entry is
    /// the first ball <see cref="CreateBall"/> returns. <see cref="CreateBall"/> pops from the end,
    /// so the list is stored reversed. A normal match never calls this and keeps its own script.
    /// </summary>
    public void SetConsumptionOrder(IReadOnlyList<string> order)
    {
        ids.Clear();
        for (var i = order.Count - 1; i >= 0; i--)
        {
            ids.Add(order[i]);
        }
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