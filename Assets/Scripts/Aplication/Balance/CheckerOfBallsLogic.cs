using Cysharp.Threading.Tasks;

public class CheckerOfBallsLogic : ICheckerOfBallsToGetPoints
{
    public async UniTask<bool> HaveCombinations(IBalance _balance)
    {
        for (int i = 0; i < _balance.CountOfColumns(); i++)
        {
            //if the type is himself destroy only that ball
            if (_balance.GetLeftPath().TryGetBallInPosition(i, out var ballLeftHs))
            {
                if (ballLeftHs.GetId() == "himself")
                {
                    _balance.GetLeftPath().RemoveBall(i);
                    ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("pop");
                    await _balance.GetLeftPath().MoveBallsDownByIndexAsc(1, i);
                    await UniTask.WaitForSeconds(.5f);
                    _balance.AddPoints();
                    return true;
                }
            }

            if (_balance.GetRightPath().TryGetBallInPosition(i, out var ballRightHs))
            {
                if (ballRightHs.GetId() == "himself")
                {
                    _balance.GetRightPath().RemoveBall(i);
                    ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("pop");
                    await _balance.GetRightPath().MoveBallsDownByIndexAsc(1, i);
                    await UniTask.WaitForSeconds(.5f);
                    _balance.AddPoints();
                    return true;
                }
            }

            // if the left path has a ball and the above position is equal to the ball in the left path
            if (_balance.GetLeftPath().TryGetBallInPosition(i, out var ballLeftRow))
            {
                if (_balance.GetLeftPath().TryGetBallInPosition(i + 2, out var ballLeftRowAbove))
                {
                    if (
                        ballLeftRow.GetId() == ballLeftRowAbove.GetId() ||
                        ballLeftRowAbove.GetId().Contains(ballLeftRow.GetId()) ||
                        ballLeftRow.GetId().Contains(ballLeftRowAbove.GetId()) ||
                        ballLeftRow.GetId() == "all" ||
                        ballLeftRowAbove.GetId() == "all"
                    )
                    {
                        _balance.GetLeftPath().RemoveBall(i);
                        _balance.GetLeftPath().RemoveBall(i + 2);
                        ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("pop");
                        await _balance.GetLeftPath().MoveBallsDownByIndexAsc(2, i);
                        await UniTask.WaitForSeconds(.5f);
                        _balance.AddPoints();
                        return true;
                    }
                }
            }

            // if the right path has a ball and the above position is equal to the ball in the right path
            if (_balance.GetRightPath().TryGetBallInPosition(i, out var ballRightRow))
            {
                if (_balance.GetRightPath().TryGetBallInPosition(i + 2, out var ballRightRowAbove))
                {
                    if (
                        ballRightRow.GetId() == ballRightRowAbove.GetId() ||
                        ballRightRow.GetId().Contains(ballRightRowAbove.GetId()) ||
                        ballRightRowAbove.GetId().Contains(ballRightRow.GetId()) ||
                        ballRightRow.GetId() == "all" ||
                        ballRightRowAbove.GetId() == "all"
                    )
                    {
                        _balance.GetRightPath().RemoveBall(i);
                        _balance.GetRightPath().RemoveBall(i + 2);
                        ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("pop");
                        await _balance.GetRightPath().MoveBallsDownByIndexAsc(2, i);
                        await UniTask.WaitForSeconds(.5f);
                        _balance.AddPoints();
                        return true;
                    }
                }
            }

            if (_balance.GetLeftPath().TryGetBallInPosition(i, out var ballLeft) &&
                _balance.GetRightPath().TryGetBallInPosition(i, out var ballRight))
            {
                // if the balls are the same and the id is "all"
                if (
                    ballRight.GetId() == "all" ||
                    ballLeft.GetId() == "all" ||
                    ballRight.GetId() == ballLeft.GetId() ||
                    ballLeft.GetId().Contains(ballRight.GetId()) ||
                    ballRight.GetId().Contains(ballLeft.GetId())
                )
                {
                    _balance.GetLeftPath().RemoveBall(i);
                    _balance.GetRightPath().RemoveBall(i);
                    ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("pop");
                    // await _balance.MoveBallsDownByIndex(1);
                    await _balance.MoveBallsDownByIndexAsc(2, i);
                    await UniTask.WaitForSeconds(.5f);
                    _balance.AddPoints();
                    return true;
                }
            }
        }

        return false;
    }
}


public interface ICheckerOfBallsToGetPoints
{
}