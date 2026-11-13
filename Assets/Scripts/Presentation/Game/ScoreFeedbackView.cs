using System.Collections.Generic;
using DG.Tweening;
using Domain.Services;
using TMPro;
using UnityEngine;

namespace Presentation.Game
{
    /// <summary>
    /// One per human board. Subscribes to <see cref="Balance.PointsAwarded"/> and shows two
    /// pieces of feedback: a persistent badge with the multiplier the last award actually
    /// applied, and a pooled floating "+N" popup anchored to the board.
    /// The badge only ever shows the applied multiplier; it never predicts the decayed value
    /// the next award would get, because that value does not exist until the award happens.
    /// The popup text is the exact integer that was added to the player's total.
    /// Visual only: no audio, no haptics, nothing that changes scoring, placement or save data.
    /// </summary>
    public sealed class ScoreFeedbackView : MonoBehaviour
    {
        // Vertical stagger between awards that share the same instant, in canvas units. The
        // horizontal lane is bounded by the per-board clamp, so the stagger cannot live in X.
        private const float PopupCascadeStep = 24f;

        // Gap kept between the popup box and the canvas centre line / canvas edge, in canvas units.
        private const float PopupEdgePadding = 8f;

        [SerializeField] private Balance balance;

        [Tooltip("TMP child of the score block that shows the multiplier applied by the last award. " +
                 "Inactive by default: it appears on the second award and hides after the decay window.")]
        [SerializeField] private TMP_Text multiplierBadge;

        [Tooltip("Canvas rect the floating popups are parented to. Must be the in-game UI canvas, not the board.")]
        [SerializeField] private RectTransform popupLayer;

        [Tooltip("World point the popup is anchored to, usually the board root.")]
        [SerializeField] private Transform popupWorldAnchor;

        [Tooltip("Inactive TMP used as the popup blueprint. Cloned once per pooled popup at wake.")]
        [SerializeField] private TMP_Text popupTemplate;

        [Tooltip("Camera that projects the anchor into canvas space. The in-game camera.")]
        [SerializeField] private Camera projectionCamera;

        [Tooltip("Rotate the popup text 180 degrees. Only for the P2 board, whose HUD block is flipped.")]
        [SerializeField] private bool flipText180;

        [Header("Popup")]
        [Tooltip("Pre-warmed popups. A resolution can award several times, so the pool is a budget; " +
                 "when it runs dry the oldest popup is recycled instead of dropping the new award.")]
        [SerializeField] private int poolSize = 6;
        [SerializeField] private float popupRise = 90f;
        [SerializeField] private float popupSeconds = 0.9f;
        [SerializeField] private float popupFadeSeconds = 0.6f;

        private sealed class Popup
        {
            public TMP_Text Text;
            public RectTransform Rect;
            public Sequence Tween;
        }

        private readonly Stack<Popup> _pool = new();
        private readonly List<Popup> _active = new();
        private GameManager _gameManager;
        private Camera _uiCamera;
        private float _badgeHideAt = float.PositiveInfinity;
        private int _cascadeIndex;
        private bool _poolReady;

        private void OnEnable()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            if (balance == null)
            {
                Debug.LogWarning($"[ScoreFeedbackView] no balance reference on '{name}'.", this);
                enabled = false;
                return;
            }

            balance.PointsAwarded += OnPointsAwarded;
            BuildPool();
            HideBadge();
            _cascadeIndex = 0;
        }

        private void OnDisable()
        {
            if (balance != null) balance.PointsAwarded -= OnPointsAwarded;
            KillAllPopups();
            HideBadge();
        }

        private void OnDestroy()
        {
            if (balance != null) balance.PointsAwarded -= OnPointsAwarded;
            KillAllPopups();
        }

        private void LateUpdate()
        {
            // The badge is a memo of the last award, not a live multiplier: it hides on its own
            // timer and is refreshed by the next award. It never reads the next-multiplier value.
            if (multiplierBadge == null || !multiplierBadge.gameObject.activeSelf) return;
            if (Time.unscaledTime < _badgeHideAt) return;
            HideBadge();
        }

        /// <summary>
        /// Fired by <see cref="Balance"/> in the same frame and right after the points were added.
        /// </summary>
        private void OnPointsAwarded(int points, int multiplier, int total)
        {
            // A no-op award never reaches this handler, and the AI board keeps its own score.
            if (points <= 0 || !IsHumanBoard()) return;

            ShowBadge(multiplier);
            ShowPopup(points, multiplier);
        }

        private bool IsHumanBoard()
        {
            if (_gameManager == null) return false;
            var player = balance.GetPlayer();
            if (player == null) return false;

            // Identity, not type: a human P2 is not a MachinePlayer, so test who owns this
            // board against the first player, and let every local-PvP board through.
            return ReferenceEquals(player, _gameManager.GetFirstPlayer()) || _gameManager.IsLocalPvP();
        }

        private void ShowBadge(int multiplier)
        {
            if (multiplierBadge == null) return;

            if (multiplier <= 1)
            {
                HideBadge();
                return;
            }

            multiplierBadge.SetText("x{0}", multiplier);
            multiplierBadge.gameObject.SetActive(true);
            _badgeHideAt = Time.unscaledTime + ScorePolicy.DecayWindowSeconds;
        }

        private void HideBadge()
        {
            _badgeHideAt = float.PositiveInfinity;
            if (multiplierBadge != null) multiplierBadge.gameObject.SetActive(false);
        }

