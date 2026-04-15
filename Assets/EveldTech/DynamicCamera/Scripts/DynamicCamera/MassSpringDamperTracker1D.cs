using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera
{

    /// <summary>
    /// This class keeps track of the internal state of a 1D "mass (= 1)" such as position and velocity and its properties. The "mass" its state can be updated by using a critical damped update or under damped update step. 
    /// </summary>
    [System.Serializable]
    public class MassSpringDamperTracker1D
    {        

        [Range(0.01f, 5)]
        public float smoothTime = 0.15f;

        [Range(0.00f, 0.99f)]
        public float dampingRatio = 0.5f;

        [Range(0, 1000)]
        public float maxFollowDistance = 100f;

        /// <summary>
        /// position of the "mass" that moves towards the target
        /// </summary>
        [HideInInspector]
        public float position = 0;

        /// <summary>
        /// velocity of the "mass" that moves towards the target
        /// </summary>
        [HideInInspector]
        public float velocity = 0;

        public MassSpringDamperTracker1D()
        {
            position = 0;
            velocity = 0;
        }

        public MassSpringDamperTracker1D(float position, float velocity)
        {
            SetInitialConditions(position, velocity);
        }

        public void SetInitialConditions(float position, float velocity)
        {
            this.position = position;
            this.velocity = velocity;
        }

        public void SetTrackerPosition(float newPosition)
        {
            position = newPosition;
        }

        public void SetTrackerVelocity(float newVelocity)
        {
            velocity = newVelocity;
        }

        /// <summary>
        /// A 1D delta t step update of the critical damped system.
        /// </summary>
        /// <param name="targetPos"></param>
        /// <param name="targetVel"></param>
        /// <param name="deltaTime"></param>
        /// <param name="antiFrameLag">Set true if the target position is already updated this frame.</param>
        /// <returns></returns>
        public float CriticalDampedStep(float targetPos, float targetVel, float deltaTime, bool antiFrameLag)
        {
            if (antiFrameLag)
            {
                position = MassSpringDamperFunctions.CriticalDampedAntiFrameLag(position, ref velocity, targetPos, targetVel, 0, smoothTime, deltaTime, maxFollowDistance);
            }
            else
            {
                position = MassSpringDamperFunctions.CriticalDamped(position, ref velocity, targetPos, targetVel, 0, smoothTime, deltaTime, maxFollowDistance);
            }
            
            return position;
        }

        /// <summary>
        /// A 1D delta t step update of the under damped system.
        /// </summary>
        /// <param name="targetPos"></param>
        /// <param name="targetVel"></param>
        /// <param name="deltaTime"></param>
        /// <param name="antiFrameLag">Set true if the target position is already updated this frame.</param>
        /// <returns></returns>
        public float UnderDampedStep(float targetPos, float targetVel, float deltaTime, bool antiFrameLag)
        {
            if (antiFrameLag)
            {
                position = MassSpringDamperFunctions.UnderDampedAntiFrameLag(position, ref velocity, targetPos, targetVel, 0, smoothTime, deltaTime, dampingRatio, maxFollowDistance);
            }
            else
            {
                position = MassSpringDamperFunctions.UnderDamped(position, ref velocity, targetPos, targetVel, 0, smoothTime, deltaTime, dampingRatio, maxFollowDistance);
            }

            return position;
        }


    }
}
