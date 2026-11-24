using System;
using System.Collections.Generic;
using Domain.Ai;

/// <summary>
/// Translates the rival's live board into the pure snapshot the policy reads. This is the only
/// place that knows about <see cref="Path"/>, <see cref="Ball"/> and <see cref="PowerUpA"/>.
/// </summary>
internal static class AiSnapshotBuilder
{
    public static AiBoardSnapshot Build(IPlayer player)
    {
        var balance = player.GetBalance();
        var zone = player.GetBallZone();

        // Peek, never GetFirstBall(): that one pops the ball, moves the queue and fires a sound.
        var incoming = zone.PeekFirstBall();
        var incomingColor = incoming != null ? incoming.GetColor() : string.Empty;

        return new AiBoardSnapshot(
            ToLane(balance.GetLeftPath()),
            ToLane(balance.GetRightPath()),
            balance.CountOfColumns(),
            incomingColor,
            ToPowerUps(zone));
    }

    private static AiLane ToLane(Path path)
    {
        var nextFree = path.GetNextFreeIndex();
        var counts = new Dictionary<string, int>();
        var byIndex = new List<string>();

        var last = Math.Max(0, nextFree);
        for (var index = 0; index <= last; index++)
        {
            var color = string.Empty;
            if (path.TryGetBallInPosition(index, out var ball) && ball != null && !ball.IsPowerUp())
            {
                color = ball.GetColor() ?? string.Empty;
                if (color.Length > 0)
                {
                    counts.TryGetValue(color, out var current);
                    counts[color] = current + 1;
                }
            }

            byIndex.Add(color);
        }

        return new AiLane(path.GetOnlyBalls(), nextFree, counts, byIndex);
    }

    private static IReadOnlyList<AiPowerUpOption> ToPowerUps(IBallZone zone)
    {
        var powerUps = zone.GetPowerUps();
        var options = new List<AiPowerUpOption>(powerUps.Count);
        for (var i = 0; i < powerUps.Count; i++)
        {
            var powerUp = powerUps[i];
            if (powerUp == null) continue;
            options.Add(new AiPowerUpOption(i, KindOf(powerUp)));
        }

        return options;
    }

    /// <summary>
    /// <see cref="Ball.GetId"/> returns the ball type (a colour, or "all" for joker/lead), not the
    /// orb class, so the kind has to be resolved from the concrete component.
    /// </summary>
    private static string KindOf(PowerUpA powerUp)
    {
        if (powerUp is PowerUpARay) return AiPowerUpKind.Ray;
        if (powerUp is PowerUpAIceBall) return AiPowerUpKind.Ice;
        if (powerUp is PowerUpAJoker) return AiPowerUpKind.Joker;
        if (powerUp is PowerUpALeadBall) return AiPowerUpKind.Lead;
        if (powerUp is PowerUpAShell) return AiPowerUpKind.Shell;
        return AiPowerUpKind.Unknown;
    }
}
