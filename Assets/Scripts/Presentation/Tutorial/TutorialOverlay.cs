using System;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation.Tutorial
{
    /// <summary>
    /// The tutorial's on-screen layer. Copy lives in serialized <see cref="TMPro.TMP_Text"/> fields
    /// (never in code) and every non-modal step leaves the board clickable: only <see cref="dim"/>
    /// carries a raycast target, and it is enabled just for the closing card and the retry prompt.
    /// </summary>
    public sealed class TutorialOverlay : MonoBehaviour
    {
        [Header("Layers")]
        [Tooltip("Full-screen block. raycastTarget must stay true; it is only active while modal.")]
        [SerializeField] private Image dim;
        [SerializeField] private GameObject card;
        [SerializeField] private GameObject retryPanel;

        [Header("Step copy (exclusive groups inside Card)")]
        [SerializeField] private GameObject dropGroup;
        [SerializeField] private GameObject matchGroup;
        [SerializeField] private GameObject balanceGroup;
        [SerializeField] private GameObject closeGroup;

        [Header("Hints")]
        [SerializeField] private GameObject hintLeft;
        [SerializeField] private GameObject hintRight;

        [Header("Buttons")]
        [SerializeField] private Button skipButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button retryButton;

        public void Initialize(Action onSkip, Action onContinue, Action onRetry)
        {
            skipButton.onClick.RemoveAllListeners();
            continueButton.onClick.RemoveAllListeners();
            retryButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(() => onSkip());
            continueButton.onClick.AddListener(() => onContinue());
            retryButton.onClick.AddListener(() => onRetry());
        }

        /// <summary>Observer mode: the card explains, the board stays tappable.</summary>
        public void ShowStep(TutorialStep step, PathType expectedSide)
        {
            retryPanel.SetActive(false);
            card.SetActive(true);
            dim.gameObject.SetActive(step == TutorialStep.Close);

            dropGroup.SetActive(step == TutorialStep.Drop);
            matchGroup.SetActive(step == TutorialStep.Match);
            balanceGroup.SetActive(step == TutorialStep.Balance);
            closeGroup.SetActive(step == TutorialStep.Close);

            hintLeft.SetActive(expectedSide == PathType.Left);
            hintRight.SetActive(expectedSide == PathType.Right);
        }

        /// <summary>Modal prompt: the board is blocked until the player retries.</summary>
        public void ShowRetry()
        {
            card.SetActive(false);
            retryPanel.SetActive(true);
            dim.gameObject.SetActive(true);
            hintLeft.SetActive(false);
            hintRight.SetActive(false);
        }

        public void Hide()
        {
            card.SetActive(false);
            retryPanel.SetActive(false);
            dim.gameObject.SetActive(false);
            hintLeft.SetActive(false);
            hintRight.SetActive(false);
        }
    }
}
