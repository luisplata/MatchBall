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
    // child. Its blocks are rotated individually; rotating the container would swap them.
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

        // Rotate each block about its own centre, NOT the cluster container. The blocks are
        // edge-anchored (name pinned to the cluster's top edge, score to its bottom), so rotating
        // the container leaves its box in place but swaps the blocks inside it.
        //
        // Then move the whole cluster to the opposite screen edge: P1's sits above its ball queue
        // at the top, while P2's queue ends up at the bottom after the flip, so its score has to
        // follow it there. The margin is mirrored instead of hardcoded so it survives a re-layout.
        if (hudCluster != null && hudCluster.childCount > 0)
        {
            for (var i = 0; i < hudCluster.childCount; i++)
            {
                if (hudCluster.GetChild(i) is RectTransform block)
                {
                    block.localRotation = Quaternion.Euler(0f, 0f, 180f);
                }
            }

            MirrorToOppositeEdge(hudCluster);
        }
        else
        {
            Debug.LogError("[PlayerBoardView] P2 HUD cluster has no blocks to rotate; it will read upside down for its owner.");
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
    /// Moves a screen-space rect to the opposite vertical edge of its canvas, keeping the same
    /// margin it currently keeps from the near edge. The opponent's HUD uses it so the score sits
    /// next to its own ball queue instead of on the other player's side.
    /// </summary>
    private static void MirrorToOppositeEdge(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>();
        var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        if (canvasRect == null)
        {
            Debug.LogError("[PlayerBoardView] HUD cluster has no canvas to mirror against; it stays where it was authored.");
            return;
        }

        var canvasCorners = new Vector3[4];
        canvasRect.GetWorldCorners(canvasCorners);

        var clusterCorners = new Vector3[4];
        rect.GetWorldCorners(clusterCorners);

        // GetWorldCorners order: bottom-left, top-left, top-right, bottom-right.
        var marginFromTop = canvasCorners[1].y - clusterCorners[1].y;
        var targetBottom = canvasCorners[0].y + marginFromTop;

        rect.position += new Vector3(0f, targetBottom - clusterCorners[0].y, 0f);
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
