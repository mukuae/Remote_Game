using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Pickup/trigger that unlocks the <see cref="WallClimb"/> ability when the player touches it.
/// Put it on an object with a trigger collider.
/// </summary>
[AddComponentMenu("Abilities/Wall Climb Unlock")]
public class WallClimbUnlock : MonoBehaviour
{
    [Tooltip("Hide this object once it has been picked up.")]
    [SerializeField] private bool hideOnPickup = true;

    [Tooltip("Invoked when the ability gets unlocked.")]
    public UnityEvent onUnlocked = new UnityEvent();

    private void OnTriggerEnter(Collider other)
    {
        WallClimb wallClimb = other.GetComponentInParent<WallClimb>();
        if (wallClimb == null || wallClimb.AbilityUnlocked)
            return;

        wallClimb.Unlock();
        onUnlocked.Invoke();

        if (hideOnPickup)
            gameObject.SetActive(false);
    }
}
