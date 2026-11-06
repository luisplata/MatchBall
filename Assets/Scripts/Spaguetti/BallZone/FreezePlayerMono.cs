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
                localScale = zone.transform.localScale,
                // The zone is a child of its board: after the P2 flip its up axis is the board's,
                // so the overlay must inherit the rotation or it renders upside down.
                rotation = zone.transform.rotation
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