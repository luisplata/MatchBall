using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

[CreateAssetMenu(menuName = "Custom/Ball configuration")]
public class BallsConfiguration : ScriptableObject
{
    [SerializeField] private Ball[] Balls;
    private Dictionary<string, Ball> _idToBall;

    private void Awake()
    {
        _idToBall = new Dictionary<string, Ball>(Balls.Length);
        foreach (var ball in Balls)
        {
            _idToBall.Add(ball.Id, ball);
        }
    }

    public Ball GetBallPrefabById(string id)
    {
        if (!_idToBall.TryGetValue(id, out var ball))
        {
            throw new Exception($"Ball with id {id} does not exit");
        }
        return ball;
    }

    public int GetBallsCount()
    {
        return Balls.Length;
    }

    public string GetBallIdByIndex(int randomIndex)
    {
        return Balls[randomIndex].Id;
    }

    public Ball GetRandomBallPrefab()
    {
        var randomIndex = UnityEngine.Random.Range(0, Balls.Length);
        return Balls[randomIndex];
    }
}