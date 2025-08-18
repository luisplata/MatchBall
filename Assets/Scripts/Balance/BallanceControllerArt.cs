using UnityEngine;

public class BallanceControllerArt : MonoBehaviour
{
    [SerializeField] private LineRenderer lineOfBallance;
    [SerializeField] private Balance balance;
    [SerializeField] private Transform leftBase, rightBase;
    [SerializeField] private Transform anchorLeft, anchorRight;

    // Update is called once per frame
    void Update()
    {
        //Quiero que los dos puntos del linerender coinsidan con los anchorLeft y anchorRight
        lineOfBallance.SetPosition(0, anchorLeft.position);
        lineOfBallance.SetPosition(1, anchorRight.position);
        //Quiero que las bases coincidan con el centro de los paths
        leftBase.position = balance.GetLeftPath().GetCenterLeftPath().position;
        rightBase.position = balance.GetRightPath().GetCenterLeftPath().position;
    }
}
