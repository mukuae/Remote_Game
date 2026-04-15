using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera
{
    /// <summary>
    /// This class contains specialized function for manipulating the camera tracker. Such as displacment by effectors that lock clamp the velocity in the tangent direction. 
    /// Collision prediction velocity compensation and methods to lead/lag the target.
    /// </summary>
    [Serializable]
    public class DynamicCameraFunctions
    {
        // velocity compensation for collision prediction
        [Range(0, 2)]
        public float lookAheadTimeFirst = 1;    // looks ahead x seconds for the raycast (dist = vel * lookAheadTime)
        [Range(0, 2)]
        public float lookAheadTimeSecond = 1;   // looks ahead y seconds for the second raycast (dist = vel * lookAheadTime)

        public LayerMask ignoreRayLayerMask;    // layer to ignore while casting the ray for collision prediction
        

        // settings for velocity compensated lead/lag tracking or for positional lead/lag tracking
        [Range(-5, 5)]
        public float leadLagMaxDistanceX = 0;   // the desired lead/lag distance on the X axis (can be negative)
        [Range(-5, 5)]
        public float leadLagMaxDistanceY = 0;   // the desired lead/lag distance on the Y axis (can be negative)
        [Range(0, 30)]
        public float leadLagMaxAtVelocity = 5;  // the max distance is reached when we go over this velocity

        public bool leadLagBoxClamp = true;     // clamp the distances per axis or use the direction of the velocity as direction for a circle/ellipse

        public DynamicCameraFunctions()
        {

        }


        /// <summary>
        /// Displaces the given position by the effectors displacement
        /// </summary>
        /// <param name="position">position before displacement</param>
        /// <returns>The displaced position</returns>
        public Vector3 DisplaceByEffectors(Vector3 position)
        {
            Vector3 newPosition = position;
            if (DCEffectorManager.GetDisplacementAt(position, out DCEffectorOutputData effectorOutput, true, true))
            {
                newPosition = position + effectorOutput.displacement;
            }
            return newPosition;
        }

        /// <summary>
        /// Displaces the given position by the effectors displacement and adjust the tracking velocity of the target so that it hooks onto the effector better
        /// </summary>
        /// <param name="position">position before displacement</param>
        /// <param name="targetVelocity">the velocity that gets adjusted according to the influence of the effector</param>
        /// <returns>The displaced position</returns>
        public Vector3 DisplaceByEffectors(Vector3 position, ref Vector2 targetVelocity)
        {
            return DisplaceByEffectors(position, ref targetVelocity, out DCEffectorOutputData displacementOutput);
        }


        /// <summary>
        /// Displaces the given position by the effectors displacement and adjust the tracking velocity of the target so that it hooks onto the effector better
        /// </summary>
        /// <param name="position">position before displacement</param>
        /// <param name="targetVelocity">the velocity that gets adjusted according to the influence of the effector</param>        
        /// <param name="displacement">the total displacement on the position by the effectors</param>
        /// <returns>The displaced position</returns>
        public Vector3 DisplaceByEffectors(Vector3 position, ref Vector2 targetVelocity, out DCEffectorOutputData displacementOutput)
        {
            Vector2 targetAcceleration = Vector2.zero;
            return DisplaceByEffectors(position, ref targetVelocity, ref targetAcceleration, out displacementOutput);
        }


        /// <summary>
        /// Displaces the given position by the effectors displacement and adjust the tracking velocity and acceleration of the target so that it hooks onto the effector better
        /// </summary>
        /// <param name="position">position before displacement</param>
        /// <param name="targetVelocity">the velocity that gets adjusted according to the influence of the effector</param>     
        /// <param name="targetAcceleration">the acceleration that gets adjusted according to the influence of the effector</param>
        /// <param name="displacementOutput">the total displacement on the position by the effectors</param>
        /// <returns></returns>
        public Vector3 DisplaceByEffectors(Vector3 position, ref Vector2 targetVelocity, ref Vector2 targetAcceleration, out DCEffectorOutputData displacementOutput)
        {
            displacementOutput.displacement = Vector3.zero;
            displacementOutput.lockedXY = false;
            displacementOutput.influence = 0;

            Vector3 newPosition = position;
            if (DCEffectorManager.GetDisplacementAt(position, out DCEffectorOutputData effectorOutput, true, true))
            {
                newPosition = position + effectorOutput.displacement;
                displacementOutput = effectorOutput;
                targetVelocity = ProjectVelocityOnTangent(targetVelocity, effectorOutput);
                targetAcceleration = ProjectVelocityOnTangent(targetAcceleration, effectorOutput);  // Note that this is not a velocity, but the projection works exactly the same
            }

            return newPosition;
        }

        /// <summary>
        /// Projects current target velocity on the output tangent with a weighted total direction based on the effectorOutput influence
        /// </summary>
        /// <param name="targetVelocity"></param>
        /// <param name="effectorOutput"></param>
        /// <returns></returns>
        public Vector3 ProjectVelocityOnTangent(Vector2 targetVelocity, DCEffectorOutputData effectorOutput)
        {
            Vector2 tangent = effectorOutput.Tangent;
            if (tangent.sqrMagnitude == 0)
            {
                if (effectorOutput.lockedXY) return Vector2.zero;
                else return targetVelocity;
            }

            Vector2 velocityProjOnTangent = Vector2.Dot(tangent, targetVelocity) / tangent.sqrMagnitude * tangent;

            targetVelocity = (1 - effectorOutput.influence) * targetVelocity + effectorOutput.influence * velocityProjOnTangent;

            if (effectorOutput.lockedXY)
            {
                targetVelocity = Vector2.zero;
            }

            return targetVelocity;
        }



        /// <summary>
        /// This shifts the target velocity in such a way that it is possible lead/lag the target. This creates an offset to the target without displacing the target position!!!  
        /// This gives the same lead/lag as the position compensated one.
        /// </summary>
        /// <param name="targetVelocity">current velocity of the target</param>
        /// <param name="dynamicCameratracker">the camera tracker, make sure to have smoothTime > 0.1 else jitter can occur</param>
        /// <param name="influence">0 means not affected and 1 means 100% affected with the given parameters</param>
        /// <returns>The compensated velocity</returns>
        public Vector3 LeadLagTargetByVelocityCompensation(Vector3 targetVelocity, DynamicCameraTracker dynamicCameratracker, float influence = 1)
        {
            const float velocityMin = 0;   // minimal velocity threshold

            // copy the max distance as it is changed if we want to clamp it like a circle or ellipse
            float maxDistanceX = leadLagMaxDistanceX;
            float maxDistanceY = leadLagMaxDistanceY;

            if (!leadLagBoxClamp)
            {
                // use the targetVelocity as the direction of the ellipse/circle.
                Vector2 direction2D = new Vector2(targetVelocity.x, targetVelocity.y).normalized;
                maxDistanceX = Mathf.Abs(direction2D.x) * leadLagMaxDistanceX;
                maxDistanceY = Mathf.Abs(direction2D.y) * leadLagMaxDistanceY;
            }

            // factors to smooth out the new tracking positions between 0 and leadLagMaxAtVelocity
            float velInterpFacX = 1;
            float velInterpFacY = 1;

            // avoid division by zero
            if ((leadLagMaxAtVelocity - velocityMin) != 0)
            {
                // Calculates the velocity factor so that we can get smooth transition of the lead lag
                // This interpolates the velocity between zero and max velocity to a range of 0 and 1.
                // The maxDistance is reached when the targetVelocity is over the max velocity treshold
                if (!leadLagBoxClamp)
                {
                    // for ellipsoid / circle clamps we must take the magnitude of the velocity
                    velInterpFacX = Mathf.Clamp((targetVelocity.magnitude - velocityMin) / (leadLagMaxAtVelocity - velocityMin), 0, 1);
                    velInterpFacY = velInterpFacX;
                }
                else
                {
                    // for box clamps we take the abs values per axis of the velocity
                    velInterpFacX = Mathf.Clamp((Mathf.Abs(targetVelocity.x) - velocityMin) / (leadLagMaxAtVelocity - velocityMin), 0, 1);
                    velInterpFacY = Mathf.Clamp((Mathf.Abs(targetVelocity.y) - velocityMin) / (leadLagMaxAtVelocity - velocityMin), 0, 1);
                }
            }

            // factors for multiplying the velocity later on
            float facX = 1;
            float facY = 1;

            // determine the factor for the desired distance. This is based on a critical damped spring damper system.
            // This is determined by the mathemathical model of the system. When using a critical damped system this simplies to:
            if (Mathf.Abs(targetVelocity.x) > 1e-3)
            {
                facX = maxDistanceX / (dynamicCameratracker.smoothTimeX * Mathf.Abs(targetVelocity.x)) * velInterpFacX + 1;
            }

            if (Mathf.Abs(targetVelocity.y) > 1e-3)
            {
                facY = maxDistanceY / (dynamicCameratracker.smoothTimeY * Mathf.Abs(targetVelocity.y)) * velInterpFacY + 1;
            }


            Vector3 targetVelocityCompensated = targetVelocity;
            // compensate the velocity by the factors
            targetVelocityCompensated.x *= facX;
            targetVelocityCompensated.y *= facY;

            // interpolate between the targetVelocity and the compensated one by the influence
            return targetVelocity + (targetVelocityCompensated - targetVelocity) * influence;
        }

        /// <summary>
        /// Lead/lags the target by offseting the position based on the current target velocity. This gives the same lead/lag as the velocity compensated one.
        /// </summary>
        /// <param name="targetPosition">The current target</param>
        /// <param name="targetVelocity">The current velocity</param>
        /// <param name="influence">0 means not affected and 1 means 100% affected with the given parameters</param>
        /// <returns>The new target position</returns>
        public Vector3 LeadLagTargetByPositionCompensation(Vector3 targetPosition, Vector3 targetVelocity, float influence = 1)
        {
            const float vMin = 0;   // minimal velocity threshold

            // copy the max distance as it is changed if we want to clamp it like a circle or ellipse
            float maxDistanceX = leadLagMaxDistanceX;
            float maxDistanceY = leadLagMaxDistanceY;


            // use the targetVelocity as the direction of the ellipse/circle.
            Vector2 velocityDirectionXY = new Vector2(targetVelocity.x, targetVelocity.y).normalized;
            if (!leadLagBoxClamp)
            {
                // limit the max distance for ellipsoidal shapes
                maxDistanceX = Mathf.Abs(velocityDirectionXY.x) * leadLagMaxDistanceX;
                maxDistanceY = Mathf.Abs(velocityDirectionXY.y) * leadLagMaxDistanceY;
            }

            // avoid division by zero
            if ((leadLagMaxAtVelocity - vMin) != 0)
            {
                // Calculates the velocity factor so that we can get smooth transition of the lead lag
                // This interpolates the velocity between zero and max velocity to a range of 0 and 1.
                // The maxDistance is reached when the targetVelocity is over the max velocity treshold
                if (!leadLagBoxClamp)
                {
                    // for ellipsoid / circle clamps we must take the magnitude of the velocity
                    float velInterpFac = Mathf.Clamp((targetVelocity.magnitude - vMin) / (leadLagMaxAtVelocity - vMin), 0, 1);                  
                    velocityDirectionXY *= velInterpFac;
                }
                else
                {
                    // for box clamps we take the abs values per axis of the velocity
                    float velInterpFacX = Mathf.Clamp((Mathf.Abs(targetVelocity.x) - vMin) / (leadLagMaxAtVelocity - vMin), 0, 1);
                    float velInterpFacY = Mathf.Clamp((Mathf.Abs(targetVelocity.y) - vMin) / (leadLagMaxAtVelocity - vMin), 0, 1);
                    velocityDirectionXY.x *= velInterpFacX;
                    velocityDirectionXY.y *= velInterpFacY;
                }
            }

            // set the new target position by offseting the velocity direction by the maxDistanceXY
            return targetPosition + new Vector3(velocityDirectionXY.x * maxDistanceX, velocityDirectionXY.y * maxDistanceY, 0) * influence;
        }

       
        /// <summary>
        /// Compensates the target velocity in such a way that if you are about to collide, it leads the velocity away from the collision.
        /// In other words, the velocity is compensated in such a way that it looks into the future where the new velocity is gonna be at.
        /// The tracker then has to track this new velocity which in effect leads the tracker away from the collision.
        /// </summary>
        /// <param name="target">This is used for the ray origin and the velocity</param>
        /// <returns>Compensated Velocity</returns>
        public Vector3 LookAheadTrackingVelocityCompensation(Rigidbody2D target)
        {
            return LookAheadTrackingVelocityCompensation(target, target.transform.position, target.linearVelocity);
        }


        /// <summary>
        /// Compensates the target velocity in such a way that if you are about to collide, it leads the velocity away from the collision.
        /// In other words, the velocity is compensated in such a way that it looks into the future where the new velocity is gonna be at.
        /// The tracker then has to track this new velocity which in effect leads the tracker away from the collision. 
        /// </summary>
        /// <param name="target">The object that does the raycast</param>
        /// <param name="targetRayCastOrigin">Origin of the ray cast for checking clear paths</param>
        /// <param name="targetVelocity">The target velocity that is going to be compensated (this does not have to be the velocity of the rigidbody)</param>
        /// <returns>Compensated Velocity</returns>
        public Vector3 LookAheadTrackingVelocityCompensation(Rigidbody2D target, Vector2 targetRayCastOrigin, Vector2 targetVelocity)
        {

            if (targetVelocity.sqrMagnitude == 0)
            {
                return targetVelocity;
            }

            // first ray cast
            RaycastHit2D rayHit = Physics2D.Raycast(targetRayCastOrigin, targetVelocity, targetVelocity.magnitude * lookAheadTimeFirst, ~ignoreRayLayerMask);

            if (rayHit.collider != null)
            {
                if (rayHit.collider.gameObject == target.gameObject)
                {
                    return targetVelocity;  // return if hit itself as nothing sensible comes out of it
                }

                const float surfaceOffset = 0.01f;  // surface offset for casting the ray

                Vector2 velNormal = Vector2.Dot(rayHit.normal, targetVelocity) * rayHit.normal;

                float magVelNormal = Mathf.Abs(Vector2.Dot(rayHit.normal, targetVelocity));
                if (magVelNormal == 0)
                {
                    return targetVelocity;  // no solution if the normal projection velocity is 0
                }

                Vector2 dP = rayHit.point - targetRayCastOrigin;

                float magdPNormal = Mathf.Abs(Vector2.Dot(rayHit.normal, dP));

                float firstLookAheadFactor = (magdPNormal / (magVelNormal * lookAheadTimeFirst));   // the look ahead factor is the amount of distance divided by the targetvelocty projected on the normal of the hit multiplied by the look ahead time, this gives a value between 0 and 1

                Vector2 reflectedVel = targetVelocity - 2 * Vector2.Dot(targetVelocity, rayHit.normal) * rayHit.normal;   // reflect the velocity onto the normal of the hit surface

                // second raycast
                RaycastHit2D rayHit2 = Physics2D.Raycast(rayHit.point + rayHit.normal * surfaceOffset, reflectedVel * lookAheadTimeSecond, targetVelocity.magnitude * lookAheadTimeSecond, ~ignoreRayLayerMask);

                // second raycast is only used for howfar we can look into the future, this means if there is no ray hit we have a clear path and the velocity can be compensated
                // if the ray hit something at a short distance, the first look ahead will not affect the new target velocity that much
                float secondLookAheadFactor = 1;
                if (rayHit2.collider != null)
                {
                    secondLookAheadFactor = (rayHit2.point - rayHit.point + rayHit.normal * surfaceOffset).magnitude / (targetVelocity.magnitude * lookAheadTimeSecond);

                }
                targetVelocity = targetVelocity - velNormal * ((1 - firstLookAheadFactor) * secondLookAheadFactor);  // weights the target velocity in such a way that it leads in the velocity based on the two hits

            }

            return targetVelocity;
        }





        /// <summary>
        /// Uses two looks ups for displacement in the effectors. One for the targetPosition and one for the targetPosition extrapolated with the target velocity. 
        /// This gives nice transitions when going in effectors. However the effectors must be large enough to contain both points at the same time, 
        /// else it leads to jumpy behaviour of the target position. It still has the problem that you already get displaced by the effector while the player is not inside an effector yet!
        /// </summary>
        /// <param name="targetPosition">position of the main rigidbody</param>
        /// <param name="targetVelocity">velocity of the target (can be different from the rigibody velocity)</param>
        /// <param name="targetAcceleration">acceleration of the target</param>
        /// <param name="dynamicCameraFunctions"></param>
        /// <param name="displacementOutput"></param>
        /// <returns></returns>
        public Vector3 LeadLagByDoublePositionExperimental(Vector3 targetPosition, ref Vector2 targetVelocity, ref Vector2 targetAcceleration, DynamicCameraFunctions dynamicCameraFunctions, out DCEffectorOutputData displacementOutput)
        {
            // state and displacement at the main target (like the rigidbody)
            Vector2 targetVelocityRB = targetVelocity;
            Vector2 targetAccelRB = targetAcceleration;
            Vector3 targetPositionDisplacementFromRigidbody = dynamicCameraFunctions.DisplaceByEffectors(targetPosition, ref targetVelocityRB, ref targetAccelRB, out DCEffectorOutputData displacementOutputRigidbody);  // we need the displacement for orthographic cameras as the depth is stored in the Z component

            // state and siplacement at the lead/lag position
            Vector2 targetVelocityLead = targetVelocity;
            Vector2 targetAccelerationLead = targetAcceleration;
            Vector3 offsetByLead = dynamicCameraFunctions.LeadLagTargetByPositionCompensation(targetPosition, targetVelocityLead);    // extrapolated camera position
            Vector3 offsetByLeadDisplaced = dynamicCameraFunctions.DisplaceByEffectors(offsetByLead, ref targetVelocityLead, ref targetAccelerationLead, out DCEffectorOutputData displacementOutputLead);  // we need the displacement for orthographic cameras as the depth is stored in the Z component
            Vector3 offsetOnLeadProj = offsetByLeadDisplaced;

            // isect point on the tracking lead/lag direction
            Vector3 offsetPlus90degDisplacement = new Vector3(-displacementOutputLead.displacement.y, displacementOutputLead.displacement.x, 0) + offsetByLeadDisplaced;

            // check if the 90degree rotated displacemetn cuts the line between the position and lead position
            if (LineLineIntersection2D(targetPosition, offsetByLead, offsetByLeadDisplaced, offsetPlus90degDisplacement, out Vector2 isectPoint))
            {
                // check if the isect point is on the leading/lag vector
                Vector2 dP1 = new Vector2(isectPoint.x - targetPosition.x, isectPoint.y - targetPosition.y);
                Vector2 dP2 = new Vector2(isectPoint.x - offsetByLead.x, isectPoint.y - offsetByLead.y);


                // Isect point is on the segment if
                if (Vector2.Dot(dP1, dP2) <= 0)
                {
                    offsetOnLeadProj.x = isectPoint.x;
                    offsetOnLeadProj.y = isectPoint.y;
                }

            }


            if (displacementOutputRigidbody.influence == 0)
            {
                targetPosition = offsetOnLeadProj;
                targetVelocity = targetVelocityLead;
                targetAcceleration = targetAccelerationLead;
                displacementOutput = displacementOutputLead;
            }
            else
            {
                // interpolate between the 2 but the influence of the rb position is leading
                targetPosition = targetPositionDisplacementFromRigidbody + (offsetOnLeadProj - targetPositionDisplacementFromRigidbody) * (1 - displacementOutputRigidbody.influence);
                targetVelocity = targetVelocityRB + (targetVelocityLead - targetVelocityRB) * (1 - displacementOutputRigidbody.influence);
                targetAcceleration = targetAccelRB + (targetAccelerationLead - targetAccelRB) * (1 - displacementOutputRigidbody.influence);

                displacementOutput = displacementOutputRigidbody;
                displacementOutput.displacement = (offsetOnLeadProj - targetPositionDisplacementFromRigidbody) * (1 - displacementOutputRigidbody.influence);
            }

            return targetPosition;
        }

        private bool LineLineIntersection2D(Vector2 P1, Vector2 P2, Vector2 P3, Vector2 P4, out Vector2 isectPoint)
        {
            float D = (P1.x - P2.x) * (P3.y - P4.y) - (P1.y - P2.y) * (P3.x - P4.x);

            if (Mathf.Abs(D) > 1e-5)
            {
                float x = ((P1.x * P2.y - P1.y * P2.x) * (P3.x - P4.x) - (P1.x - P2.x) * (P3.x * P4.y - P3.y * P4.x)) / D;
                float y = ((P1.x * P2.y - P1.y * P2.x) * (P3.y - P4.y) - (P1.y - P2.y) * (P3.x * P4.y - P3.y * P4.x)) / D;

                isectPoint.x = x;
                isectPoint.y = y;
                return true;
            }

            isectPoint.x = 0;
            isectPoint.y = 0;

            return false;
        }
    }
}

