using System.Collections.Generic;
using UnityEngine;

// Attach this to the pendulum's platform (bob) GameObject.
// It carries any CharacterController-based player standing on top along with
// the platform's movement AND rotation - no changes to the player's own script needed.
//
// Setup on this GameObject:
// 1. Keep the existing solid Collider (non-trigger) that the player physically stands on.
// 2. Add a SECOND Collider (e.g. a slightly larger/taller BoxCollider) and check "Is Trigger".
//    This is what detects the player standing on top.
// 3. Add a Rigidbody, set "Is Kinematic" = true and "Use Gravity" = false.
//    (This is required for trigger events to reliably fire - it won't affect the
//    Animator-driven swing at all.)
// 4. Make sure the player GameObject has the tag "Player".
public class PendulumPlatformCarrier : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private Vector3 lastPosition;
    private Quaternion lastRotation;

    private readonly List<CharacterController> ridersOnPlatform = new List<CharacterController>();

    private void Start()
    {
        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }

    // LateUpdate runs after every Update() this frame (including the player's own movement
    // and after the Animator has applied this frame's swing), so we always add the
    // platform's motion on top of whatever the player already did this frame.
    private void LateUpdate()
    {
        Vector3 deltaPosition = transform.position - lastPosition;
        Quaternion deltaRotation = transform.rotation * Quaternion.Inverse(lastRotation);

        for (int i = ridersOnPlatform.Count - 1; i >= 0; i--)
        {
            CharacterController rider = ridersOnPlatform[i];
            if (rider == null)
            {
                ridersOnPlatform.RemoveAt(i);
                continue;
            }

            Vector3 offsetFromPlatform = rider.transform.position - lastPosition;
            Vector3 rotatedOffset = deltaRotation * offsetFromPlatform;
            Vector3 movement = deltaPosition + (rotatedOffset - offsetFromPlatform);

            rider.Move(movement);
        }

        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        CharacterController cc = other.GetComponent<CharacterController>();
        if (cc != null && !ridersOnPlatform.Contains(cc))
        {
            ridersOnPlatform.Add(cc);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        CharacterController cc = other.GetComponent<CharacterController>();
        if (cc != null)
        {
            ridersOnPlatform.Remove(cc);
        }
    }
}