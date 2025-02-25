using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Commander.Commands
{
    public class UseLeadPowerUpInPath : ICommand, ICommandPowerUp
    {
        private IPowerUp _powerup;
        private IBalance _balance;

        public UseLeadPowerUpInPath(IPowerUp powerUp, IBalance balance)
        {
            _powerup = powerUp;
            _balance = balance;
        }

        public async UniTask Execute()
        {
            Path path = null;
            Path pathHeigher = null;

            if (_balance.GetLeftPath().GetOnlyBalls() < _balance.GetRightPath().GetOnlyBalls())
            {
                //left path is less than right path
                path = _balance.GetLeftPath();
                pathHeigher = _balance.GetRightPath();
            }
            else if (_balance.GetLeftPath().GetOnlyBalls() > _balance.GetRightPath().GetOnlyBalls())
            {
                //right path is less than left path
                path = _balance.GetRightPath();
                pathHeigher = _balance.GetLeftPath();
            }
            else
            {
                //both paths have the same amount of balls and get the random path
                if (Random.Range(0, 2) == 0)
                {
                    path = _balance.GetLeftPath();
                    pathHeigher = _balance.GetRightPath();
                }
                else
                {
                    path = _balance.GetRightPath();
                    pathHeigher = _balance.GetLeftPath();
                }
            }

            /*await path.TryBall(_powerup.GetBall());
            await UniTask.WhenAll(
                path.MoveBallsDownByIndex(1),
                pathHeigher.MoveBallsUpByIndex(1)
            );*/
            await _balance.TryBall(_powerup.GetBall(), path.GetPath());
            int lastball = pathHeigher.GetLastIndexBall();
            Ball ball = pathHeigher.ExtractBall(lastball);
            await ball.MoveBall(ball.gameObject.transform.position,
                pathHeigher.GetPointToTransition().transform.position);
            await ball.MoveBall(ball.gameObject.transform.position,
                _balance.GetPlayer().GetBallZone().GetBallPointToSpawn().transform.position);
            await _balance.BalanceBothPaths();
            var otherPlayer = _balance.GetPlayer().GetGameManager().GetOtherPlayer();
            ServiceLocator.Instance.GetService<ICommandQueue>()
                .AddCommand(new SetBallInFirstPositionInQueue(ball, otherPlayer.GetBallZone()), otherPlayer);
            ServiceLocator.Instance.GetService<ICommandQueue>().AddCommand(
                new TryBallInBalanceCommand(otherPlayer.GetBallZone(), path.GetPath(), otherPlayer.GetBalance()),
                otherPlayer);
            await _balance.BalanceBothPaths();
        }
    }
}