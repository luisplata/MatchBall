using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Commander
{
    public class CommandPlayerQueue : ICommandQueue
    {
        private List<IPlayer> _players = new();

        public void AddPlayer(IPlayer playerInComing)
        {
            if (_players.Any(player => player.Id == playerInComing.Id))
            {
                //Debug.Log("Player already in queue");
                return;
            }
            
            _players.Add(playerInComing);
        }

        public void AddCommand(ICommand command, IPlayer playerId)
        {
            var player = _players.FirstOrDefault(player => player.Id == playerId.Id);
            if (player == null)
            {
                Debug.LogWarning($"ICommandQueue: player '{playerId.Id}' is not registered - command dropped.");
                return;
            }

            player.AddCommand(command);
        }

        public bool IsBusy(IPlayer player)
        {
            var p = _players.FirstOrDefault(p => p.Id == player.Id);
            return p?.IsBusy() ?? false;
        }

        public void RemovePlayer(IPlayer playerInComing)
        {
            if (_players.Any(player => player.Id == playerInComing.Id))
            {
                _players.Remove(playerInComing);
            }
        }

        public void ClearCommands(IPlayer player)
        {
            var p = _players.FirstOrDefault(p => p.Id == player.Id);
            p?.ClearCommands();
        }
    }

    public interface ICommandQueue
    {
        void AddPlayer(IPlayer playerInComing);
        void AddCommand(ICommand command, IPlayer playerId);
        bool IsBusy(IPlayer player);
        void RemovePlayer(IPlayer player);
        void ClearCommands(IPlayer player);
    }
}