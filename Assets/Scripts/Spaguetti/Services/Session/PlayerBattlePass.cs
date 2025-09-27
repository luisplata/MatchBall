using System;
using System.Collections.Generic;

namespace Spaguetti.Services.Session
{
    [Serializable]
    public class PlayerBattlePass
    {
        public int id;
        public string playerId;
        public int level;
        public int experience;
        public DateTime created_at;
        public DateTime updated_at;
        public List<PlayerReward> playerRewards;
    }
}