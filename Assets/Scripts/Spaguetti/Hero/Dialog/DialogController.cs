using System;
using System.Collections.Generic;
using System.Linq;
using App.Ports;
using Cysharp.Threading.Tasks;
using Domain.Entities;
using Spaguetti.Hero;
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

    [SerializeField] private Button nextButton;

    private DialogRunner runner;
    private Hero currentHero;
    private DialogData currentDialogData;

    [SerializeField] private Button buttonA;
    [SerializeField] private Button buttonB;
    [SerializeField] private GameObject panelOfQuestions;

    private string playerId;
    private IHeroController _heroController;
    private IProfilePictureFactory _pictureFactory;


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

    public void StartDialog(Hero hero, DialogData dialogData, string player, IProfilePictureFactory pictureFactory,
        IHeroController heroController)
    {
        currentHero = hero;
        currentDialogData = dialogData;
        playerId = player;
        _pictureFactory = pictureFactory;
        _heroController = heroController;

        runner = new DialogRunner();
        runner.Initialize(dialogData.metadata);

        ShowDialog();
        RenderCurrentNode();
    }

    private void ShowDialog()
    {
        panelDialog.SetActive(true);
        canvasGroupDialog.alpha = 1;
        canvasGroupDialog.interactable = true;
        canvasGroupDialog.blocksRaycasts = true;

        nextButton.onClick.RemoveAllListeners();
        nextButton.onClick.AddListener(OnNextPressed);
    }

    private void RenderCurrentNode()
    {
        var node = runner.GetCurrentNode();

        if (node == null)
            return;

        textDialog.text = node.text;

        imageDialog.sprite = _pictureFactory.GetProfilePicture(node.emotion);

        if (node.isQuestion)
        {
            ShowAnswers(node);
        }
        else
        {
            HideAnswers();
        }
    }

    private void ShowAnswers(DialogNode node)
    {
        var answers = node.possibleAnswers;

        if (answers.Count > 0)
        {
            buttonA.gameObject.SetActive(true);
            buttonA.GetComponentInChildren<TextMeshProUGUI>().text = answers[0].optionText;
            buttonA.onClick.RemoveAllListeners();
            buttonA.onClick.AddListener(() => OnAnswerSelected(answers[0].optionKey));
        }

        if (answers.Count > 1)
        {
            buttonB.gameObject.SetActive(true);
            buttonB.GetComponentInChildren<TextMeshProUGUI>().text = answers[1].optionText;
            buttonB.onClick.RemoveAllListeners();
            buttonB.onClick.AddListener(() => OnAnswerSelected(answers[1].optionKey));
        }

        panelOfQuestions.SetActive(true);
    }

    private void HideAnswers()
    {
        buttonA.gameObject.SetActive(false);
        buttonB.gameObject.SetActive(false);
        panelOfQuestions.SetActive(false);
    }

    private void OnNextPressed()
    {
        runner.GoNext();

        var node = runner.GetCurrentNode();

        if (node == null)
            return;

        // Opcional: sync con backend aquí también si quieres tracking completo

        if (runner.IsCompleted())
        {
            EndDialog();
            return;
        }

        RenderCurrentNode();
    }


    private void OnAnswerSelected(string optionKey)
    {
        var node = runner.GetCurrentNode();

        if (node == null)
            return;

        var answer = node.possibleAnswers
            .FirstOrDefault(a => a.optionKey == optionKey);

        if (answer == null)
            return;

        var questionId = ResolveQuestionId(node.sequence);

        // 🔁 Flujo local inmediato
        runner.Answer(optionKey);
        RenderCurrentNode();

        // 🚀 Enviar al backend en background
        if (!string.IsNullOrWhiteSpace(questionId))
        {
            SendAnswerInBackground(questionId, optionKey).Forget();
        }
        else
        {
            Debug.LogWarning($"[Dialog] No se encontró questionId para el nodo '{node.sequence}'. No se enviará la respuesta al backend.");
        }
    }

    private string ResolveQuestionId(string nodeSequence)
    {
        if (currentDialogData?.questions == null || string.IsNullOrWhiteSpace(nodeSequence))
            return null;

        return currentDialogData.questions
            .FirstOrDefault(q => q.node_sequence == nodeSequence)
            ?.questionId;
    }

    private async UniTaskVoid SendAnswerInBackground(string questionId, string optionKey)
    {
        try
        {
            var result = await ServiceLocator.Instance.GetService<IWebAdapter>().AnswerDialog(
                playerId,
                currentHero.HeroId,
                questionId,
                optionKey
            );

            if (result == null)
                return;

            // 🔥 Opcional: solo efectos secundarios
            if (result.assignedPassive != null)
            {
                Debug.Log($"[Dialog] Passive received: {result.assignedPassive.name}");
                // 👉 integrar con sistema de héroe
            }

            if (result.pointsAwarded > 0)
            {
                Debug.Log($"[Dialog] Points: {result.pointsAwarded}");
            }

            if (result.completed)
            {
                Debug.Log("[Dialog] Completed (backend)");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Dialog] Background answer failed: {ex.Message}");
        }
    }

    private async void EndDialog()
    {
        Debug.Log("Dialog completed");
        
        await _heroController.LoadHeroes();

        panelDialog.SetActive(false);
    }

    private void HandleCompletion(AnswerDialogResult result)
    {
        Debug.Log($"Dialog completed. Points: {result.pointsAwarded}");

        if (result.assignedPassive != null)
        {
            Debug.Log($"New Passive: {result.assignedPassive.name}");
            // Aquí puedes integrarlo con tu sistema de héroes
        }

        EndDialog();
    }

    private void SetInteraction(bool enabled)
    {
        canvasGroupDialog.interactable = enabled;
        canvasGroupDialog.blocksRaycasts = enabled;
    }
}

[Serializable]
public class SpriteImages
{
    public string image;
    public Sprite sprite;
}