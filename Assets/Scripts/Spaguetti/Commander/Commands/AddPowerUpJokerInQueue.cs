using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace Commander.Commands
{
    public class AddPowerUpJokerInQueue : ICommand, ICommandPowerUp
    {
        private IBallZone _ballZone;
        private IPowerUp _powerup;

        public AddPowerUpJokerInQueue(IBallZone ballZone, IPowerUp powerup)
        {
            _ballZone = ballZone;
            _powerup = powerup;
        }

        public async UniTask Execute()
        {
            _ballZone.DecrementOverFlow();
            //move all balls one position to up
            await MoveAllBallsInQueueOnePositionUp();

            //delete the last ball
            var lastBall = _ballZone.GetLastBall();
            _ballZone.SetLastBall(null);
            lastBall.RemoveBall();

            //add the power up in the first position
            _ballZone.SetBall(0, _powerup.GetBall());
            ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("PowerUpJoker");
            await _powerup.GetBall().MoveBall(_powerup.GetBall().transform.position,
                _ballZone.GetBallPositionGameObject(0).transform.position);
        }

        private async UniTask MoveAllBallsInQueueOnePositionUp()
        {
            for (int i = _ballZone.GetCountOfBalls(); i >= 0; i--)
            {
                var ball = _ballZone.GetBall(i);
                if (ball == null) continue;
                await ball.MoveBall(ball.transform.position,
                    _ballZone.GetBallPositionGameObject(i + 1).transform.position);
                _ballZone.SetBall(i + 1, ball);
                _ballZone.SetBall(i, null);
            }
        }
    }
}