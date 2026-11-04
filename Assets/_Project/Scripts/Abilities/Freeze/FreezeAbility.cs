using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Player ability: freeze a platform in three steps - press, choose, press.
//
//   1. Freeze        - start targeting; the nearest freezable object (TimeControllable)
//                      in front of the player is selected
//   2. CycleTarget   - next target: in front first, nearest to farthest, then behind  (Tab / LB)
//      AimTarget     - flick the right stick to pick the target in that direction
//      CancelTarget  - stop targeting without freezing             (Backspace / B)
//   3. Freeze        - freeze the selected object                  (F / right mouse / RB)
//
//   Attack           - hit a frozen object to store momentum
//
// The selection never changes by itself. If the selected object leaves the range it turns grey
// and can't be frozen until it comes back.
// An object is choosable if it's in range and the player OR the camera can see it. Once choosable
// during a targeting session, it stays in the Tab list while in range (even if it goes off-screen).
// Range is a cylinder by default (height ignored). Every number lives in FreezeSettings.
public class FreezeAbility : MonoBehaviour
{
    [Header("Input (optional - falls back to the project-wide actions by name)")]
    [SerializeField] private InputActionReference freezeAction;
    [SerializeField] private InputActionReference cycleTargetAction;
    [SerializeField] private InputActionReference cancelTargetAction;
    [SerializeField] private InputActionReference aimAction;
    [SerializeField] private InputActionReference strikeAction;

    [Tooltip("Camera used for right-stick target selection. Defaults to the main camera.")]
    [SerializeField] private Camera viewCamera;

    public event Action<TimeControllable> TargetChanged;
    public event Action TargetingStarted;
    public event Action TargetingEnded;
    public event Action<TimeControllable> Froze;
    public event Action FreezeFailed;

    public bool IsTargeting { get; private set; }
    public TimeControllable CurrentTarget { get; private set; }
    public bool IsTargetInRange { get; private set; }
    public bool HasTargetInRange => candidates.Count > 0;
    public int Charges { get; private set; }
    public float RechargeProgress { get; private set; } // 0..1 towards the next charge
    public bool HasFrozenOnce { get; private set; }

    private struct Candidate
    {
        public TimeControllable Target;
        public float Distance;
        public bool IsBehind;
    }

    // Freezable objects in range and in sight: in front of the player first, then behind,
    // each group sorted nearest to farthest.
    private readonly List<Candidate> candidates = new List<Candidate>();

    // Objects that were choosable at some point during the current targeting session.
    private readonly HashSet<TimeControllable> seenWhileTargeting = new HashSet<TimeControllable>();

    // Counted in BuildCandidates, used for the failure hint.
    private int objectsInRange;

    private struct DebugEntry
    {
        public TimeControllable Target;
        public bool Visible;
    }

    private readonly List<DebugEntry> debugEntries = new List<DebugEntry>();

    private InputAction freeze;
    private InputAction cycleTarget;
    private InputAction cancelTarget;
    private InputAction aim;
    private InputAction strike;

    private bool stickWasNeutral = true;
    private AudioSource audioSource;

    private FreezeSettings Settings => FreezeSettings.Instance;
    private Vector3 AimOrigin => transform.position + Vector3.up * Settings.aimOriginHeight;

    private void Awake()
    {
        if (viewCamera == null) viewCamera = Camera.main;

        freeze = ResolveAction(freezeAction, "Player/Freeze");
        cycleTarget = ResolveAction(cycleTargetAction, "Player/CycleTarget");
        cancelTarget = ResolveAction(cancelTargetAction, "Player/CancelTarget");
        aim = ResolveAction(aimAction, "Player/AimTarget");
        strike = ResolveAction(strikeAction, "Player/Attack");

        if (freeze == null)
            Debug.LogWarning("FreezeAbility: no 'Freeze' input action found.", this);

        Charges = Settings.maxCharges;
    }

    private void OnEnable()
    {
        freeze?.Enable();
        cycleTarget?.Enable();
        cancelTarget?.Enable();
        aim?.Enable();
        strike?.Enable();
    }

