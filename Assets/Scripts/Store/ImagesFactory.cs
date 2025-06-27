using UnityEngine;

public class ImagesFactory : MonoBehaviour
{
    [SerializeField] private Sprite sprite, sprite2, sprite3, sprite4;

    public Sprite GetSprite(string id)
    {
        switch (id)
        {
            case "ICE":
                return sprite;
            case "RAY":
                return sprite2;
            case "LEAD":
                return sprite3;
            case "JOKER":
                return sprite4;
            default:
                Debug.LogWarning($"Sprite with id {id} not found, returning default sprite.");
                return sprite; // Return a default sprite or handle it as needed
        }
    }
}