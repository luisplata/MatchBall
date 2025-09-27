using System;
using App.UseCase.PowerUps;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PowerUpSelector : MonoBehaviour
{
    [SerializeField] private Image powerUpImage;
    [SerializeField] private Image imageSelected;
    [SerializeField] private Button powerUpButton;
    [SerializeField] private string powerUpName;
    [SerializeField] private TextMeshProUGUI powerUpQuantity;
    private bool _isSelected;

    public void Configuration(Sprite spritem, string powerUpName, bool selected, int quantity)
    {
        this.powerUpName = powerUpName;
        powerUpImage.sprite = spritem;
        imageSelected.gameObject.SetActive(selected);
        powerUpButton.onClick.AddListener(SelectPowerUp);
        _isSelected = selected;
        powerUpQuantity.text = $"x{quantity}";
    }

    private void SelectPowerUp()
    {
        _isSelected = !_isSelected;
        imageSelected.gameObject.SetActive(ServiceLocator.Instance.GetService<IPowerUpsUseCase>()
            .PowerUpSelected(powerUpName, _isSelected));
    }
}