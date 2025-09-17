public class PowerUpSpawner
{
    private readonly PowerUpsFactory powerUpsFactory;

    public PowerUpSpawner(PowerUpsFactory powerUpsFactory)
    {
        this.powerUpsFactory = powerUpsFactory;
    }
        
    // Logic

    public PowerUpA SpawnPowerUp(string id)
    {
        return powerUpsFactory.Create(id);
    }

    public PowerUpA SpawnPowerUp()
    {
        //get a radom powerup
        return powerUpsFactory.Create("random");
    }
}