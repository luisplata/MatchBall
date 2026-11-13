using UnityEngine;

namespace Presentation.Tutorial
{
    /// <summary>
    /// The one world-to-canvas conversion the tutorial layer uses. Stateless on purpose: the framing
    /// can move (the Main Camera has a CinemachineBrain and a 9:19 LetterBoxer), so every caller
    /// re-projects on its own late update instead of caching a screen offset.
    /// </summary>
    /// <remarks>
    /// <see cref="Camera.WorldToScreenPoint"/> is not used because the LetterBoxer rewrites
    /// <c>cam.rect</c>: the camera renders into a sub-rect of the screen, so the viewport has to be
    /// scaled by <see cref="Camera.pixelRect"/> before it becomes a screen point. The target canvas is
    /// Screen Space Overlay, which is why the rectangle conversion passes a null camera.
    /// </remarks>
    internal static class TutorialProjection
    {
        /// <summary>
        /// Projects <paramref name="world"/> into <paramref name="canvas"/> local space (origin at the
        /// rect's pivot). Returns false when the canvas has no layout yet, when there is no camera, or
        /// when the point sits behind the camera or outside the camera's viewport.
        /// </summary>
        /// <remarks>
        /// The off-viewport case is a rejection, not a clamp: a point left or right of the letterboxed
        /// frame would still yield a numeric local point (the rectangle conversion does not reject it),
        /// and callers need to know the anchor is no longer on screen to take their fallback path.
        /// </remarks>
        internal static bool TryWorldToCanvas(RectTransform canvas, Camera cam, Vector3 world, out Vector2 local)
        {
            local = Vector2.zero;

            if (canvas == null || cam == null) return false;
            if (canvas.rect.height <= 0f) return false;

            var viewport = cam.WorldToViewportPoint(world);
            if (viewport.z <= 0f) return false;
            if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) return false;

            var pixelRect = cam.pixelRect;
            var screenPoint = new Vector2(
                pixelRect.x + viewport.x * pixelRect.width,
                pixelRect.y + viewport.y * pixelRect.height);

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screenPoint, null, out local);
        }
    }
}
