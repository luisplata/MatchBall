using System;
using Commander;
using Commander.Commands;
using Cysharp.Threading.Tasks;
using Domain.Ai;
using Domain.Services;
using UnityEngine;

/// <summary>
/// How the rival's decision cycle is doing. <see cref="Stopped"/> is teardown only; a freeze
/// produces <see cref="Suspended"/> and never destroys the cycle.
/// </summary>
public enum AiState
{
    Waiting,
    Suspended,
    Stopped,
    GameOver
}

/// <summary>
/// The rival's decision cycle: one async loop with an explicit state, instead of a chain of
/// TeaTime graphs. The old chain broke on <c>ClearCommands</c> and nothing could restart it
/// because its first state had already returned; a loop can be suspended and resumed in place.
/// </summary>
public sealed class AiMachine : IAiMachine
{
    private const float MinCadenceSeconds = 0.25f;

    /// <summary>How much of the level's beat the rival keeps when its board is about to lose.</summary>
    private const float DangerCadenceFactor = 0.35f;

    private readonly IPlayer _player;
    private readonly IGameManager _gameManager;
    private readonly int _seed;
    private readonly float _difficulty;
    private readonly float _cadenceSeconds;

    private bool _canPlay = true;
    private bool _suspended;
    private bool _running;
    private int _loop;
    private int _decisionCount;
    private AiState _state = AiState.Stopped;
    private float _nextCadence;

    public AiMachine(IPlayer player, IGameManager gameManager, int seed, float difficulty,
        float cadenceSeconds)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _gameManager = gameManager;
        _seed = seed;
        _difficulty = Mathf.Clamp01(difficulty);
        _cadenceSeconds = Mathf.Max(MinCadenceSeconds, cadenceSeconds);
        _nextCadence = _cadenceSeconds;
    }

    public bool CanPlay()
    {
        return _canPlay;
    }

    /// <summary>
    /// Controls on/off, mapped onto resume/suspend. The freeze uses this path, so unfreezing can
    /// never revive a stopped or finished machine.
    /// </summary>
    public void SetCanPlay(bool canplay)
    {
        _canPlay = canplay;
        if (canplay) Resume();
        else Suspend();
    }

    public void StartAI()
    {
        if (_running) return;

        _running = true;
        _suspended = false;
        _loop++;
        _state = AiState.Waiting;
        RunAsync(_loop).Forget();
    }

    public void StopAI()
    {
        _running = false;
        _suspended = false;
        _loop++;
        _state = AiState.Stopped;
    }

    public void Suspend()
    {
        if (!_running) return;
        _suspended = true;
        _state = AiState.Suspended;
    }

    public void Resume()
    {
        if (!_running) return;
        _suspended = false;
        _state = AiState.Waiting;
    }

    /// <summary>Fin de partida: el bucle no vuelve a decidir y un unfreeze no lo revive.</summary>
    public void NotifyGameOver()
    {
        _running = false;
        _suspended = false;
        _loop++;
        _state = AiState.GameOver;
    }

    public string GetCurrentState()
    {
        return _state.ToString().ToUpperInvariant();
    }

    private async UniTaskVoid RunAsync(int loop)
    {
        await UniTask.WaitUntil(() => FirstPlayerReady() || loop != _loop);

        while (loop == _loop)
        {
            if (_player.IsGameOver())
            {
                NotifyGameOver();
                return;
            }

            await UniTask.WaitUntil(() => !_suspended || loop != _loop);
            if (loop != _loop) return;

            // Delta time, so a paused game does not burn the rival's thinking window.
            await UniTask.Delay((int) (_nextCadence * 1000f));
            if (loop != _loop) return;

            // The freeze may have started while thinking: wait it out instead of playing through.
            await UniTask.WaitUntil(() => !_suspended || loop != _loop);
            if (loop != _loop) return;

            // The board is the real limiter: never stack a decision on a command still animating.
            await UniTask.WaitUntil(() => !_player.IsBusy() || loop != _loop);
            if (loop != _loop) return;

            if (_player.IsGameOver())
            {
                NotifyGameOver();
                return;
            }

            DecideAndAct();
            await UniTask.Yield();
        }
    }

    private bool FirstPlayerReady()
    {
        var first = _gameManager != null ? _gameManager.GetFirstPlayer() : null;
        return first != null && first.IsReadyToPlay();
    }

    private void DecideAndAct()
    {
        var snapshot = AiSnapshotBuilder.Build(_player);
        _nextCadence = NextCadence(snapshot);

        var decision = AiPolicy.Decide(snapshot, _difficulty, NextSeed());
        LogDecision(snapshot, decision);

        switch (decision.Action.Kind)
        {
            case AiActionKind.TapLeft:
                QueueTap(PathType.Left);
                break;
            case AiActionKind.TapRight:
                QueueTap(PathType.Right);
                break;
            case AiActionKind.UsePowerUp:
                ClickPowerUp(decision.Action.PowerUpIndex);
                break;
        }
    }

    /// <summary>
    /// Reaction floor for the wait that follows this move. A fixed beat made the rival play a fifth
    /// as often as a human; under pressure it now answers in a third of its level's beat, and the
    /// floor keeps the tap readable instead of robotic.
    /// </summary>
    private float NextCadence(AiBoardSnapshot snapshot)
    {
        var cadence = snapshot.Risk == BalanceRisk.Strong
            ? _cadenceSeconds * DangerCadenceFactor
            : _cadenceSeconds;

        return Mathf.Max(MinCadenceSeconds, cadence);
    }

    private void QueueTap(PathType side)
    {
        ServiceLocator.Instance.GetService<ICommandQueue>()
            .AddCommand(new TryBallInBalanceCommand(_player.GetBallZone(), side, _player.GetBalance()),
                _player);
    }

    private void ClickPowerUp(int index)
    {
        var powerUps = _player.GetBallZone().GetPowerUps();
        if (index < 0 || index >= powerUps.Count) return;

        var powerUp = powerUps[index];
        if (powerUp != null) powerUp.Click();
    }

    private int NextSeed()
    {
        _decisionCount++;
        return unchecked(_seed + _decisionCount * 7919);
    }

    private static void LogDecision(AiBoardSnapshot snapshot, AiDecision decision)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[AiMachine] {decision.Action.Kind} score={decision.Score} " +
                  $"cap={snapshot.MinCapacityMoves} risk={snapshot.Risk} :: {decision.Reason}");
#endif
    }
}

public interface IAiMachine
{
    bool CanPlay();
    void SetCanPlay(bool canplay);
    void StartAI();
    void StopAI();
    void Suspend();
    void Resume();
    void NotifyGameOver();
    string GetCurrentState();
}
