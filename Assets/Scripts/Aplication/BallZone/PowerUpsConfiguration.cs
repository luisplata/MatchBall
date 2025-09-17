using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Custom/Power up configuration")]
public class PowerUpsConfiguration : ScriptableObject
{
    [SerializeField] private PowerUpA[] powerUps;
    private Dictionary<string, PowerUpA> idToPowerUp;

    private void Awake()
    {
        idToPowerUp = new Dictionary<string, PowerUpA>(powerUps.Length);
        foreach (var powerUp in powerUps)
        {
            idToPowerUp.Add(powerUp.Id, powerUp);
        }
    }

    public PowerUpA GetPowerUpPrefabById(string id)
    {
        if (!idToPowerUp.TryGetValue(id, out var powerUp))
        {
            throw new Exception($"PowerUp with id {id} does not exit");
        }
        return powerUp;
    }

    public int GetPowerUpsCount()
    {
        return powerUps.Length;
    }

    public string GetPowerUpIdByIndex(int randomIndex)
    {
        return powerUps[randomIndex].Id;
    }

    public Dictionary<string, PowerUpA> GetPowerUps()
    {
        return idToPowerUp;
    }
}