using UnityEngine;
using UnityEngine.UI;

namespace Presentation.Tutorial
{
    /// <summary>
    /// The step indicator: one dot per lesson step, filled up to the current one. It deliberately
    /// knows nothing about <see cref="TutorialStep"/>: the overlay feeds it an index, so the enum
    /// stays the only owner of the curriculum order.
    /// </summary>
    /// <remarks>
    /// Every state always writes a tint, and a sprite is swapped only when one is assigned. That way
    /// the indicator is readable before any art exists: the three sprites are placeholders in the
    /// manifest, not a requirement for the component to work.
    /// </remarks>
    public sealed class TutorialStepDots : MonoBehaviour
    {
        [Tooltip("One image per step, left to right. The overlay checks the count against the step enum.")]
        [SerializeField] private Image[] dots;

        [Header("Sprites (optional until the art lands)")]
        [SerializeField] private Sprite doneSprite;
        [SerializeField] private Sprite activeSprite;
        [SerializeField] private Sprite normalSprite;

        [Header("Tints (always applied)")]
        [SerializeField] private Color doneColor = new Color(1f, 1f, 1f, 0.55f);
        [SerializeField] private Color activeColor = Color.white;
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.25f);

        // Strip geometry from the design's layout tables. Four dots always fit exactly: 3 gaps plus
        // 4 dots equals the strip width, so the indicator never overlaps the bubble's edge.
        private static readonly Vector2 FullSize = new Vector2(148f, 28f);
        private static readonly Vector2 CompactSize = new Vector2(116f, 20f);
        private const float FullSpacing = 40f;
        private const float CompactSpacing = 32f;
        private const float FullDotSize = 28f;
        private const float CompactDotSize = 20f;
        private const float FullTopOffset = -24f;
        private const float CompactTopOffset = -12f;

        /// <summary>How many dots are wired. Zero when the prefab has no array, which is not an error.</summary>
        public int Count => dots != null ? dots.Length : 0;

        /// <summary>
        /// Applies the strip's geometry for the bubble mode it sits in. The dots strip shrinks with
        /// the bubble, and the spacing is derived from the size so four dots always fit exactly.
        /// </summary>
        public void ApplyLayout(bool compact)
        {
            var rect = transform as RectTransform;
            if (rect != null)
            {
                rect.sizeDelta = compact ? CompactSize : FullSize;
                rect.anchoredPosition = new Vector2(0f, compact ? CompactTopOffset : FullTopOffset);
            }

            if (dots == null) return;

            var spacing = compact ? CompactSpacing : FullSpacing;
            var dotSize = compact ? CompactDotSize : FullDotSize;

            for (var i = 0; i < dots.Length; i++)
            {
                var dot = dots[i];
                if (dot == null) continue;

                var dotRect = dot.transform as RectTransform;
                if (dotRect == null) continue;

                dotRect.sizeDelta = new Vector2(dotSize, dotSize);
                dotRect.anchoredPosition = new Vector2(i * spacing, 0f);
            }
        }

        /// <summary>
        /// Paints the indicator for <paramref name="index"/>. Earlier dots read as done, the current
        /// one as active, the rest as pending. The index is clamped, so a resumed step can never blank
        /// the indicator.
        /// </summary>
        public void SetCurrent(int index)
        {
            if (dots == null || dots.Length == 0) return;

            var current = Mathf.Clamp(index, 0, dots.Length - 1);

            for (var i = 0; i < dots.Length; i++)
            {
                var dot = dots[i];
                if (dot == null) continue;

                if (i < current) Paint(dot, doneSprite, doneColor);
                else if (i == current) Paint(dot, activeSprite, activeColor);
                else Paint(dot, normalSprite, normalColor);
            }
        }

        /// <summary>
        /// Back to all-pending. Called when the overlay hides, so the next session never starts with
        /// a stale progress reading on screen.
        /// </summary>
        public void Clear()
        {
            if (dots == null) return;

            for (var i = 0; i < dots.Length; i++)
            {
                var dot = dots[i];
                if (dot != null) Paint(dot, normalSprite, normalColor);
            }
        }

        private static void Paint(Image dot, Sprite sprite, Color tint)
        {
            if (sprite != null) dot.sprite = sprite;
            dot.color = tint;
        }
    }
}
