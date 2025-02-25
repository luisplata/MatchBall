using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Reward : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI level;
    [SerializeField] private Button rewardButton;
    [SerializeField] private Slider progressBar;
    [SerializeField] private GameObject claimed, claimedIcon;
    private RewardData _rewardData;

    public void Configure(RewardData reward)
    {
        _rewardData = reward;
        level.text = reward.level.ToString();
        //Debug.Log(reward.reward);
        var a = JsonUtility.FromJson<RewardDataJson>(reward.reward);
        //Debug.Log(a.type);
        //Debug.Log(a.count);
        claimed.SetActive(false);
        claimedIcon.SetActive(false);
        progressBar.value = 0;
    }

    public RewardData RewardData => _rewardData;

    private async void CallRewarded()
    {
        //Debug.Log("Reward button clicked to be rewarded");
        //reiniciar los eventos del boton
        rewardButton.onClick.RemoveAllListeners();
        try
        {
            ServiceLocator.Instance.GetService<IMessageService>().ShowMessage("Claiming reward...", 3).Forget();
            await ServiceLocator.Instance.GetService<IHttpRequestsCustom>().PlayerRewardApi
                .ClaimReward(_rewardData.level);
            await ServiceLocator.Instance.GetService<IClaimRewardService>().ClaimReward(_rewardData);
            ServiceLocator.Instance.GetService<IMessageService>().ShowMessage("Reward claimed", 3).Forget();
        }
        catch (Exception e)
        {
            ServiceLocator.Instance.GetService<IMessageService>().ErrorMessage(e.Message, 3);
            rewardButton.onClick.AddListener(CallRewarded);
        }

        claimed.SetActive(true);
        //Debug.Log("Reward claimed");
    }

    public async UniTask AnimToReceived(float duration)
    {
        float targetValue = 1f;
        await progressBar.DOValue(targetValue, duration).SetEase(Ease.Linear).AsyncWaitForCompletion();
        claimedIcon.SetActive(true);
    }

    public async UniTask AnimToHalfReceived(int percentage, float duration)
    {
        if (percentage is < 0 or >= 100)
        {
            Debug.LogError("Percentage must be between 0 and 99");
            return;
        }

        var targetValue = percentage / 100f;
        await progressBar.DOValue(targetValue, duration).SetEase(Ease.Linear).AsyncWaitForCompletion();
    }

    public void ConfigureClaimed(bool playerRewardClaimed)
    {
        if (playerRewardClaimed)
        {
            claimed.SetActive(true);
        }
        else
        {
            rewardButton.onClick.AddListener(CallRewarded);
        }
    }
}

[Serializable]
public class RewardDataJson
{
    public string type;
    public string count;
}