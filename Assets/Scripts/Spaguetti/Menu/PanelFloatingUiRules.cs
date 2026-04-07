using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PanelFloatingUiRules : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button[] showButton;
    [SerializeField] private Button[] closeButton;

    private bool _isShown;

    private void Show()
    {
        if (_isShown) return;
        _isShown = true;
        //move panel from right -1000 to left 0 with dotween
        panel.transform.localPosition = new Vector3(-2000, 0, 0);
        panel.transform.DOLocalMove(new Vector3(0, 0, 0), 0.5f);
    }

    private void Hide()
    {
        if (!_isShown) return;
        //move panel from right 0 to left -1000 with dotween
        panel.transform.localPosition = new Vector3(0, 0, 0);
        panel.transform.DOLocalMove(new Vector3(-2000, 0, 0),
            0.5f).onComplete += () => _isShown = false;
    }

    // Start is called before the first frame update
    void Start()
    {
        foreach (var button in showButton)
        {
            button.onClick.AddListener(Show);
        }

        foreach (var button in closeButton)
        {
            button.onClick.AddListener(Hide);
        }

        panel.transform.localPosition =
            new Vector3(-2000, 0, 0);
    }
}