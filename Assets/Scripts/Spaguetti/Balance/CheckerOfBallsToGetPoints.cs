using Cysharp.Threading.Tasks;
using UnityEngine;

public class CheckerOfBallsToGetPoints : MonoBehaviour
{
    private CheckerOfBallsLogic _checkerOfBallsLogic;

    public void Configure()
    {
        _checkerOfBallsLogic = new CheckerOfBallsLogic();
    }

    public async UniTask<bool> HaveCombinations(IBalance balance)
    {
        return await _checkerOfBallsLogic.HaveCombinations(balance);
    }
}