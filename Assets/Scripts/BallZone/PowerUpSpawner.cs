public class PowerUpSpawner
{
    private readonly PowerUpsFactory powerUpsFactory;

    public PowerUpSpawner(PowerUpsFactory powerUpsFactory)
    {
        this.powerUpsFactory = powerUpsFactory;
    }
        
    // Logic

    public PowerUp SpawnPowerUp(string id)
    {
        return powerUpsFactory.Create(id);
    }

    public PowerUp SpawnPowerUp()
    {
        //get a radom powerup
        return powerUpsFactory.Create("random");
    }
}