    private void OnDisable()
    {
        // The actions are shared with other scripts, so they're left enabled.
        if (IsTargeting) EndTargeting();
    }

    private static InputAction ResolveAction(InputActionReference reference, string actionName)
    {
        if (reference != null && reference.action != null) return reference.action;
        return InputSystem.actions != null ? InputSystem.actions.FindAction(actionName) : null;
    }

    private void Update()
    {
        if (PauseMenu.GameIsPaused) return;

        Recharge();
        BuildCandidates();

        if (IsTargeting)
        {
            UpdateCurrentTarget();
            HandleStickFlick();

            if (IsTargeting && cycleTarget != null && cycleTarget.WasPressedThisFrame()) CycleTarget();
            if (IsTargeting && cancelTarget != null && cancelTarget.WasPressedThisFrame()) EndTargeting();
        }
        else
        {
            stickWasNeutral = !IsStickPushed();
        }

        if (freeze != null && freeze.WasPressedThisFrame())
        {
            if (IsTargeting) ConfirmFreeze();
            else StartTargeting();
        }

        if (strike != null && strike.WasPressedThisFrame()) TryStrike();
    }

    // ---------- Charges ----------

    private void Recharge()
    {
        int max = Settings.maxCharges;
        if (Charges >= max)
        {
            Charges = max;
            RechargeProgress = 0f;
            return;
        }

        RechargeProgress += Time.deltaTime / Settings.rechargeTime;
        if (RechargeProgress >= 1f)
        {
            Charges++;
            RechargeProgress = 0f;
        }
    }

    // ---------- Targeting ----------

    private void StartTargeting()
    {
        if (Charges <= 0 || candidates.Count == 0)
        {
            PlaySound(Settings.noChargeSound);
            FreezeFailed?.Invoke();

            if (Settings.showFailureHints)
            {
                string reason = Charges <= 0 ? Settings.noChargesMessage
                    : objectsInRange == 0 ? Settings.nothingInRangeMessage
                    : Settings.nothingInSightMessage;
                ScreenMessage.Show(reason, Settings.failureHintDuration);
            }
            return;
        }

        IsTargeting = true;
        seenWhileTargeting.Clear();
        foreach (Candidate candidate in candidates)
            seenWhileTargeting.Add(candidate.Target);
        stickWasNeutral = !IsStickPushed(); // a stick held while activating doesn't count as a flick
        SetTarget(candidates[0].Target);
        IsTargetInRange = true; // candidates are always in range
        TargetingStarted?.Invoke();
    }

    private void ConfirmFreeze()
    {
        TimeControllable target = CurrentTarget;
        if (target == null)
        {
            EndTargeting();
            return;
        }

        // Out of range: stay in targeting mode so the player can wait for it to come back.
        if (!IsTargetInRange)
        {
            PlaySound(Settings.noChargeSound);
            FreezeFailed?.Invoke();
            return;
        }

        EndTargeting();

        if (Charges <= 0)
        {
            PlaySound(Settings.noChargeSound);
            FreezeFailed?.Invoke();
            return;
        }

        if (!target.Freeze(Settings.freezeDuration)) return;

        Charges--;
        HasFrozenOnce = true;
        Froze?.Invoke(target);
    }

    private void EndTargeting()
    {
        IsTargeting = false;
        seenWhileTargeting.Clear();
        SetTarget(null);
        TargetingEnded?.Invoke();
    }

    // The selection is kept even when it leaves the range; it's only greyed out.
    // It's replaced only if the object stops existing (destroyed, disabled, made unfreezable).
    private void UpdateCurrentTarget()
    {
        if (CurrentTarget == null || !CurrentTarget.isActiveAndEnabled || !CurrentTarget.canBeFrozen || CurrentTarget.IsFrozen)
        {
            SetTarget(null);
            if (candidates.Count > 0)
                SetTarget(candidates[0].Target);
            else
            {
                EndTargeting();
                return;
            }
        }

        IsTargetInRange = IsInRange(CurrentTarget, out _);

        Outline outline = CurrentTarget.GetComponent<Outline>();
        if (outline != null)
            outline.OutlineColor = IsTargetInRange ? Settings.targetOutlineColor : Settings.outOfRangeOutlineColor;
    }

