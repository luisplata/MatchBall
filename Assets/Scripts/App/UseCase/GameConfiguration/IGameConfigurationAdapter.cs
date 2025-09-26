namespace App.UseCase.GameConfiguration
{
    public interface IGameConfigurationAdapter
    {
        Domain.Entities.GameConfiguration LoadConfig();
    }
}