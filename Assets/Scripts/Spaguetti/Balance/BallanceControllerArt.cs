using UnityEngine;

public class BallanceControllerArt : MonoBehaviour
{
    [SerializeField] private LineRenderer lineOfBallance, lineaBase;
    [SerializeField] private Balance balance;
    [SerializeField] private Transform leftBase, rightBase;
    [SerializeField] private Transform anchorLeft, anchorRight;

    private void Start()
    {
        balance.AddBallanceArt(this);
    }

    // Update is called once per frame
    public void UpdatePositions()
    {
        //Scene teardown destroys this controller together with the paths; a late
        //callback must not touch path points, balls or anchors.
        if (this == null) return;

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

        lineOfBallance.SetPosition(0, leftBase.position);
        lineOfBallance.SetPosition(1, rightBase.position);
        var mitad = GetCenterByTwoPoints(leftBase.position, rightBase.position);
        var pointDown = AddInYPosition(-3, mitad);
        lineaBase.SetPosition(0, mitad);
        lineaBase.SetPosition(1, pointDown);
    }

    private Vector3 GetCenterByTwoPoints(Vector3 a, Vector3 b)
    {
        return new Vector3((a.x + b.x) / 2f, (a.y + b.y) / 2f, (a.z + b.z) / 2f);
    }

    private Vector3 AddInYPosition(float yPosition, Vector3 position)
    {
        return position + new Vector3(0, yPosition, 0);
    }
}