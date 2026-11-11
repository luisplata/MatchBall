using System;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation.Tutorial
{
    /// <summary>
    /// The tutorial's on-screen layer. Copy lives in serialized <see cref="TMPro.TMP_Text"/> fields
    /// (never in code) and every non-modal step leaves the board clickable: only <see cref="dim"/>
    /// carries a raycast target, and it is enabled just for the closing card and the retry prompt.
    /// The bubble is a world-anchored panel: it is re-placed every late update from the opponent
    /// board, which keeps it off the playable balls at any aspect instead of parking on top of them.
    /// </summary>
    public sealed class TutorialOverlay : MonoBehaviour
    {
        /// <summary>Point in the opponent board's space the bubble talks about: between its columns.</summary>
        private static readonly Vector3 AnchorLocalPoint = new Vector3(-3f, -2.70f, 0f);

        /// <summary>Constant world fallback, so an unwired anchor still lands where the design expects.</summary>
        private static readonly Vector3 AnchorFallbackWorld = new Vector3(2.35f, -2.70f, 0f);

        /// <summary>World x of the human board's right edge, used when no lane collider is wired.</summary>
        private const float BoardRightFallbackWorldX = -0.0717f;

        /// <summary>Design position from the prefab. Never (0,0): that would centre the bubble on the board.</summary>
        private static readonly Vector2 DesignPosition = new Vector2(211f, -216f);

        /// <summary>The canvas is a 900 px wide match-width scaler, so its half width is constant.</summary>
        private const float CanvasHalfWidth = 450f;

        /// <summary>Inset the bubble keeps from the canvas edge and from the board.</summary>
        private const float Margin = 24f;
        private const float BoardGap = 16f;

        /// <summary>Body text floor: the prefab already used 18 as its minimum, 24 is the readable one.</summary>
        private const float MinimalFontSize = 24f;
        private const float FullFontSize = 32f;
        private const float CompactFontSize = 24f;

        /// <summary>Below this canvas height the two-set layout takes over (see the design's 568 px).</summary>
        private const float CompactCanvasHeight = 568f;

        /// <summary>Skip's size and inset. Both sets are applied by <see cref="ApplyLayout"/>.</summary>
        private const float CompactSkipWidth = 80f;
        private const float CompactSkipHeight = 36f;
        private const float CompactSkipInset = 12f;
        private static readonly Vector2 CompactSkipSize = new Vector2(CompactSkipWidth, CompactSkipHeight);
        private static readonly Vector2 FullSkipSize = new Vector2(120f, 56f);

        /// <summary>Gap Skip keeps from the retry modal in compact, and the modal keeps from the top edge.</summary>
        private const float RetrySkipGap = 8f;
        private const float RetryTopMargin = 8f;

        /// <summary>
        /// Bottom band of a compact frame that belongs to Skip: inset + button + gap, plus the margin
        /// the modal needs above itself.
        /// </summary>
        private const float RetryBottomStrip =
            CompactSkipInset + CompactSkipHeight + RetrySkipGap + RetryTopMargin;

        [Header("Layers")]
        [Tooltip("Full-screen block. raycastTarget must stay true; it is only active while modal.")]
        [SerializeField] private Image dim;
        [Tooltip("The bubble. Layout node only: it is re-placed every late update.")]
        [SerializeField] private RectTransform panel;
        [Tooltip("Bubble tail. Parented to the bubble body and toggled off when it would point nowhere.")]
        [SerializeField] private RectTransform tail;
        [SerializeField] private GameObject retryPanel;

        [Header("Step copy (exclusive groups inside the bubble)")]
        [SerializeField] private GameObject dropGroup;
        [SerializeField] private GameObject matchGroup;
        [SerializeField] private GameObject balanceGroup;
        [SerializeField] private GameObject closeGroup;

        [Header("Progress")]
        [Tooltip("One dot per step. The count must match (int)TutorialStep.Done.")]
        [SerializeField] private TutorialStepDots dots;

        [Header("Lane callouts")]
        [Tooltip("Pointing hand for the left lane. Only one callout is visible at a time.")]
        [SerializeField] private TutorialWorldAnchor calloutLeft;
        [SerializeField] private TutorialWorldAnchor calloutRight;

        [Header("Danger")]
        [Tooltip("Overflow-risk hint. Copy lives in the prefab; shown only while a path is 1-2 moves from loss.")]
        [SerializeField] private GameObject dangerHint;

        [Header("Buttons")]
        [SerializeField] private Button skipButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button retryButton;

        private Camera _worldCamera;
        private Transform _opponentBoard;
        private Collider2D _rightLane;

        private RectTransform[] _copies;
        private RectTransform _skipRect;
        private RectTransform _retryRect;
        private Vector2 _retryDesignSize;
        private Vector2 _retryDesignPosition;
        private bool _compact;
        private bool _layoutApplied;
        private bool _dangerRequested;
        private bool _warned;

        private void Awake()
        {
            _copies = new[]
            {
                CopyRect(dropGroup), CopyRect(matchGroup), CopyRect(balanceGroup), CopyRect(closeGroup)
            };

            _skipRect = skipButton != null ? skipButton.transform as RectTransform : null;

            // The retry modal's design rect is read once from the prefab instead of hardcoded: the
            // compact pass shrinks and lifts it, and every full frame has to restore it verbatim.
            _retryRect = retryPanel != null ? retryPanel.transform as RectTransform : null;
            if (_retryRect != null)
            {
                _retryDesignSize = _retryRect.sizeDelta;
                _retryDesignPosition = _retryRect.anchoredPosition;
            }

            // The curriculum's length is the enum's, not a literal: if the prefab drifts from it the
            // indicator silently under- or over-reports progress, which is worse than a warning.
            if (dots != null && dots.Count != (int)TutorialStep.Done)
            {
                Debug.LogWarning(
                    $"[Tutorial] the progress indicator has {dots.Count} dots but the curriculum has {(int)TutorialStep.Done} steps.");
            }
        }

        public void Initialize(Action onSkip, Action onContinue, Action onRetry)
        {
            skipButton.onClick.RemoveAllListeners();
            continueButton.onClick.RemoveAllListeners();
            retryButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(() => onSkip());
            continueButton.onClick.AddListener(() => onContinue());
            retryButton.onClick.AddListener(() => onRetry());
        }

        /// <summary>
        /// Wires the three references the scene cannot carry without a prefab override: the world
        /// camera, the board the bubble talks about, and the tap lane that bounds it. Called once by
        /// <see cref="TutorialController"/> after it resolves the match.
        /// </summary>
        public void Bind(Camera worldCamera, Transform opponentBoard, Collider2D leftLane, Collider2D rightLane)
        {
            _worldCamera = worldCamera;
            _opponentBoard = opponentBoard;
            _rightLane = rightLane;

            // Forwarded once: the callouts re-project on their own late update, never per frame from here.
            if (calloutLeft != null) calloutLeft.Bind(worldCamera, leftLane);
            if (calloutRight != null) calloutRight.Bind(worldCamera, rightLane);

            if (_opponentBoard == null && !_warned)
            {
                _warned = true;
                Debug.LogWarning("[Tutorial] no opponent board root; the bubble falls back to a fixed world anchor.");
            }
        }

        /// <summary>Observer mode: the bubble explains, the board stays tappable.</summary>
        public void ShowStep(TutorialStep step, PathType expectedSide)
        {
            retryPanel.SetActive(false);
            panel.gameObject.SetActive(true);
            skipButton.gameObject.SetActive(true);
            dim.gameObject.SetActive(step == TutorialStep.Close);

            dropGroup.SetActive(step == TutorialStep.Drop);
            matchGroup.SetActive(step == TutorialStep.Match);
            balanceGroup.SetActive(step == TutorialStep.Balance);
            closeGroup.SetActive(step == TutorialStep.Close);

            if (dots != null) dots.SetCurrent((int)step);

            ShowCallout(calloutLeft, expectedSide == PathType.Left);
            ShowCallout(calloutRight, expectedSide == PathType.Right);

            _dangerRequested = false;
            dangerHint.SetActive(false);
        }

        /// <summary>Modal prompt: the board is blocked until the player retries.</summary>
        public void ShowRetry()
        {
            panel.gameObject.SetActive(false);
            retryPanel.SetActive(true);
            dim.gameObject.SetActive(true);

            // Skip is a root sibling on purpose: the bubble goes away here but leaving the tutorial
            // has to stay one tap away, so the overlay owns that button instead of the bubble.
            skipButton.gameObject.SetActive(true);

            // Placed on the same frame it is shown: waiting for the late update would pop the modal
            // in at its design rect for one frame before settling.
            PlaceRetry();

            ShowCallout(calloutLeft, false);
            ShowCallout(calloutRight, false);
            _dangerRequested = false;
            dangerHint.SetActive(false);
        }

        /// <summary>
        /// Removes every overlay affordance, Skip included: the button is a sibling of the bubble, so
        /// hiding the panel no longer disarms it, and leaving it behind would leak a live clickable
        /// layer into every normal match that shares the scene with this prefab.
        /// </summary>
        public void Hide()
        {
            panel.gameObject.SetActive(false);
            retryPanel.SetActive(false);
            dim.gameObject.SetActive(false);
            skipButton.gameObject.SetActive(false);
            ShowCallout(calloutLeft, false);
            ShowCallout(calloutRight, false);
            _dangerRequested = false;
            dangerHint.SetActive(false);
            if (dots != null) dots.Clear();
        }

        /// <summary>
        /// Toggles the overflow-risk hint. Framed as an imminent retry, never a defeat:
        /// the copy lives in the prefab and the retry flow stays intact.
        /// </summary>
        public void ShowDanger(bool visible)
        {
            _dangerRequested = visible;
            if (dangerHint != null) dangerHint.SetActive(visible && !_compact);
        }

        /// <summary>
        /// Re-places the bubble. Runs every frame on purpose: the anchor is resolved from live world
        /// transforms, so moving the camera or resizing the game view is handled on the next frame
        /// without a single cached offset.
        /// </summary>
        private void LateUpdate()
        {
            // The modal owns the layer while it is up, and it is the only frame where Skip is not on
            // the bubble: one branch keeps both of them placed across a resize.
            if (retryPanel != null && retryPanel.activeSelf)
            {
                PlaceRetry();
                return;
            }

            PlacePanel();
        }

        private void PlacePanel()
        {
            // The retry prompt is laid out by PlaceRetry, which LateUpdate reaches first; this guard
            // only protects the call that arrives while the bubble is already down.
            if (panel == null || !panel.gameObject.activeSelf) return;

            // The overlay root is a stretch child of Canvas_UI (pivot 0.5,0.5), so its rect *is* the
            // 900 px wide canvas: local coordinates are the same ones the design tables use.
            var canvas = transform as RectTransform;
            if (canvas == null) return;

            var canvasHeight = canvas.rect.height;
            if (canvasHeight <= 0f) return;

            if (_worldCamera == null) _worldCamera = Camera.main;

            if (_worldCamera == null || !TryResolveAnchor(canvas, out var anchor, out var boardRightX))
            {
                panel.anchoredPosition = DesignPosition;
                PlaceSkip(DesignPosition, canvasHeight, _compact);
                return;
            }

            var band = (CanvasHalfWidth - Margin) - (boardRightX + BoardGap);
            var compact = canvasHeight < CompactCanvasHeight || band < 400f;

            if (!_layoutApplied || compact != _compact)
            {
                _compact = compact;
                _layoutApplied = true;
                ApplyLayout(compact);
            }

            var halfWidth = compact ? 160f : 200f;
            var halfHeight = compact ? 120f : 260f;

            // Safety net only: the compact layout already fits a 288 px tall canvas. Below that the
            // bubble scales instead of spilling out of the frame.
            var scale = compact && canvasHeight < 288f
                ? Mathf.Clamp((canvasHeight - 24f) / 240f, 0.5f, 1f)
                : 1f;
            panel.localScale = new Vector3(scale, scale, 1f);

            var minX = boardRightX + BoardGap + halfWidth * scale;
            var maxX = CanvasHalfWidth - Margin - halfWidth * scale;
            var x = Mathf.Clamp(anchor.x, minX, Mathf.Max(minX, maxX));
            var y = Mathf.Clamp(
                anchor.y,
                -canvasHeight * 0.5f + Margin + halfHeight * scale,
                canvasHeight * 0.5f - Margin - halfHeight * scale);

            panel.anchoredPosition = new Vector2(x, y);
            PlaceSkip(new Vector2(x, y), canvasHeight, compact);

            if (tail == null) return;

            // The bubble always lives right of the board, so its tail always points left. When the
            // clamp pushes the bubble over the board's edge there is nothing to point at: hide the
            // tail rather than mirror it (the manifest ships no right-facing tail).
            tail.anchoredPosition = new Vector2(0f, Mathf.Clamp(anchor.y - y, -halfHeight + 40f, halfHeight - 40f));
            tail.gameObject.SetActive((x - halfWidth) >= boardRightX);
        }

        /// <summary>
        /// Resolves the world anchor and the board's right edge in canvas space. Nothing is cached:
        /// both come from live transforms and colliders, and the fallbacks exist so an unwired
        /// tutorial still draws somewhere sensible instead of throwing.
        /// </summary>
        private bool TryResolveAnchor(RectTransform canvas, out Vector2 anchor, out float boardRightX)
        {
            boardRightX = 0f;

            var boardPoint = _opponentBoard != null
                ? _opponentBoard.TransformPoint(AnchorLocalPoint)
                : AnchorFallbackWorld;

            if (!TutorialProjection.TryWorldToCanvas(canvas, _worldCamera, boardPoint, out anchor))
            {
                if (!TutorialProjection.TryWorldToCanvas(canvas, _worldCamera, AnchorFallbackWorld, out anchor))
                    return false;

                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[Tutorial] the board anchor projects off-canvas; using the fixed world anchor.");
                }
            }

            var boardRightWorldX = _rightLane != null ? _rightLane.bounds.max.x : BoardRightFallbackWorldX;
            if (!TutorialProjection.TryWorldToCanvas(canvas, _worldCamera,
                    new Vector3(boardRightWorldX, 0f, 0f), out var boardEdge))
                return false;

            boardRightX = boardEdge.x;
            return true;
        }

        /// <summary>
        /// Applies one of the two number sets from the design. The node tree is the same in both
        /// modes: only rect values, sizes and the danger strip's visibility change.
        /// </summary>
        private void ApplyLayout(bool compact)
        {
            panel.sizeDelta = compact ? new Vector2(320f, 240f) : new Vector2(400f, 520f);

            // Skip is a root sibling rather than a bubble child, so only its size is a bubble layout
            // value: PlacePanel pins it to the bubble's bottom-left corner every late update.
            if (_skipRect != null)
            {
                _skipRect.sizeDelta = compact ? CompactSkipSize : FullSkipSize;
            }

            ApplyStrip(continueButton != null ? continueButton.transform as RectTransform : null,
                compact ? new Vector2(200f, 52f) : new Vector2(280f, 84f),
                compact ? new Vector2(0f, 56f) : new Vector2(0f, 84f));

            // Copy strip: 228..444 in full, 116..200 (one line) in compact. Autosizing is what keeps
            // the promise that the body never drops below 24 px: the rich text title rides at 125 %
            // of whatever size fits, so it stays bigger than the body in both modes.
            var copyMin = compact ? new Vector2(16f, 116f) : new Vector2(28f, 228f);
            var copyMax = compact ? new Vector2(-16f, -40f) : new Vector2(-28f, -76f);
            var copyMaxSize = compact ? CompactFontSize : FullFontSize;

            if (_copies != null)
            {
                foreach (var rect in _copies)
                {
                    if (rect == null) continue;
                    rect.offsetMin = copyMin;
                    rect.offsetMax = copyMax;

                    var text = rect.GetComponent<TMPro.TMP_Text>();
                    if (text == null) continue;

                    text.enableAutoSizing = true;
                    text.fontSizeMin = MinimalFontSize;
                    text.fontSizeMax = copyMaxSize;
                }
            }

            // The strip has no room in the compact bubble: it is suppressed there and comes back
            // with its last requested state when the frame grows again.
            if (dangerHint != null) dangerHint.SetActive(_dangerRequested && !compact);

            if (tail != null)
            {
                tail.sizeDelta = compact ? new Vector2(40f, 40f) : new Vector2(56f, 56f);
            }

            if (dots != null) dots.ApplyLayout(compact);
        }

        /// <summary>
        /// Pins Skip to the bubble's bottom-left corner. Skip is a sibling of the bubble because the
        /// retry prompt hides the bubble and leaving the tutorial has to stay one tap away; the design
        /// still draws it inside the bubble's frame, so it follows the panel instead of living in it.
        /// </summary>
        private void PlaceSkip(Vector2 panelPosition, float canvasHeight, bool compact)
        {
            if (_skipRect == null) return;

            var halfWidth = compact ? 160f : 200f;
            var halfHeight = compact ? 120f : 260f;
            var scale = panel.localScale.x;
            var inset = compact ? CompactSkipInset : Margin;

            // The bubble is centred on the canvas and Skip is anchored to its bottom-left corner, so
            // the bubble corner has to be re-expressed in that origin.
            _skipRect.anchoredPosition = new Vector2(
                panelPosition.x - halfWidth * scale + inset + CanvasHalfWidth,
                panelPosition.y - halfHeight * scale + inset + canvasHeight * 0.5f);
        }

        /// <summary>
        /// Lays out the retry prompt and Skip together. The modal is 780 x 420, which is taller than a
        /// compact frame (506 px at 16:9, 405 px at 20:9): pinned to the canvas centre it leaves no free
        /// band, and Skip — the last sibling, so the top-most graphic — would sit on top of "Reintentar"
        /// and steal the tap. On compact frames the modal is shrunk to the height that fits and lifted
        /// above a bottom strip that belongs to Skip alone, so their rects cannot intersect. Full frames
        /// keep the prefab rect untouched: there Skip is on the bubble's corner, 180+ px below the modal.
        /// </summary>
        private void PlaceRetry()
        {
            var canvas = transform as RectTransform;
            if (canvas == null || _retryRect == null) return;

            var canvasHeight = canvas.rect.height;
            if (canvasHeight <= 0f) return;

            // The frame height decides on its own: the bubble's band test needs the camera and the
            // opponent board, and neither is being measured while the modal has the screen.
            if (canvasHeight >= CompactCanvasHeight && !_compact)
            {
                _retryRect.sizeDelta = _retryDesignSize;
                _retryRect.anchoredPosition = _retryDesignPosition;
                return;
            }

            // Squeeze only as far as the frame demands: 16:9 still fits the 420 px design height once
            // the modal is lifted clear, and only 20:9 has to give some of it up.
            var height = Mathf.Clamp(canvasHeight - RetryBottomStrip, 120f, _retryDesignSize.y);
            var bottom = CompactSkipInset + CompactSkipHeight + RetrySkipGap;

            // The modal is anchored and pivoted at the canvas centre, so its offset is the distance
            // from that centre to the middle of the band it is allowed to occupy.
            _retryRect.sizeDelta = new Vector2(_retryDesignSize.x, height);
            _retryRect.anchoredPosition = new Vector2(
                _retryDesignPosition.x,
                bottom + height * 0.5f - canvasHeight * 0.5f);

            if (_skipRect == null) return;

            // Anchored and pivoted at (0,0): this is the canvas' bottom-left corner, inset so the
            // button does not touch the frame edge that phones lose to a rounded corner or a notch.
            _skipRect.sizeDelta = CompactSkipSize;
            _skipRect.anchoredPosition = new Vector2(CompactSkipInset, CompactSkipInset);
        }

        private static void ApplyStrip(RectTransform rect, Vector2 size, Vector2 position)
        {
            if (rect == null) return;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        /// <summary>
        /// Shows or hides one lane callout. A visible callout is placed on the same frame: waiting for
        /// its late update would pop the hand in a frame late on the step change.
        /// </summary>
        private static void ShowCallout(TutorialWorldAnchor callout, bool visible)
        {
            if (callout == null) return;

            callout.gameObject.SetActive(visible);
            if (visible) callout.Refresh();
        }

        /// <summary>Finds the copy label inside a step group. The tree is fixed, so this is a name lookup, not a search.</summary>
        private static RectTransform CopyRect(GameObject group)
        {
            if (group == null) return null;
            return group.transform.Find("Copy") as RectTransform;
        }
    }
}
