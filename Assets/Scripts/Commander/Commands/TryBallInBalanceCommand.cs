using Cysharp.Threading.Tasks;

namespace Commander.Commands
{
    public class TryBallInBalanceCommand : ICommand
    {
        private IBallZone _ballZone;
        private PathType _sidePathType;
        private IBalance _balance;

        public TryBallInBalanceCommand(IBallZone ballZone, PathType sidePathType, IBalance balance)
        {
            _ballZone = ballZone;
            _sidePathType = sidePathType;
            _balance = balance;
        }
        
        public async UniTask Execute()
        {
            var currentBall = _ballZone.GetFirstBall();
            currentBall.StayInZone(_sidePathType);
            await _balance.TryBall(currentBall, _sidePathType);
        }
    }
}