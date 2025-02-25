using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Custom/Power up configuration")]
public class PowerUpsConfiguration : ScriptableObject
{
    [SerializeField] private PowerUp[] powerUps;
    private Dictionary<string, PowerUp> idToPowerUp;

    private void Awake()
    {
        idToPowerUp = new Dictionary<string, PowerUp>(powerUps.Length);
        foreach (var powerUp in powerUps)
        {
            idToPowerUp.Add(powerUp.Id, powerUp);
        }
    }

    public PowerUp GetPowerUpPrefabById(string id)
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

    public Dictionary<string, PowerUp> GetPowerUps()
    {
        return idToPowerUp;
    }
}