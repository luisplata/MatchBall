using UnityEngine;

public class BallanceControllerArt : MonoBehaviour
{
    [SerializeField] private LineRenderer lineOfBallance;
    [SerializeField] private Balance balance;
    [SerializeField] private Transform leftBase, rightBase;
    [SerializeField] private Transform anchorLeft, anchorRight;

    // Update is called once per frame
    public void UpdatePositions()
    {
        //Quiero que los dos puntos del linerender coinsidan con los anchorLeft y anchorRight
        //left path
        if (balance.GetLeftPath().GetLastIndexBall() < 0)
        {
            anchorLeft.position = balance.GetLeftPath().GetBasePosition().position;
        }
        else
        {
            anchorLeft.position = balance.GetLeftPath().GetLastBall().BasePosition.gameObject
                .transform.position;
        }

        //right path
        if (balance.GetRightPath().GetLastIndexBall() < 0)
        {
            anchorRight.position = balance.GetRightPath().GetBasePosition().position;
        }
        else
        {
            anchorRight.position = balance.GetRightPath().GetLastBall().BasePosition.gameObject
                .transform.position;
        }

        //set positions of line renderer
        lineOfBallance.SetPosition(0, leftBase.position);
        lineOfBallance.SetPosition(1, rightBase.position);
        //Quiero que las bases coincidan con el centro de los paths
        // leftBase.position = balance.GetLeftPath().GetCenterLeftPath().position;
        // rightBase.position = balance.GetRightPath().GetCenterLeftPath().position;
    }
}