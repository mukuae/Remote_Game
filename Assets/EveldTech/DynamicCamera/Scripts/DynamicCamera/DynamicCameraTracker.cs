using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera
{
    /// <summary>
    /// This class keeps track of the internal state of the tracker such as position and velocity and its properties. The tracker its state can be updated by using a critical damped update or under damped update step.
    /// Note that the functions in this class are all frame compensated.
    /// </summary>
    [System.Serializable]
    public class DynamicCameraTracker
    {
        [Range(0.01f, 5)]
        public float smoothTimeX = 0.15f;   // [seconds], smooth time is roughly the time it takes to converge to the target. (The natural frequency is 2/smoothTime)
        [Range(0.01f, 5)]
        public float smoothTimeY = 0.15f;   // [seconds]
        [Range(0.01f, 5)]
        public float smoothTimeZ = 0.15f;   // [seconds]

        [Range(0.00f, 0.99f)]
        public float dampingRatioX = 0.5f;  // damping ratio always have to be between 0 and 1
        [Range(0.00f, 0.99f)]
        public float dampingRatioY = 0.5f;
        [Range(0.00f, 0.99f)]
        public float dampingRatioZ = 0.5f;

        [Range(0, 1000)]
        public float maxFollowDistanceX = 100f; // [meter], clamping the distance is usefull for limiting the distance between the tracker and target. However it can cause snappy behaviour inside effectors.
        [Range(0, 1000)]
        public float maxFollowDistanceY = 100f; // [meter]
        [Range(0, 1000)]
        public float maxFollowDistanceZ = 100f; // [meter]

        // anit overshoot options for X/Y-axis. Not allowing to overshoot can cause a snappy feel to the camera
        public bool antiOvershootX = false;     // prevents overshooting the target in the X axis, it only limits the overshoot when the tracker overshoots the target and the velocity is greater than the target.
        public bool antiOvershootY = false;

        [Range(0, 10000)]
        public float accelerationThresholdX = 100f; // [meter / seconds^2], this limits the maximum acceleration that we can track
                                                    // note that a collision or a rapid change in velocity can be a huge acceleration spike
        [Range(0, 10000)]
        public float accelerationThresholdY = 100f; // same for the Y axis

        public bool linkThresholdXtoY = false;                  // link threshold Y with X
        public bool accelerationOverThresholdIsZero = false;    // zeros the acceleration if the threshold is exceeded


        /// <summary>
        /// Position of the tracker that moves towards the target
        /// </summary>
        [HideInInspector]
        public Vector3 trackerPosition = Vector3.zero;

        /// <summary>
        /// Velocity of the tracker that moves towards the target
        /// </summary>
        [HideInInspector]
        public Vector3 trackerVelocity = Vector3.zero;

        /// <summary>
        /// Keeps track of the previous target position after an update step is called, this is important for the AntiFrameLagStableClamp method
        /// </summary>
        private Vector3 previousTargetPosition = Vector3.zero;


        public DynamicCameraTracker()
        {
            trackerPosition = Vector3.zero;
            trackerVelocity = Vector3.zero;
        }

        public DynamicCameraTracker(Vector3 trackerPosition, Vector3 trackerVelocity)
        {
            SetInitialConditions(trackerPosition, trackerVelocity);
        }

        /// <summary>
        /// Sets the current internal tracker position and velocity to given values.
        /// </summary>
        /// <param name="trackerPosition">New position</param>
        /// <param name="trackerVelocity">New velocity</param>
        public void SetInitialConditions(Vector3 trackerPosition, Vector3 trackerVelocity)
        {
            this.trackerPosition = trackerPosition;
            this.trackerVelocity = trackerVelocity;
        }

        /// <summary>
        /// Sets the internal tracker position to the new position.
        /// </summary>
        /// <param name="newPosition"></param>
        public void SetTrackerPosition(Vector3 newPosition)
        {
            trackerPosition = newPosition;
        }

        /// <summary>
        /// Sets the internal tracker velocity to the new velocity
        /// </summary>
        /// <param name="newVelocity"></param>
        public void SetTrackerVelocity(Vector3 newVelocity)
        {
            trackerVelocity = newVelocity;
        }

        /// <summary>
        /// Assign the position of the target at the previous frame, or assign a previous position of a new target
        /// </summary>
        /// <param name="previousPos"></param>
        public void SetPreviousTargetPosition(Vector3 previousPos)
        {
            previousTargetPosition = previousPos;
        }


        private Vector3 LimitAcceleration(Vector3 acceleration)
        {
            if (linkThresholdXtoY)
            {
                if (acceleration.sqrMagnitude > accelerationThresholdX * accelerationThresholdX)
                {
                    return (accelerationOverThresholdIsZero) ? Vector3.zero : acceleration.normalized * accelerationThresholdX;
                }
                
            }
            else
            {
                
                if (accelerationOverThresholdIsZero)
                {
                    // set to zero if exceeded threshold
                    acceleration.x = (Mathf.Abs(acceleration.x) > Mathf.Abs(accelerationThresholdX)) ? 0 : acceleration.x;
                    acceleration.y = (Mathf.Abs(acceleration.y) > Mathf.Abs(accelerationThresholdY)) ? 0 : acceleration.y;
                }
                else
                {
                    // clamp x and y individually
                    acceleration.x = Mathf.Clamp(acceleration.x, -accelerationThresholdX, accelerationThresholdX);
                    acceleration.y = Mathf.Clamp(acceleration.y, -accelerationThresholdY, accelerationThresholdY);
                }
                
            }
            
            return acceleration;
            
        }


        public Vector3 CritcalDampedStepNoFrameCompensation(Vector3 targetPos, Vector3 targetVel, Vector3 targetAccel, float deltaTime)
        {
            targetAccel = LimitAcceleration(targetAccel);
            trackerPosition = MassSpringDamperFunctions.CriticalDamped3D(trackerPosition, ref trackerVelocity, targetPos, targetVel, targetAccel, new Vector3(smoothTimeX, smoothTimeY, smoothTimeZ), deltaTime, new Vector3(maxFollowDistanceX, maxFollowDistanceY, maxFollowDistanceZ));
            return trackerPosition;
        }


        /// <summary>
        /// Updates the tracker position for the time step by using a critical damped spring damper. This uses the frame lag compensation for the target.
        /// </summary>
        /// <param name="targetPos">target position at Time = t + deltaT</param>
        /// <param name="targetVel">target velocity at Time = t</param>
        /// <param name="deltaTime"></param>
        /// <param name="clampType"></param>
        /// <returns></returns>
        public Vector3 CriticalDampedStep(Vector3 targetPos, Vector3 targetVel, float deltaTime, MassSpringDamperFunctions.ClampType clampType = MassSpringDamperFunctions.ClampType.Box)
        {
            return CriticalDampedStep(targetPos, targetVel, Vector3.zero, deltaTime, clampType);
        }

        /// <summary>
        /// Updates the tracker position for the time step by using a critical damped spring damper. This uses the frame lag compensation for the target.
        /// </summary>
        /// <param name="targetPos">target position at Time = t + deltaT</param>
        /// <param name="targetVel">target velocity at Time = t or t + deltaT</param>
        /// <param name="targetAccel">target acceleration at Time = t or t + deltaT</param>
        /// <param name="deltaTime"></param>
        /// <param name="clampType"></param>
        /// <returns></returns>
        public Vector3 CriticalDampedStep(Vector3 targetPos, Vector3 targetVel, Vector3 targetAccel, float deltaTime, MassSpringDamperFunctions.ClampType clampType = MassSpringDamperFunctions.ClampType.Circle)
        {
            targetAccel = LimitAcceleration(targetAccel);
            Vector3 newPos = MassSpringDamperFunctions.CriticalDampedAntiFrameLag3D(trackerPosition, ref trackerVelocity, targetPos, targetVel, targetAccel, new Vector3(smoothTimeX, smoothTimeY, smoothTimeZ), deltaTime, new Vector3(maxFollowDistanceX, maxFollowDistanceY, maxFollowDistanceZ), clampType, antiOvershootX, antiOvershootY, false);
            trackerPosition = newPos;
            return trackerPosition;
        }


        /// <summary>
        /// Updates the tracker position for the time step by using a critical damped spring damper. 
        /// This uses the frame lag compensation for the target and works properly when clamping the distance of the tracker!
        /// </summary>
        /// <param name="targetPos">target position at Time = t + deltaT</param>
        /// <param name="targetVel">target velocity at Time = t or t + deltaT</param>
        /// <param name="targetAccel">target acceleration at Time = t or t + deltaT</param>
        /// <param name="deltaTime"></param>
        /// <param name="clampType"></param>
        /// <returns></returns>
        public Vector3 CriticalDampedStableClampStep(Vector3 targetPos, Vector3 targetVel, Vector3 targetAccel, float deltaTime, MassSpringDamperFunctions.ClampType clampType = MassSpringDamperFunctions.ClampType.Circle)
        {
            targetAccel = LimitAcceleration(targetAccel);
            Vector3 newPos = MassSpringDamperFunctions.CriticalDampedAntiFrameLagStableClamp3D(trackerPosition, ref trackerVelocity, previousTargetPosition, targetPos, targetVel, targetAccel, new Vector3(smoothTimeX, smoothTimeY, smoothTimeZ), deltaTime, new Vector3(maxFollowDistanceX, maxFollowDistanceY, maxFollowDistanceZ), clampType, antiOvershootX, antiOvershootY, false);
            trackerPosition = newPos;
            previousTargetPosition = targetPos; // need to record the previous position for the stable clamping method
            return trackerPosition;
        }

        /// <summary>
        /// Updates the tracker position for the time step by using an under damped spring damper. This uses the frame lag compensation for the target.
        /// </summary>
        /// <param name="targetPos">target position at Time = t + deltaT</param>
        /// <param name="targetVel">target velocity at Time = t or t + deltaT</param>
        /// <param name="deltaTime"></param>
        /// <param name="clampType"></param>
        /// <returns></returns>
        public Vector3 UnderDampedStep(Vector3 targetPos, Vector3 targetVel, float deltaTime, MassSpringDamperFunctions.ClampType clampType = MassSpringDamperFunctions.ClampType.Box)
        {
            return UnderDampedStep(targetPos, targetVel, Vector3.zero, deltaTime, clampType);
        }

        /// <summary>
        /// Updates the tracker position for the time step by using an under damped spring damper. This uses the frame lag compensation for the target.
        /// </summary>
        /// <param name="targetPos">target position at Time = t + deltaT</param>
        /// <param name="targetVel">target velocity at Time = t or t + deltaT</param>
        /// <param name="targetAccel">target acceleration at Time = t or t + deltaT</param>
        /// <param name="deltaTime"></param>
        /// <param name="clampType"></param>
        /// <returns></returns>
        public Vector3 UnderDampedStep(Vector3 targetPos, Vector3 targetVel, Vector3 targetAccel, float deltaTime, MassSpringDamperFunctions.ClampType clampType = MassSpringDamperFunctions.ClampType.Box)
        {
            targetAccel = LimitAcceleration(targetAccel);
            Vector3 newPos = MassSpringDamperFunctions.UnderDampedAntiFrameLag3D(trackerPosition, ref trackerVelocity, targetPos, targetVel, targetAccel, new Vector3(smoothTimeX, smoothTimeY, smoothTimeZ), deltaTime, new Vector3(dampingRatioX, dampingRatioY, dampingRatioZ), new Vector3(maxFollowDistanceX, maxFollowDistanceY, maxFollowDistanceZ), clampType);
            trackerPosition = newPos;
            return trackerPosition;
        }


    }
}