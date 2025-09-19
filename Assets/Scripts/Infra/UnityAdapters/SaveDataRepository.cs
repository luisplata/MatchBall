using App.UseCase.GameConfiguration;
using Domain.Entities;
using Newtonsoft.Json;
using UnityEngine;

namespace Infra.UnityAdapters
{
    public class SaveDataRepository : IStorageRepository
    {
        private string json;

        public void SaveConfig(GameConfiguration config)
        {
            json = JsonConvert.SerializeObject(config, Formatting.Indented);
        }

        public GameConfiguration GetConfig()
        {
            return JsonConvert.DeserializeObject<GameConfiguration>(json);
        }
    }
}