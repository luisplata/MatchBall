using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Commander.Commands
{
    public class SendShellBallToEnemy : ICommand, ICommandPowerUp
    {
        private IPowerUp _powerup;
        private IBalance _balance;
        private float _scaleToShell;
        private GameObject _imageToShell;
        private IPlayer _player;

        public SendShellBallToEnemy(IPowerUp powerUp, IBalance balance, float scaleToShell, GameObject imageToShell, IPlayer player)
        {
            _powerup = powerUp;
            _balance = balance;
            _scaleToShell = scaleToShell;
            _imageToShell = imageToShell;
            _player = player;
        }

        public async UniTask Execute()
        {
            await _powerup.GetBall().MoveBall(_powerup.GetBall().gameObject.transform.position,
                _balance.GetPlayer().GetBallZone().GetBallPointToSpawn().transform.position);
            var otherPlayer = _balance.GetPlayer().GetGameManager().GetOtherPlayer(_player);
            Path path = null;
            if (Random.Range(0, 2) == 0)
            {
                path = _balance.GetLeftPath();
            }
            else
            {
                path = _balance.GetRightPath();
            }

            _powerup.GetBall().SetScale(_scaleToShell);
            ServiceLocator.Instance.GetService<ICommandQueue>().AddCommand(
                new SetBallInFirstPositionInQueue(_powerup.GetBall(), otherPlayer.GetBallZone()), otherPlayer);
            ServiceLocator.Instance.GetService<ICommandQueue>().AddCommand(
                new TryBallInBalanceCommand(otherPlayer.GetBallZone(), path.GetPath(), otherPlayer.GetBalance()),
                otherPlayer);
        }
    }
}