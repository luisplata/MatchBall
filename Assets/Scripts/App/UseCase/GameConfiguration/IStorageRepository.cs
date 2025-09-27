namespace App.UseCase.GameConfiguration
{
    public interface IStorageRepository
    {
        void SaveConfig(Domain.Entities.GameConfiguration config);
        Domain.Entities.GameConfiguration GetConfig();
    }
}