    private void CycleTarget()
    {
        if (candidates.Count == 0) return;

        int next = (IndexOf(CurrentTarget) + 1) % candidates.Count;
        SetTarget(candidates[next].Target);
    }

    private bool IsStickPushed()
    {
        return aim != null && aim.ReadValue<Vector2>().magnitude >= Settings.stickDeadzone;
    }

    // A flick (stick moves out of the neutral zone) selects the object in that screen direction,
    // seen from the current selection - like moving between buttons in a menu.
    private void HandleStickFlick()
    {
        bool pushed = IsStickPushed();
        bool flicked = pushed && stickWasNeutral;
        stickWasNeutral = !pushed;

        if (!flicked || CurrentTarget == null) return;

        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera == null) return;

        Vector2 direction = aim.ReadValue<Vector2>().normalized;
        Vector2 from = viewCamera.WorldToScreenPoint(CurrentTarget.AimPoint);
        float screenSize = Mathf.Max(Screen.width, Screen.height);
        float maxAngle = Settings.stickFlickMaxAngle;

        TimeControllable best = null;
        float bestScore = float.MaxValue;

        foreach (Candidate candidate in candidates)
        {
            if (candidate.Target == CurrentTarget) continue;

            Vector3 screen = viewCamera.WorldToScreenPoint(candidate.Target.AimPoint);
            if (screen.z <= 0f) continue;

            Vector2 offset = (Vector2)screen - from;
            if (offset.sqrMagnitude < 1f) continue;

            float angle = Vector2.Angle(direction, offset);
            if (angle > maxAngle) continue;

            // Prefer objects straight in the flick direction, then nearer ones.
            float score = angle / maxAngle + offset.magnitude / screenSize;
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate.Target;
            }
        }

        if (best != null) SetTarget(best);
    }

    private void BuildCandidates()
    {
        candidates.Clear();

        Vector3 origin = AimOrigin;
        Vector3 forward = transform.forward;
        forward.y = 0f;

        objectsInRange = 0;
        debugEntries.Clear();

        foreach (TimeControllable target in TimeControllable.All)
        {
            if (target == null || !target.canBeFrozen || target.IsFrozen) continue;
            if (!IsInRange(target, out float distance)) continue;
            objectsInRange++;

            bool visible = !Settings.requireLineOfSight
                || (IsTargeting && seenWhileTargeting.Contains(target))
                || IsVisible(target);

            if (Settings.showDebugGizmos)
                debugEntries.Add(new DebugEntry { Target = target, Visible = visible });

            if (!visible) continue;
            if (IsTargeting) seenWhileTargeting.Add(target);

            Vector3 toTarget = target.AimPoint - origin;
            toTarget.y = 0f;
            bool isBehind = toTarget.sqrMagnitude > 0.01f && Vector3.Dot(forward, toTarget) < 0f;

            candidates.Add(new Candidate { Target = target, Distance = distance, IsBehind = isBehind });
        }

        // In front first, then behind; nearest to farthest within each group.
        candidates.Sort((a, b) =>
            a.IsBehind != b.IsBehind ? a.IsBehind.CompareTo(b.IsBehind) : a.Distance.CompareTo(b.Distance));
    }

    // Distance from the player to the nearest edge of the object. With ignoreHeightInRange the
    // range is a cylinder: only horizontal distance counts (optionally limited in height).
    private bool IsInRange(TimeControllable target, out float distance)
    {
        Vector3 origin = AimOrigin;
        Vector3 offset = target.ClosestPoint(origin) - origin;

        if (Settings.ignoreHeightInRange)
        {
            float maxHeight = Settings.maxHeightDifference;
            if (maxHeight > 0f && Mathf.Abs(offset.y) > maxHeight)
            {
                distance = float.MaxValue;
                return false;
            }
            offset.y = 0f;
        }

        distance = offset.magnitude;
        return distance <= Settings.targetRange;
    }

    // Visible if the player or the camera can see the object's center or top.
    private bool IsVisible(TimeControllable target)
    {
        if (CanSee(AimOrigin, target)) return true;

        if (viewCamera == null) viewCamera = Camera.main;
        return viewCamera != null && CanSee(viewCamera.transform.position, target);
    }

    private bool CanSee(Vector3 from, TimeControllable target)
    {
        return HasLineOfSight(from, target.AimPoint, target) || HasLineOfSight(from, target.TopPoint, target);
    }

    private bool HasLineOfSight(Vector3 from, Vector3 point, TimeControllable target)
    {
        // A few steps, so the player's own body (e.g. between camera and target) is looked past.
        for (int step = 0; step < 3; step++)
        {
            if (!Physics.Linecast(from, point, out RaycastHit hit, Settings.lineOfSightMask, QueryTriggerInteraction.Ignore))
                return true;

            if (!hit.collider.transform.IsChildOf(transform))
                return hit.collider.GetComponentInParent<TimeControllable>() == target;

            from = hit.point + (point - from).normalized * 0.05f;
        }
        return false;
    }

    // Debug view (Scene view, Play mode): range and lines to every freezable object in range.
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !Settings.showDebugGizmos) return;

        Vector3 origin = AimOrigin;
        float range = Settings.targetRange;
        float maxHeight = Settings.ignoreHeightInRange ? Settings.maxHeightDifference : 0f;

        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f);
        if (maxHeight > 0f)
        {
            DrawCircle(origin + Vector3.up * maxHeight, range);
            DrawCircle(origin - Vector3.up * maxHeight, range);
        }
        else
        {
            DrawCircle(origin, range);
        }

        foreach (DebugEntry entry in debugEntries)
        {
            if (entry.Target == null) continue;
            Gizmos.color = entry.Target == CurrentTarget ? Color.yellow : entry.Visible ? Color.green : Color.red;
            Gizmos.DrawLine(origin, entry.Target.AimPoint);
        }
    }

    private static void DrawCircle(Vector3 center, float radius)
    {
        const int segments = 64;
        Vector3 previous = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    private int IndexOf(TimeControllable target)
    {
        if (target == null) return -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Target == target) return i;
        }
        return -1;
    }

    private void SetTarget(TimeControllable target)
    {
        if (target == CurrentTarget) return;

        if (CurrentTarget != null)
        {
            Outline oldOutline = CurrentTarget.GetComponent<Outline>();
            if (oldOutline != null) oldOutline.enabled = false;
        }

        CurrentTarget = target;

        if (target != null)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null) outline = target.gameObject.AddComponent<Outline>();
            outline.OutlineColor = Settings.targetOutlineColor;
            outline.OutlineWidth = Settings.targetOutlineWidth;
            outline.enabled = true;
        }

        TargetChanged?.Invoke(target);
    }

    // ---------- Momentum ----------

    // Hits the closest frozen object in front of the player, storing momentum in the facing direction.
    private void TryStrike()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return;
        forward.Normalize();

        float range = Settings.strikeRange;
        Vector3 center = AimOrigin + forward * (range * 0.5f);
        Collider[] hits = Physics.OverlapSphere(center, range * 0.5f + 0.25f, ~0, QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            TimeControllable target = hit.GetComponentInParent<TimeControllable>();
            if (target != null && target.IsFrozen && target.TryAddHit(forward))
                return;
        }
    }

    // Running into a frozen object counts as a hit (CharacterController callback).
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!Settings.bumpCountsAsHit) return;
        if (hit.normal.y > 0.7f) return; // standing on top of it doesn't count

        Vector3 pushDirection = hit.moveDirection;
        pushDirection.y = 0f;
        if (pushDirection.sqrMagnitude < 0.09f) return; // mostly vertical movement

        TimeControllable target = hit.collider.GetComponentInParent<TimeControllable>();
        if (target != null && target.IsFrozen)
            target.TryAddHit(pushDirection);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        audioSource.PlayOneShot(clip, Settings.soundVolume);
    }
}
