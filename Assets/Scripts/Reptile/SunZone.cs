using UnityEngine;

/// <summary>
/// Marker component: add it to ANY GameObject that has a Collider (or
/// Collider2D) to make it a sun zone. While the player (with a LightPoints
/// component) touches it, their Light Points refill.
///
/// Works with trigger volumes (Is Trigger ticked) and with solid colliders.
/// Disable the component or the GameObject to switch the zone off.
/// </summary>
[AddComponentMenu("Light/Sun Zone")]
public class SunZone : MonoBehaviour
{
}