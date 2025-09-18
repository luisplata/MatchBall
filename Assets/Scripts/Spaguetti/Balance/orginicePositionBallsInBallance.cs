using System.Collections.Generic;
using UnityEngine;

public class orginicePositionBallsInBallance : MonoBehaviour
{
    [SerializeField] private List<GameObject> ballPositions;
    [SerializeField] private float distanceBetweenBalls = 0.7f;
    [SerializeField] private Transform startPosition;
    [SerializeField] private bool dearribaabajo = true;
    private Vector3 _startPosition;

    [ContextMenu("OrginicePositions")]
    public void OrginicePositions()
    {
        _startPosition = startPosition.position;
        if (dearribaabajo)
            ballPositions.Reverse();
        foreach (var ballPosition in ballPositions)
        {
            ballPosition.transform.position = new Vector3(ballPosition.transform.position.x, startPosition.position.y,
                ballPosition.transform.position.z);
            startPosition.position = new Vector3(startPosition.position.x,
                startPosition.position.y - distanceBetweenBalls, startPosition.position.z);
        }
        startPosition.position = _startPosition;
    }
}