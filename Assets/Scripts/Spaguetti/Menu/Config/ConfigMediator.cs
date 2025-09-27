using UnityEngine;

public class ConfigMediator : MonoBehaviour, IConfigMediator, IConfigAudioMediator, IConfigLanguageMediator,
    IConfigRenameMediator, IConfigFriendMediator
{
    [Header("Config Audio")] [SerializeField]
    private ConfigAudio audio;

    [Header("Config Language")] [SerializeField]
    private ConfigLanguage language;

    [Header("Config Rename")] [SerializeField]
    private ConfigRename rename;
    
    // [Header("Fiends")] [SerializeField]
    // private ConfigFriend friend;

    private void Start()
    {
        audio.Configure(this);
        language.Configure(this);
        rename.Configure(this);
        //friend.Configure(this);

    }

}