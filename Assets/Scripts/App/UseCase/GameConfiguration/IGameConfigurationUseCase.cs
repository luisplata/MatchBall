namespace App.UseCase.GameConfiguration
{
    public interface IGameConfigurationUseCase
    {
        void LoadConfig();
        Domain.Entities.GameConfiguration GetConfig();
    }
}