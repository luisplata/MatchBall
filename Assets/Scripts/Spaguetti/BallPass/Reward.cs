using System;
using App.UseCase;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Domain.Entities;
using Newtonsoft.Json.Linq;
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
    private bool _isClaiming;

    public async UniTask Configure(RewardData reward)
    {
        _rewardData = reward;
        level.text = reward.Level.ToString();
        Debug.Log(reward.Reward);
        var rewardData = JsonUtility.FromJson<RewardDataJson>(reward.Reward);
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
        if (_isClaiming) return;

        _isClaiming = true;
        rewardButton.interactable = false;
        //reiniciar los eventos del boton
        rewardButton.onClick.RemoveAllListeners();

        try
        {
            var reward = await ServiceLocator.Instance.GetService<IClaimRewardService>().ClaimReward(_rewardData);
            // Persist the claim only after the reward was granted, so a failed grant stays claimable.
            await ServiceLocator.Instance.GetService<ILoginUseCase>().ClaimReward(_rewardData.Level);

            claimed.SetActive(true);
            ServiceLocator.Instance.GetService<IMessageService>().ShowMessage($"Reward {reward.type} claimed", 3)
                .Forget();
        }
        catch (Exception e)
        {
            rewardButton.interactable = true;
            rewardButton.onClick.AddListener(CallRewarded);
            ServiceLocator.Instance.GetService<IMessageService>().ErrorMessage(e.Message, 3).Forget();
        }
        finally
        {
            _isClaiming = false;
        }
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
    public string body;
}

[Serializable]
public class RewardConfigData
{
    public Sprite image;
    public string value;
}