using UnityEngine;

/// <summary>
/// Single owner of where a player board is and which way is up for its owner.
/// The board is authored for the first seat (x = 5.35, identity rotation); a second seat
/// mirrors it about its own ball columns and turns it 180 degrees about Z, so the board
/// stays inside the same camera frame. Only local PvP ever calls <see cref="ApplyOrientation"/>;
/// in PvE and the tutorial the root keeps its authored transform.
/// </summary>
public sealed class PlayerBoardView : MonoBehaviour
{
    [SerializeField] private Balance balance;

    // The cluster lives in Canvas_UI_In_Game, not under the board, so it is a reference and not a
    // child: rotating it in place is the whole counter-fix (the band is already the top strip).
    [SerializeField] private RectTransform hudCluster;

    // Elements positioned in board coordinates whose validity depends on the screen frame:
    // P2's SlipScreen lands across P1's board once the root is mirrored.
    [SerializeField] private GameObject[] screenSpaceStragglers;

    /// <summary>X of the board's own vertical axis: the midpoint of its two ball columns. NaN when unreadable.</summary>
    public float BoardCenterX => TryResolveBoardCenter(out var center) ? center : float.NaN;

    /// <summary>The HUD cluster this board describes. Wired by <see cref="ApplyOrientation"/>.</summary>
    public RectTransform HudCluster => hudCluster;

    /// <summary>
    /// The one entry point that changes the board's orientation. <c>false</c> is the identity,
    /// which is what every other board in the project already assumes.
    /// </summary>
    public void ApplyOrientation(bool flipped)
    {
        if (!flipped) return;

        if (!TryResolveBoardCenter(out var center))
        {
            // Fail visible: a wrong board orientation is far worse than no flip at all.
            Debug.LogError(
                "[PlayerBoardView] cannot derive the board centre from the two ball columns; board left unflipped.");
            return;
        }

        var targetX = 2f * center - transform.position.x;
        var targetPosition = new Vector3(targetX, transform.position.y, transform.position.z);
        transform.SetPositionAndRotation(targetPosition, Quaternion.Euler(0f, 0f, 180f));

        // The cluster is a screen-space rect: rotate it about its own pivot (0.5/0.5) so the
        // bounding box stays identical. Moving it would be a second layout to maintain.
        if (hudCluster != null)
        {
            hudCluster.localRotation = Quaternion.Euler(0f, 0f, 180f);
        }
        else
        {
            Debug.LogError("[PlayerBoardView] P2 HUD cluster is not wired; it will read upside down for its owner.");
        }

        if (screenSpaceStragglers != null)
        {
            foreach (var straggler in screenSpaceStragglers)
            {
                if (straggler != null) straggler.SetActive(false);
            }
        }

        Debug.Log($"[PlayerBoardView] flipped about its own columns: centre={center:F2} root x={targetX:F2}");
    }

    /// <summary>
    /// Derives the pivot from the two ball columns instead of hardcoding a magic x, so a
    /// re-authored or moved board keeps mirroring itself correctly. Must run before the
    /// rotation is applied: the paths are still unflipped at that point.
    /// </summary>
    private bool TryResolveBoardCenter(out float center)
    {
        center = float.NaN;

        if (balance == null) return false;

        var leftPath = balance.GetLeftPath();
        var rightPath = balance.GetRightPath();
        if (leftPath == null || rightPath == null) return false;

        var left = leftPath.GetBasePosition();
        var right = rightPath.GetBasePosition();
        if (left == null || right == null) return false;

        center = (left.position.x + right.position.x) / 2f;
        return !float.IsNaN(center) && !float.IsInfinity(center);
    }
}
