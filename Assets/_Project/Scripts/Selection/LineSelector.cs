using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Put this on the Player (or a manager object).
// Selects the closest SelectableObject whose player->object line the mouse is hovering near.
// If several selectables line up behind each other, the nearest one wins by default;
// press cycleKey to step further back into the stack.
public class LineSelector : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    [Tooltip("The point the line starts from (eyes/hand/etc).")]
    public Transform lineOrigin;
    public LayerMask selectableLayer;

    [Header("Tuning")]
    [Tooltip("How close (in screen pixels) the mouse must be to the line.")]
    public float pixelThreshold = 15f;
    [Tooltip("World-space range to even consider objects.")]
    public float maxSelectDistance = 50f;
    [Tooltip("Press to reach an object hidden behind the current one.")]
    public Key cycleKey = Key.Tab;

    public SelectableObject CurrentSelection { get; private set; }

    private struct Candidate
    {
        public SelectableObject Selectable;
        public float PixelDistance;
        public float WorldDistance;
    }

    private readonly List<Candidate> candidates = new List<Candidate>();
    private int cycleIndex = 0;

    private void Update()
    {
        if (PauseMenu.GameIsPaused || playerCamera == null || lineOrigin == null || Mouse.current == null)
            return;

        GatherCandidates(Mouse.current.position.ReadValue());

        if (candidates.Count == 0)
        {
            cycleIndex = 0;
            SetSelection(null);
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[cycleKey].wasPressedThisFrame)
            cycleIndex = (cycleIndex + 1) % candidates.Count; // step deeper into the stack
        else if (cycleIndex >= candidates.Count)
            cycleIndex = 0; // stack shrank (something moved off-screen) - snap back to front

        SetSelection(candidates[cycleIndex].Selectable);
    }

    // Collects every selectable whose line the mouse is near, sorted by aim precision, then distance.
    private void GatherCandidates(Vector2 mousePosition)
    {
        candidates.Clear();

        Collider[] colliders = Physics.OverlapSphere(lineOrigin.position, maxSelectDistance, selectableLayer);
        Vector3 screenStart = playerCamera.WorldToScreenPoint(lineOrigin.position);

        foreach (Collider col in colliders)
        {
            SelectableObject selectable = col.GetComponentInParent<SelectableObject>();
            if (selectable == null) continue;

            Vector3 targetPos = col.bounds.center;
            Vector3 screenEnd = playerCamera.WorldToScreenPoint(targetPos);
            if (screenStart.z < 0 || screenEnd.z < 0) continue; // behind camera

            float pixelDist = DistancePointToSegment(mousePosition, screenStart, screenEnd);
            if (pixelDist < pixelThreshold)
            {
                candidates.Add(new Candidate
                {
                    Selectable = selectable,
                    PixelDistance = pixelDist,
                    WorldDistance = Vector3.Distance(lineOrigin.position, targetPos)
                });
            }
        }

        candidates.Sort((a, b) =>
        {
            // Different lines, different aim precision -> whichever the cursor is closer to wins.
            float pixelDiff = a.PixelDistance - b.PixelDistance;
            if (Mathf.Abs(pixelDiff) > 2f)
                return pixelDiff < 0f ? -1 : 1;

            // Aim is basically tied (lines overlap on screen, e.g. one object behind another)
            // -> prefer whichever is physically closer to the player.
            return a.WorldDistance.CompareTo(b.WorldDistance);
        });
    }

    private void SetSelection(SelectableObject selectable)
    {
        if (CurrentSelection == selectable) return;

        if (CurrentSelection != null)
            CurrentSelection.OnDeselected();

        CurrentSelection = selectable;

        if (CurrentSelection != null)
            CurrentSelection.OnSelected();
    }

    // Shortest distance from point p to segment a-b, all in screen space (pixels).
    private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float sqrLen = ab.sqrMagnitude;
        if (sqrLen < 0.0001f) return Vector2.Distance(p, a);

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / sqrLen);
        Vector2 closest = a + t * ab;
        return Vector2.Distance(p, closest);
    }

    private void OnDrawGizmos()
    {
        if (CurrentSelection == null || lineOrigin == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(lineOrigin.position, CurrentSelection.transform.position);
    }
}
