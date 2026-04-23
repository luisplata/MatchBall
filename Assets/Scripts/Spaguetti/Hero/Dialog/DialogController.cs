using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogController : MonoBehaviour
{
    [SerializeField] private GameObject panelDialog;
    [SerializeField] private CanvasGroup canvasGroupDialog;
    [SerializeField] private TextMeshProUGUI textDialog;
    [SerializeField] private Image imageDialog;

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
}
