using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Presentation.UI
{
    /// <summary>
    /// Presents the shared power-up selector and waits for an explicit decision.
    /// The panel owns the presentation only: the selection itself lives in the power-up use case,
    /// so whatever the player leaves selected is what the match will use for both boards.
    /// Two ways out, both idempotent: "Listo" resolves <c>true</c>, "Volver" / cancellation / destroy
    /// resolve <c>false</c>. Nothing is written anywhere; the caller decides what to do with the answer.
    /// The buttons are inactive by default, so a host that only calls
    /// <see cref="ShowPowerUpsInLobby.Configure"/> and <see cref="ShowPowerUpsInLobby.ShowPowerUps"/>
    /// (the Lobby timer path) never sees them.
    /// </summary>
    public sealed class PowerUpsSelectionPanelView : MonoBehaviour
    {
        [Tooltip("The selector that builds one tile per power-up. Also fades the panel in and out.")]
        [SerializeField] private ShowPowerUpsInLobby showPowerUps;

        [Tooltip("Panel-wide fade handle. Fades the whole panel, dim included, during the close.")]
        [SerializeField] private CanvasGroup panelCanvasGroup;

        [Tooltip("Right action. Resolves the panel with true. Inactive by default.")]
        [SerializeField] private Button confirmButton;

        [Tooltip("Left action. Resolves the panel with false. Inactive by default.")]
        [SerializeField] private Button cancelButton;

        [Tooltip("How long the close fade lasts before the panel deactivates, in seconds.")]
        [SerializeField] private float fadeSeconds = 0.5f;

        // Non-null while a run is in flight. It is the whole single-flight mechanism: a second caller
        // receives this same task instead of stacking another run.
        private UniTaskCompletionSource<bool> _pending;

        // Panel-wide fade. The tiles fade through ShowPowerUpsInLobby, but the dim sits on a sibling
        // object, so the root group fades too during the same close window.
        private Tween _fadeTween;

        /// <summary>
        /// Opens the panel, waits for "Listo" or "Volver", then fades out and deactivates.
        /// </summary>
        /// <remarks>
        /// Single-flight: if a run is already in flight the call returns that run's task. A second call
        /// neither reconfigures the selector (which would duplicate tiles) nor adds a second set of
        /// listeners, so both callers observe the same answer and the panel closes exactly once.
        /// The returned task completes after the close fade, so awaiting it means "the panel is gone".
        /// </remarks>
        /// <param name="cancellationToken">Cancels the wait. Cancellation resolves <c>false</c>, like "Volver".</param>
        /// <returns><c>true</c> when the player pressed "Listo"; <c>false</c> on "Volver", cancellation or destroy.</returns>
        public UniTask<bool> ShowAndWaitAsync(CancellationToken cancellationToken = default)
        {
            if (_pending != null)
            {
                return _pending.Task;
            }

            // Keep a local handle: the run may finish and clear _pending before this method returns.
            var pending = new UniTaskCompletionSource<bool>();
            _pending = pending;
            RunAsync(pending, cancellationToken).Forget();
            return pending.Task;
        }

        /// <summary>
        /// Enforces the close order: configure, show, wait, hide, fade, deactivate, then answer.
        /// Runs as fire-and-forget so a synchronous "Listo" in the same frame cannot resolve the task
        /// before the caller has finished assigning it.
        /// </summary>
        private async UniTaskVoid RunAsync(UniTaskCompletionSource<bool> pending, CancellationToken cancellationToken)
        {
            var confirmed = false;

            // Declared before the try so the finally can still read them when the setup itself throws.
            // Both are created inside the try: a failure there must resolve the caller instead of
            // escaping this unobserved UniTaskVoid.
            var destroyToken = CancellationToken.None;
            CancellationTokenSource linked = null;

            try
            {
                destroyToken = this.GetCancellationTokenOnDestroy();
                linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyToken);
                var token = linked.Token;

                gameObject.SetActive(true);
                ResetFade();
                if (showPowerUps == null)
                {
                    Debug.LogWarning($"[PowerUpsSelectionPanelView] no ShowPowerUpsInLobby on '{name}'.", this);
                }
                else
                {
                    await showPowerUps.Configure();
                }

                // Configure awaits the use case, so the panel may have been cancelled or destroyed
                // while it was loading. Stop before showing anything in that case.
                token.ThrowIfCancellationRequested();

                if (showPowerUps != null) showPowerUps.ShowPowerUps();
                OpenButtons();
                confirmed = await WaitForChoiceAsync(token);
                CloseButtons();

                if (showPowerUps != null) showPowerUps.HidePowerUps();
                FadeOutPanel();
                // Same clock as the DOTween fades: scaled time, so a pause holds the panel too.
                await UniTask.Delay(TimeSpan.FromSeconds(fadeSeconds), ignoreTimeScale: false, cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is a normal answer, not an error. Before a decision it stays false,
                // which is the same outcome as "Volver"; after "Listo" the answer already stands.
            }
            catch (Exception exception)
            {
                // The task must always resolve, otherwise the caller waits forever. Log and treat it
                // as a refusal instead of letting the exception escape an unobserved UniTaskVoid.
                Debug.LogError($"[PowerUpsSelectionPanelView] the panel failed to run: {exception}", this);
                confirmed = false;
            }
            finally
            {
                // Teardown is best effort: it must never be the reason the answer goes missing.
                try
                {
                    ResetFade();

                    // A destroyed panel cannot be deactivated and its gameObject is already gone.
                    if (this != null && !destroyToken.IsCancellationRequested)
                    {
                        gameObject.SetActive(false);
                    }
                }
                catch (Exception teardownException)
                {
                    Debug.LogError($"[PowerUpsSelectionPanelView] the panel failed to close: {teardownException}", this);
                }

                // The one and only resolution point, reached from every path above. UniTask's
                // TrySetResult is idempotent, so even an impossible second call would be a no-op
                // that returns false instead of throwing; the caller is always answered.
                pending.TrySetResult(confirmed);

                // Cleared only after the task has an answer: a caller that arrives later starts a
                // fresh run instead of receiving a task that already finished.
                if (ReferenceEquals(_pending, pending))
                {
                    _pending = null;
                }

                // Released last: the answer is already out, so nothing here can strand the caller.
                linked?.Dispose();
            }
        }

        /// <summary>
        /// Restores the panel to full opacity and drops any fade still in flight, so a re-entry never
        /// starts from a half-faded panel or inherits the tail of the previous close.
        /// </summary>
        private void ResetFade()
        {
            if (_fadeTween != null)
            {
                _fadeTween.Kill();
                _fadeTween = null;
            }

            if (panelCanvasGroup != null)
            {
                // The panel is only visible once ShowPowerUpsInLobby.ShowPowerUps runs. Starting at
                // alpha 1 made it flash empty before Configure had a chance to build the tiles.
                panelCanvasGroup.alpha = 0f;
                panelCanvasGroup.blocksRaycasts = false;
            }
        }

        private void FadeOutPanel()
        {
            if (panelCanvasGroup == null) return;
            _fadeTween = panelCanvasGroup.DOFade(0f, fadeSeconds);
        }

        /// <summary>
        /// Waits for either button, or for the token. Listeners are removed on every path, including
        /// cancellation, so a re-entry never runs a stale handler and a destroyed panel keeps no
        /// reference to itself alive.
        /// </summary>
        private async UniTask<bool> WaitForChoiceAsync(CancellationToken token)
        {
            if (confirmButton == null || cancelButton == null)
            {
                Debug.LogWarning("[PowerUpsSelectionPanelView] confirm/cancel button missing: the panel cannot be resolved.", this);
                return false;
            }

            var choice = new UniTaskCompletionSource<bool>();
            UnityAction onConfirm = () => choice.TrySetResult(true);
            UnityAction onCancel = () => choice.TrySetResult(false);

            confirmButton.onClick.AddListener(onConfirm);
            cancelButton.onClick.AddListener(onCancel);

            try
            {
                // TrySetResult is idempotent, so the first of {button, cancel, destroy} wins.
                using (token.Register(() => choice.TrySetResult(false)))
                {
                    return await choice.Task;
                }
            }
            finally
            {
                confirmButton.onClick.RemoveListener(onConfirm);
                cancelButton.onClick.RemoveListener(onCancel);
            }
        }

        private void OpenButtons()
        {
            if (confirmButton != null) confirmButton.gameObject.SetActive(true);
            if (cancelButton != null) cancelButton.gameObject.SetActive(true);
        }

        private void CloseButtons()
        {
            // The close fade still has the panel on screen; without this, a second click lands on a
            // button that is about to disappear.
            if (panelCanvasGroup != null) panelCanvasGroup.blocksRaycasts = false;
            if (confirmButton != null) confirmButton.gameObject.SetActive(false);
            if (cancelButton != null) cancelButton.gameObject.SetActive(false);
        }
    }
}
