using UnityEngine;

namespace Presentation.Tutorial
{
    /// <summary>
    /// Anchors a callout (the pointing hand plus its caption) to the BoxCollider2D of the lane it is
    /// teaching. The projection runs every late update because the framing can move and because the
    /// collider's bounds are one physics step behind whatever shakes the board art.
    /// </summary>
    public sealed class TutorialWorldAnchor : MonoBehaviour
    {
        /// <summary>
        /// How far under the lane's top edge the tip hangs. The lane collider is 13 units tall and the
        /// hand has to sit in its upper third, where a tap actually lands.
        /// </summary>
        private const float TipDropFromLaneTop = 1.20f;

        private Camera _camera;
        private Collider2D _lane;
        private RectTransform _rect;
        private bool _warned;

        /// <summary>True while a lane is wired; the callout is hidden while it is false.</summary>
        public bool HasTarget => _lane != null;

        private void Awake()
        {
            _rect = transform as RectTransform;
        }

        /// <summary>Wires the camera and the lane to teach. Called once by <see cref="TutorialOverlay"/>.</summary>
        public void Bind(Camera camera, Collider2D lane)
        {
            _camera = camera;
            _lane = lane;
        }

        /// <summary>
        /// Re-projects the tip onto the lane's collider. Public so the overlay can force a placement on
        /// the frame it shows the callout, before the next late update runs.
        /// </summary>
        public void Refresh()
        {
            if (_rect == null) return;

            if (_lane == null)
            {
                // No lane means nothing to point at: hide instead of drawing a hand over empty space.
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[Tutorial] the lane callout has no collider; it stays hidden.");
                }

                gameObject.SetActive(false);
                return;
            }

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            var bounds = _lane.bounds;
            var tipWorld = new Vector3(bounds.center.x, bounds.max.y - TipDropFromLaneTop, 0f);

            if (!TutorialProjection.TryWorldToCanvas(transform.parent as RectTransform, _camera, tipWorld, out var local))
                return;

            _rect.anchoredPosition = local;
        }

        private void LateUpdate()
        {
            if (!isActiveAndEnabled || !HasTarget) return;
            Refresh();
        }
    }
}
