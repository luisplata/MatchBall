using UnityEngine;

public class PowerUpRay : PowerUp
{
    [SerializeField] private GameObject vfxToDestroyBalls;
    protected override void OnActionWithClick()
    {
        //Debug.Log("PowerUpRay");
        _ballZone.DeleteAllBallsWithHaveMoreSameColor(vfxToDestroyBalls);
        gameObject.SetActive(false);
    }
}