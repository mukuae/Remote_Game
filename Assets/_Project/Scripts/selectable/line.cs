using System.Collections.Generic;
using UnityEngine;

// Put this on the Player (or a manager object).
// Selects the closest Selectable whose player->object line the mouse is hovering near.
// If several selectables line up behind each other, the nearest one wins by default;
// press cycleKey to step further back into the stack.
public class LineSelector : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public Transform lineOrigin;          // the point the line starts from (eyes/hand/etc)
    public LayerMask selectableLayer;

    [Header("Tuning")]
    public float pixelThreshold = 15f;    // how close (in screen pixels) the mouse must be to the line
    public float maxSelectDistance = 50f; // world-space range to even consider objects
    public KeyCode cycleKey = KeyCode.Tab; // press to reach an object hidden behind the current one

    public Selectable CurrentSelection { get; private set; }

    private readonly List<Selectable> _stack = new List<Selectable>();
    private int _cycleIndex = 0;

    void Update()
    {
        _stack.Clear();

        Collider[] candidates = Physics.OverlapSphere(lineOrigin.position, maxSelectDistance, selectableLayer);
        Vector3 screenStart = playerCamera.WorldToScreenPoint(lineOrigin.position);

        // Gather every selectable whose line the mouse is hovering near,
        // along with how precisely the cursor is aimed at it and how far away it is.
        var inRange = new List<(Selectable sel, float pixelDist, float worldDist)>();

        foreach (var col in candidates)
        {
            Selectable sel = col.GetComponentInParent<Selectable>();
            if (sel == null) continue;

            Vector3 targetPos = col.bounds.center;
            Vector3 screenEnd = playerCamera.WorldToScreenPoint(targetPos);
            if (screenStart.z < 0 || screenEnd.z < 0) continue; // behind camera

            float pixelDist = DistancePointToSegment(Input.mousePosition, screenStart, screenEnd);
            if (pixelDist < pixelThreshold)
                inRange.Add((sel, pixelDist, Vector3.Distance(lineOrigin.position, targetPos)));
        }

        inRange.Sort((a, b) =>
        {
            // Different lines, different aim precision -> whichever the cursor is closer to wins.
            float pixelDiff = a.pixelDist - b.pixelDist;
            if (Mathf.Abs(pixelDiff) > 2f)
                return pixelDiff < 0f ? -1 : 1;

            // Aim is basically tied (lines overlap on screen, e.g. one object behind another)
            // -> prefer whichever is physically closer to the player.
            return a.worldDist.CompareTo(b.worldDist);
        });

        foreach (var entry in inRange)
            _stack.Add(entry.sel);

        if (_stack.Count == 0)
        {
            _cycleIndex = 0;
            SetSelection(null);
            return;
        }

        if (Input.GetKeyDown(cycleKey))
            _cycleIndex = (_cycleIndex + 1) % _stack.Count; // step deeper into the stack
        else if (_cycleIndex >= _stack.Count)
            _cycleIndex = 0; // stack shrank (something moved off-screen) - snap back to front

        SetSelection(_stack[_cycleIndex]);
    }

    void SetSelection(Selectable sel)
    {
        if (CurrentSelection == sel) return;

        if (CurrentSelection != null)
            CurrentSelection.OnDeselected();

        CurrentSelection = sel;

        if (CurrentSelection != null)
            CurrentSelection.OnSelected();
    }

    // Shortest distance from point p to segment a-b, all in screen space (pixels)
    float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float sqrLen = ab.sqrMagnitude;
        if (sqrLen < 0.0001f) return Vector2.Distance(p, a);

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / sqrLen);
        Vector2 closest = a + t * ab;
        return Vector2.Distance(p, closest);
    }

    void OnDrawGizmos()
    {
        if (CurrentSelection == null || lineOrigin == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(lineOrigin.position, CurrentSelection.transform.position);
    }
}