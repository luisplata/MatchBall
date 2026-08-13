using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace App.UseCase
{
    public interface ILoginUseCase
    {
        UniTask LoginPlayer(string playerId);
        UniTask AddPlayer(string playerId, string nickname, string key);
        UniTask UpdatePlayerNickname(string playerId, string playerName);
        UniTask ClaimReward(int rewardDataLevel);
        UniTask AddExperience(bool b);
        UniTask AddPlayer(PlayerSessionDataCustom playerSessionData, string devPlayerKeyChangeInProduction);
    }
}