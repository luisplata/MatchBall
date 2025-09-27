using System;

namespace Spaguetti.Services.Session
{
    [Serializable]
    public class PlayerReward
    {
        public int id;
        public string playerId;
        public int rewardId;
        public DateTime awarded_at;
        public bool claimed;
    }
}