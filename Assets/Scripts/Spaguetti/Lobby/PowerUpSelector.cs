using System;
using UnityEngine;
using UnityEngine.UI;

public class PowerUpSelector : MonoBehaviour
{
    [SerializeField] private Image powerUpImage;
    [SerializeField] private Image imageSelected;
    [SerializeField] private Button powerUpButton;
    [SerializeField] private string powerUpName;
    private bool _isSelected;

    public void Configuration(Sprite spritem, string powerUpName, bool selected)
    {
        this.powerUpName = powerUpName;
        powerUpImage.sprite = spritem;
        imageSelected.gameObject.SetActive(selected);
        powerUpButton.onClick.AddListener(SelectPowerUp);
        _isSelected = selected;
    }

    private void SelectPowerUp()
    {
        _isSelected = !_isSelected;
        imageSelected.gameObject.SetActive(ServiceLocator.Instance.GetService<IPlayerSessionService>()
            .PowerUpSelected(powerUpName, _isSelected));
    }
}