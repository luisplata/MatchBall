using UnityEngine;

public class PowerUpARay : PowerUpA
{
    [SerializeField] private GameObject vfxToDestroyBalls;
    protected override void OnActionWithClick()
    {
        //Debug.Log("PowerUpRay");
        _ballZone.DeleteAllBallsWithHaveMoreSameColor(vfxToDestroyBalls);
        gameObject.SetActive(false);
    }
}