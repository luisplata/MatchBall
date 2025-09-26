using System;
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
    [SerializeField] private Image rewardIcon;
    [SerializeField] private TextMeshProUGUI rewardText;
    private RewardData _rewardData;

    public async UniTask Configure(RewardData reward)
    {
        _rewardData = reward;
        level.text = reward.level.ToString();
        //Debug.Log(reward.reward);
        var rewardData = JsonUtility.FromJson<RewardDataJson>(reward.reward);
        var data = await ServiceLocator.Instance.GetService<IRewardFactoryService>().GetRewardConfigData(rewardData);
        rewardIcon.sprite = data.image;
        rewardText.text = data.value;
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
            await ServiceLocator.Instance.GetService<IHttpRequestsCustom>().PlayerRewardApi
                .ClaimReward(_rewardData.level);
            var regard = await ServiceLocator.Instance.GetService<IClaimRewardService>().ClaimReward(_rewardData);
            ServiceLocator.Instance.GetService<IMessageService>().ShowMessage($"Reward {regard.type} claimed", 3).Forget();
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

[Serializable]
public class RewardConfigData
{
    public Sprite image;
    public string value;
}