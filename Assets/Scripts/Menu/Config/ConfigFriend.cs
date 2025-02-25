using UnityEngine;

public class ConfigFriend : MonoBehaviour, IConfigFriend
{
    [SerializeField] private GameObject friendPrefab;
    [SerializeField] private AddFriendPanel addFriendPanel;
    [SerializeField] private GameObject configPanel;
    private IConfigFriendMediator _mediator;
    private PlayerSessionDataCustom _player;

    public void Configure(IConfigFriendMediator configFriend)
    {
        _mediator = configFriend;
        addFriendPanel.Configure(this);
    }


    public void Show()
    {
        configPanel.SetActive(true);

        _player = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData();
    }

    public void Hide()
    {
        configPanel.SetActive(false);
    }

    public void ShowAddFriendPanel()
    {
        addFriendPanel.Show();
    }

    public void HideAddFriendPanel()
    {
        addFriendPanel.Hide();
    }

    public void CopyPlayerId()
    {
        //Need to copy the player id to the clipboard
        var playerId = _player.playerId;
        GUIUtility.systemCopyBuffer = playerId;
        ServiceLocator.Instance.GetService<IMessageService>().ShowMessage("Player Id copied to clipboard");
    }
}

public interface IConfigFriendMediator
{
}