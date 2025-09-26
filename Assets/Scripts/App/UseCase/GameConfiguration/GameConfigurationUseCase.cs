namespace App.UseCase.GameConfiguration
{
    public class GameConfigurationUseCase : IGameConfigurationUseCase
    {
        private readonly IGameConfigurationAdapter _gameConfigurationAdapter;
        private readonly IStorageRepository _storageRepository;

        public GameConfigurationUseCase(IGameConfigurationAdapter gameConfigurationAdapter,
            IStorageRepository storageRepository)
        {
            _gameConfigurationAdapter = gameConfigurationAdapter;
            _storageRepository = storageRepository;
        }

        public void LoadConfig()
        {
            var config = _gameConfigurationAdapter.LoadConfig();
            _storageRepository.SaveConfig(config);
        }

        public Domain.Entities.GameConfiguration GetConfig()
        {
            return _storageRepository.GetConfig();
        }
    }
}