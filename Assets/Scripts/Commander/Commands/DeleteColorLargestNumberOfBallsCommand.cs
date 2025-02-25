using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Commander.Commands
{
    public class DeleteColorLargestNumberOfBallsCommand : ICommand, ICommandPowerUp
    {
        private IBalance _balance;
        private readonly GameObject _vfxToDestroyBalls;

        public DeleteColorLargestNumberOfBallsCommand(IBalance balance, GameObject vfxToDestroyBalls)
        {
            _balance = balance;
            _vfxToDestroyBalls = vfxToDestroyBalls;
        }
        
        public async UniTask Execute()
        {
            var colorToDelete = _balance.GetTheColorOfLargestNumberOfBalls();
            //Debug.Log($"Deleting color: {colorToDelete}");
            await _balance.DeleteColor(colorToDelete, _vfxToDestroyBalls);
        }
    }
}