        private void BuildPool()
        {
            if (_poolReady) return;

            if (popupTemplate == null || popupLayer == null)
            {
                Debug.LogWarning(
                    "[ScoreFeedbackView] popup template or popup layer is not wired; popups stay off.",
                    this);
                return;
            }

            _uiCamera = ResolveUiCamera();
            popupTemplate.gameObject.SetActive(false);
            for (var i = 0; i < Mathf.Max(0, poolSize); i++)
            {
                _pool.Push(CreatePopup());
            }

            _poolReady = true;
        }

        /// <summary>
        /// Screen-space-overlay canvases take no camera when converting points; every other mode
        /// takes the same camera the canvas was rendered with.
        /// </summary>
        private Camera ResolveUiCamera()
        {
            var canvas = popupLayer != null ? popupLayer.GetComponentInParent<Canvas>() : null;
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return projectionCamera;
        }

        private Popup CreatePopup()
        {
            var text = Instantiate(popupTemplate, popupLayer);
            text.gameObject.name = "MultiplierPopup (pooled)";
            text.gameObject.SetActive(false);
            return new Popup { Text = text, Rect = (RectTransform)text.transform };
        }

        private Popup Rent()
        {
            Popup popup;

            if (_pool.Count > 0)
            {
                popup = _pool.Pop();
            }
            else if (_active.Count > 0)
            {
                popup = _active[0];
                _active.RemoveAt(0);
            }
            else
            {
                popup = CreatePopup();
            }

            // Kill without completing: the recycled popup must not report its own completion and
            // hand itself back to the pool twice.
            popup.Tween?.Kill();
            popup.Tween = null;
            popup.Text.alpha = 1f;
            popup.Text.gameObject.SetActive(true);
            _active.Add(popup);
            return popup;
        }

        private void ShowPopup(int points, int multiplier)
        {
            if (!_poolReady || popupWorldAnchor == null) return;

            var popup = Rent();
            var rect = popup.Rect;

            // The popup is parented to the canvas, so it is never mirrored by the board flip;
            // only the glyphs are turned for the P2 owner.
            rect.localRotation = flipText180 ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;

            if (!TryWorldToCanvas(popupWorldAnchor.position, out var anchored))
            {
                Recycle(popup);
                return;
            }

            // Both board roots project close to the canvas centre (P1's projects exactly onto it)
            // while a popup box is 44 % of the canvas width, so two simultaneous awards would sit on
            // top of each other. Each popup stays inside its own board's half instead.
            var x = ClampToBoardHalf(anchored.x, rect.rect.width);

            // Cascade several awards from one resolution so stacked popups stay readable. The offset
            // is vertical: the horizontal lane is already bounded by the clamp above.
            var stagger = (_cascadeIndex++ % 3) * PopupCascadeStep;
            var start = new Vector3(x, anchored.y + stagger, rect.localPosition.z);
            rect.localPosition = start;

            if (multiplier > 1) popup.Text.SetText("+{0} x{1}", points, multiplier);
            else popup.Text.SetText("+{0}", points);

            popup.Tween = DOTween.Sequence()
                .Join(rect.DOLocalMove(start + new Vector3(0f, popupRise, 0f), popupSeconds)
                    .SetEase(Ease.OutCubic))
                .Join(popup.Text.DOFade(0f, popupFadeSeconds).SetEase(Ease.InQuad))
                .OnComplete(() => Recycle(popup));
        }

        /// <summary>
        /// Projects a world point into <see cref="popupLayer"/>'s local space. The anchor does not
        /// move while a popup lives, so this runs once per award and never in the tween.
        /// </summary>
        private bool TryWorldToCanvas(Vector3 worldPosition, out Vector2 localPoint)
        {
            localPoint = Vector2.zero;
            if (projectionCamera == null) return false;

            var screen = projectionCamera.WorldToScreenPoint(worldPosition);
            if (screen.z < 0f) return false;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                popupLayer,
                screen,
                _uiCamera,
                out localPoint);
        }

        /// <summary>
        /// Bounds the projected X to the half of <see cref="popupLayer"/> that belongs to this board,
        /// so a popup from each board can never share the same pixels. The clearance is half the popup
        /// box plus <see cref="PopupEdgePadding"/>: the box is the space the view reserves for one
        /// award, which makes the rule independent of how long the "+N xM" string turns out to be.
        /// </summary>
        private float ClampToBoardHalf(float projectedX, float popupWidth)
        {
            var layer = popupLayer.rect;
            var clearance = popupWidth * 0.5f + PopupEdgePadding;
            var center = layer.center.x;
            var left = IsLeftBoard();

            var outerEdge = left ? layer.xMin + clearance : layer.xMax - clearance;
            var innerEdge = left ? center - clearance : center + clearance;

            return Mathf.Clamp(projectedX, Mathf.Min(outerEdge, innerEdge), Mathf.Max(outerEdge, innerEdge));
        }

        /// <summary>
        /// Which half of the canvas this board owns. The first player is authored on the left column
        /// and the second seat is mirrored to the right, so identity is the side rule; the projected
        /// point cannot be used because P1's board root projects exactly onto the canvas centre.
        /// </summary>
        private bool IsLeftBoard()
        {
            if (_gameManager == null || balance == null) return true;
            return ReferenceEquals(balance.GetPlayer(), _gameManager.GetFirstPlayer());
        }

        private void Recycle(Popup popup)
        {
            if (popup == null) return;

            popup.Tween = null;
            _active.Remove(popup);

            // The cascade window ends with the last popup in flight: the next award must start at
            // offset zero instead of continuing the previous ramp.
            if (_active.Count == 0) _cascadeIndex = 0;

            if (popup.Text == null) return;

            popup.Text.gameObject.SetActive(false);
            _pool.Push(popup);
        }

        private void KillAllPopups()
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var popup = _active[i];
                if (popup == null) continue;
                popup.Tween?.Kill();
                Recycle(popup);
            }

            _active.Clear();
        }
    }
}
