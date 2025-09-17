using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ConfigAudio : MonoBehaviour
{
    private IConfigAudioMediator _mediator;
    [SerializeField] private Button muteButton;
    [SerializeField] private Button unmuteButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Slider volumeSlider;
    private bool _isMuted;
    private float _volume;
    private PlayerSessionDataCustom _player;

    public void Configure(IConfigAudioMediator mediator)
    {
        _mediator = mediator;

        muteButton.onClick.AddListener(() =>
        {
            _isMuted = !_isMuted;
            ChangeUiForMute();
        });
        closeButton.onClick.AddListener(SaveData);

        volumeSlider.onValueChanged.AddListener((value) => { _volume = value; });

        _player = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData();
        _isMuted = _player.isMute;
        _volume = _player.volume;

        ChangeUiForMute();
        volumeSlider.value = _volume;
    }

    private void ChangeUiForMute()
    {
        if (_isMuted)
        {
            muteButton.gameObject.SetActive(false);
            unmuteButton.gameObject.SetActive(true);
        }
        else
        {
            muteButton.gameObject.SetActive(true);
            unmuteButton.gameObject.SetActive(false);
        }
    }

    public void SaveData()
    {
        //save changes
        _player.isMute = _isMuted;
        _player.volume = _volume;
        ServiceLocator.Instance.GetService<IStorageData>().UpdatePlayerData(_player).Forget();
    }
}