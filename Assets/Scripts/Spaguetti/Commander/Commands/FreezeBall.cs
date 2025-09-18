using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Commander.Commands
{
    public class FreezeBall : ICommand, ICommandPowerUp
    {
        private readonly float _timeToFreeze;
        private readonly IPlayer _player;

        public FreezeBall(float timeToFreeze, IPlayer player)
        {
            _timeToFreeze = timeToFreeze;
            _player = player;
        }

        public async UniTask Execute()
        {
            _player.DisableControls();
            Debug.Log("Freeze ball");
            _player.FreezeGame(true);
            await UniTask.Delay((int) (_timeToFreeze * 1000));
            _player.FreezeGame(false);
            Debug.Log("Unfreeze ball");
            _player.EnableControls();
        }
    }
}