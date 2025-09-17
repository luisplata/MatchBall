using Cysharp.Threading.Tasks;

namespace Commander.Commands
{
    public class SetBallInFirstPositionInQueue : ICommand
    {
        private readonly Ball _ball;
        private readonly IBallZone _ballZone;

        public SetBallInFirstPositionInQueue(Ball ball, IBallZone ballZone)
        {
            _ball = ball;
            _ballZone = ballZone;
        }

        public async UniTask Execute()
        {
            await _ball.MoveBall(_ball.gameObject.transform.position,
                _ballZone.GetBallPointToSpawn().transform.position);
            await _ball.MoveBall(_ball.gameObject.transform.position,
                _ballZone.GetBallPositionGameObject(0).transform.position);
            _ballZone.GetBall(0).RemoveBall();
            _ballZone.SetBall(0, _ball);
        }
    }
}