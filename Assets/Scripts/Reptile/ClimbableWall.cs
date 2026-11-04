using UnityEngine;

/// <summary>
/// Marker component: add it to ANY GameObject that has a Collider (or
/// Collider2D) to let the player grab onto it with <see cref="WallClimb"/>.
/// Walls without this component can't be grabbed.
///
/// A collider on a child of the marked object counts too. Disable the
/// component to temporarily make the wall ungrabbable.
/// </summary>
[AddComponentMenu("Light/Climbable Wall")]
public class ClimbableWall : MonoBehaviour
{
}
