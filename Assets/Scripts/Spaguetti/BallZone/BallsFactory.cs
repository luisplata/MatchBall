using UnityEngine;

public class BallsFactory
{
    private readonly BallsConfiguration _ballsConfiguration;

    public BallsFactory(BallsConfiguration ballsConfiguration)
    {
        _ballsConfiguration = ballsConfiguration;
    }

    public Ball Create(string id)
    {
        var prefab = _ballsConfiguration.GetBallPrefabById(id);

        return Object.Instantiate(prefab);
    }

    public Ball Create()
    {
        var prefab = _ballsConfiguration.GetRandomBallPrefab();

        return Object.Instantiate(prefab);
    }
}