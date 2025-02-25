using UnityEngine;

public class Global : IGlobalParameters
{
    private readonly Color[] _colors =
    {
        Color.grey,
        Color.white,
        Color.green,
        Color.magenta,
        Color.cyan
    };

    private readonly Color[] _powerUpColors =
    {
        Color.yellow
    };

    private readonly string[] _names =
    {
        "Grey",
        "White",
        "Green",
        "Magenta",
        "Cyan"
    };

    private readonly string[] _powerUpNames =
    {
        "all"
    };

    public Global()
    {
    }

    public BallStructure GetRandomBall()
    {
        var randomIndex = Random.Range(0, _colors.Length);
        return new BallStructure
        {
            Color = _colors[randomIndex],
            Name = _names[randomIndex]
        };
    }

    public PowerUpStructure GetRandomPowerUp()
    {
        var randomIndex = Random.Range(0, _powerUpColors.Length);
        return new PowerUpStructure
        {
            Color = _powerUpColors[randomIndex],
            Name = _powerUpNames[randomIndex]
        };
    }
}

public interface IGlobalParameters
{
    BallStructure GetRandomBall();
    PowerUpStructure GetRandomPowerUp();
}

public class PowerUpStructure
{
    public Color Color;
    public string Name;
}

public class BallStructure
{
    public Color Color;
    public string Name;
}