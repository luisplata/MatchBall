using App.UseCase;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConfigRename : MonoBehaviour
{
    private IConfigRenameMediator _mediator;
    [SerializeField] private Button renameButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_InputField renameText;
    private PlayerSessionDataCustom _player;

    public void Configure(IConfigRenameMediator mediator)
    {
        _mediator = mediator;
        renameButton.onClick.AddListener(() =>
        {
            _player.playerName = renameText.text;
            SaveData().Forget();
        });
        closeButton.onClick.AddListener(() =>
        {
            renameText.text = _player.playerName;
        });
        Show();
    }


    private void Show()
    {
        _player = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData();
        renameText.text = _player.playerName;
    }

    private async UniTaskVoid SaveData()
    {
        await ServiceLocator.Instance.GetService<IStorageData>().UpdatePlayerData(_player);
        await ServiceLocator.Instance.GetService<ILoginUseCase>().UpdatePlayerNickname(_player.playerId, _player.playerName);
    }
}

public interface IConfigRenameMediator
{
}