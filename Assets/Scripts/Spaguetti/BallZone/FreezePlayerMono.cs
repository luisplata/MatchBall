using UnityEngine;

public class FreezePlayerMono : MonoBehaviour
{
    [SerializeField] private GameObject zone;
    [SerializeField] private Sprite freezeSprite;
    private GameObject _freezeZone;

    public void Freeze()
    {
        _freezeZone = new GameObject("FreezeZone")
        {
            transform =
            {
                position = zone.transform.position,
                localScale = zone.transform.localScale
            }
        };
        var spriteRenderer = _freezeZone.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 100;
        spriteRenderer.sprite = freezeSprite;
    }

    public void Unfreeze()
    {
        Destroy(_freezeZone);
    }
}