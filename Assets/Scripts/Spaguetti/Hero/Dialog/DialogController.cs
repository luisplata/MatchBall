using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hero = Domain.Entities.Hero;

public class DialogController : MonoBehaviour, IDialogService
{
    [SerializeField] private GameObject panelDialog;
    [SerializeField] private CanvasGroup canvasGroupDialog;
    [SerializeField] private TextMeshProUGUI textDialog;
    [SerializeField] private Image imageDialog;

    private void Awake()
    {
        if (FindObjectsByType<DialogController>(FindObjectsSortMode.None).Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        ServiceLocator.Instance.RegisterService<IDialogService>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Instance.UnregisterService<IDialogService>();
    }

    private void Start()
    {
        panelDialog.SetActive(false);
        canvasGroupDialog.alpha = 0;
        canvasGroupDialog.interactable = false;
        canvasGroupDialog.blocksRaycasts = false;
    }

    public void StartDialog()
    {
        panelDialog.SetActive(true);
        // startdotwee to fade
        canvasGroupDialog.alpha = 1;
        canvasGroupDialog.interactable = true;
        canvasGroupDialog.blocksRaycasts = true;
    }

    public void StartDialog(Hero hero)
    {
        textDialog.text = $"Hello, I am {hero.Name}!";
        StartDialog();
    }
}