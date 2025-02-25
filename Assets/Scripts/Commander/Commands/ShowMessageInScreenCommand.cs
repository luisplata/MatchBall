using Cysharp.Threading.Tasks;

namespace Commander.Commands
{
    public class ShowMessageInScreenCommand : ICommand, ICommandPowerUp
    {
        private string _message;
        private IGameManager _gameManager;

        public ShowMessageInScreenCommand(IGameManager gameManager, string message)
        {
            _message = message;
            _gameManager = gameManager;
        }

        public async UniTask Execute()
        {
            ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("PowerUpIce");
            _gameManager.ShowMessage(_message);
            await UniTask.Delay(2000);
            _gameManager.HideMessage();
        }
    }
}