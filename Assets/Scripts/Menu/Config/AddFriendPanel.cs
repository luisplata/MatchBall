using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AddFriendPanel : MonoBehaviour
{
    [SerializeField] private TMP_InputField friendId;
    [SerializeField] private Button addFriendButton;
    [SerializeField] private GameObject configPanel;
    private IConfigFriend _configFriend;
    private PlayerSessionDataCustom _player;

    public void Configure(IConfigFriend configFriend)
    {
        _configFriend = configFriend;
        addFriendButton.onClick.AddListener(() =>
        {
            Debug.Log("Add friend");
        });
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
        _configFriend.ShowAddFriendPanel();
    }

    public void HideAddFriendPanel()
    {
        _configFriend.HideAddFriendPanel();
    }
}

public interface IConfigFriend
{
    void ShowAddFriendPanel();
    void HideAddFriendPanel();
}