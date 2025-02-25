using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using System.Threading;
using Commander.Commands;

namespace Commander
{
    public class CommandQueue
    {
        private Queue<ICommand> _commands = new();
        private bool _isCommandExecuting;
        private CancellationTokenSource _cancellationTokenSource;
        public bool IsCancelled { get; private set; }


        public void AddCommand(ICommand command)
        {
            _commands.Enqueue(command);
            ExecuteCommand().Forget();
        }

        public bool IsBusy()
        {
            return _isCommandExecuting;
        }

        public void ClearCommands()
        {
            var powerUpCommands = new Queue<ICommand>(_commands.Where(command => command is ICommandPowerUp));
            _commands = powerUpCommands;
        }

        private async UniTask ExecuteCommand()
        {
            if (_isCommandExecuting) return;

            _cancellationTokenSource = new CancellationTokenSource();
            IsCancelled = false;

            while (_commands.Count > 0)
            {
                _isCommandExecuting = true;
                var command = _commands.Dequeue();
                await command.Execute().AttachExternalCancellation(_cancellationTokenSource.Token);
            }

            _isCommandExecuting = false;
        }
    }
}