using UnityEngine;

namespace Eveld.DynamicCamera
{
    /// <summary>
    /// This class contains functions for solving a mass spring damper system that has to track up to an second order polynomial. The functions are the solution of the differential equation discretized in deltaT.
    /// </summary>
    public static class MassSpringDamperFunctions
    {
        // Note that the methods might be hard to understand if you don't have experience with differential equations


        const float clampBiasFactor = 0.999f;   // this bias factor prevents the clamping from getting stuck

        /// <summary>
        /// Clamp type of when the solution exceeds specified distance. Note that Circle and Ellipse only work in the XY directions
        /// </summary>
        public enum ClampType
        {
            Box, Circle, Ellipse, Sphere
        }

        /// <summary>
        /// Standard solution to spring damper system that is tracking a second order polynomial. Note that this function does not have frame compensation. It expects all inputs at time t!
        /// </summary>
        /// <param name="currentPos">Current position at time = t</param>
        /// <param name="currentVel">Current velocity at time = t</param>
        /// <param name="targetPos">Target position at time = t</param>
        /// <param name="targetVel">Target velocity at time = t</param>
        /// <param name="targetAccel">Target acceleration at time = t</param>
        /// <param name="smoothTime">Timing value how fast it responds</param>
        /// <param name="deltaTime">delta T</param>
        /// <param name="maxDistance">clamps the current Pos to this distance w.r.t. the target if it exceeds it</param>
        /// <returns></returns>
        public static float CriticalDamped(float currentPos, ref float currentVel, float targetPos, float targetVel, float targetAccel, float smoothTime, float deltaTime, float maxDistance = float.PositiveInfinity)
        {
            // solution based on mass damper spring differential equation with mass = 1 and damping ratio = 1 i.e. critical damped system.
            // this model assumes tracking an target with constant acceleration.
            //float omega = 2 / smoothtime;   // natural frequency
            //float k = omega * omega;        // spring constant
            //float cdamp = 2 * omega;        // critical damping

            float lambda = -2 / smoothTime;                                         // negative equal roots (2x) because D = 0 in quadratic formula

            float C1 = currentPos - targetPos;                                      // constant1 based on initial conditions

            // clamping distance to target
            // target speed * smoothtime = lag distance, but only when targetVel = 0 and targetAccel = 0
            C1 = Mathf.Min(Mathf.Max(-maxDistance, C1), maxDistance);               // clamp is neccesary if maxdistance is specified, this is used to keep the distance from the target and tracker in check

            bool isClamped = (maxDistance - Mathf.Abs(C1) == 0);                    // whether or not we have to re-adjust the initial conditions

            float C2 = currentVel - targetVel - lambda * C1;                        // constant2 based on initial conditions

            float expDT = Mathf.Exp(lambda * deltaTime);                            // Can be optimized but note that this is always an exp of a negative value. (Continued fraction could improve the time) -> simple test show Mathf.EXP is about 4 times slower than a simple taylor expansion

            float partialY = (C1 + C2 * deltaTime) * expDT;

            currentVel = partialY * lambda + C2 * expDT + targetAccel * deltaTime + targetVel;                          // update current velocity

            float newPos = partialY + 0.5f * targetAccel * deltaTime * deltaTime + targetVel * deltaTime + targetPos;   // position for x(t+dt)
            
            float targetFuturePos = targetPos + targetVel * deltaTime + 0.5f * targetAccel * deltaTime * deltaTime;     // update the target dt step (assuming constant acceleration model

            float diff = Mathf.Abs(newPos - targetFuturePos);

            if (isClamped || diff >= maxDistance)    // (isClamped && targetVel != 0) ||  || diff >= maxDistance
            {
                //newPos = targetFuturePos + C1 * clampBiasFactor;            // compensate targetPos back to its "future pos"
                newPos = targetPos + Mathf.Sign(newPos - targetFuturePos) * maxDistance * clampBiasFactor;// C1 * clampBiasFactor;   
                currentVel = 0.5f * targetAccel * deltaTime + targetVel;    // = targetFutureVel, although futureVelocity can be extracted from the current frame, it is only neccesary at this part and its not that important here
            }

            return newPos;  // new position for x(t+dt)
        }


        /// <summary>
        /// Solution to spring damper system that is tracking a second order polynomial. This takes frame compensation of the target into account. 
        /// This means that if we want to track a rigid body for example, the position is already calculated before the currentPos had time to update. 
        /// Note this method does have issues when clamping to a max distance. If you want to clamp distances use CriticalDampedAntiFrameLagType2(...) method
        /// </summary>
        /// <param name="currentPos">Current position at time = t</param>
        /// <param name="currentVel">Current velocity at time = t</param>
        /// <param name="targetPos">Target position at time = t + deltaT</param>
        /// <param name="targetVel">Target velocity at time = t + deltaT</param>
        /// <param name="targetAccel">Target acceleration at time = t + deltaT</param>
        /// <param name="smoothTime">Timing value how fast it responds</param>
        /// <param name="deltaTime">delta T</param>
        /// <param name="maxDistance">clamps the current Pos to this distance w.r.t. the target if it exceeds it</param>
        /// <param name="antiOvershoot">Clamps the tracker to the targetposition if it overshoots the target. This can only happen when the direction of the velocity is the same but the tracker has a greater velocity upon overshooting</param>
        /// <returns></returns>
        public static float CriticalDampedAntiFrameLag(float currentPos, ref float currentVel, float targetPos, float targetVel, float targetAccel, float smoothTime, float deltaTime, float maxDistance = float.PositiveInfinity, bool antiOvershoot = false)
        {
            // Anti frame lag is necessary for limiting the distance in a stable manner.
            // note that the natural frequency = 2 / smoothtime;
            float lambda = -2 / smoothTime;     // negative equal roots (2x) because D = 0 in quadratic formula
            
            float C1 = currentPos - (targetPos + 0.5f * targetAccel * deltaTime * deltaTime - targetVel * deltaTime);    // constant1 based on initial conditions, the sign may seem weird but this is what you get from the differential equation
            // This is where the tracking goes wrong when the velocity is zero. It can't approximate the target position of the previous frame.
            // This is only problematic when we want to clamp the position to a max distance, so if you are not going to clamp the position this works fine.
            // The clamping with no velocity information is solved in the CriticalDampedAntiFrameLagStableClamp(...) method

            
            // clamping distance to target
            // target speed * smoothtime = lag distance for positional tracking         
            C1 = Mathf.Min(Mathf.Max(-maxDistance, C1), maxDistance);               // clamp is neccesary if maxdistance is specified, else it can overshoot like crazy if not clamped and later repositioned
                        
            float C2 = currentVel - (targetVel - targetAccel * deltaTime) - lambda * C1;    // constant2 based on initial conditions

            float expDT = Mathf.Exp(lambda * deltaTime);                            // Can be optimized but note that this is always an exp of a negative value. (Continued fraction could improve the time) -> simple test show Mathf.EXP is about 4 times slower than a simple taylor expansion

            float partialY = (C1 + C2 * deltaTime) * expDT;                         // This is actually the delta position from the future position and the target position

            currentVel = partialY * lambda + C2 * expDT + targetVel;                // update current velocity

            float newPos = partialY + targetPos;   // position for x(t+dt)

            float diff = Mathf.Abs(newPos - targetPos);            
            
            // clamping based on maxDistance
            if ( diff >= maxDistance)
            {
                //newPos = targetPos + C1 * clampBiasFactor;
                newPos = targetPos + Mathf.Sign(newPos - targetPos) * maxDistance * clampBiasFactor;
                //currentVel = targetVel;
                if (targetVel != 0)
                {
                    currentVel = targetVel;
                }
                          
            }
            
            // anti overshoot:
            if (antiOvershoot)
            {
                float dPp = targetPos - currentPos;
                float dPn = targetPos - newPos;
                float dVel = currentVel - targetVel;

                // check if we overshot the target by checking the delta position signs and also do the same for the velocity. 
                // This results in a overshoot clamp only when the velocity of the tracker is in the same direction of the target but it have to be greater than that
                if (dPp * dPn < 0 && dVel * targetVel >= 0)
                {
                    newPos = targetPos;
                    currentVel = targetVel;
                }
            }
           

            return newPos;   // new position for x(t+dt)
        }


        /// <summary>
        /// Solution to spring damper system that is tracking a second order polynomial. This takes frame compensation of the target into account by taking the previous position.
        /// This method fixes distance clamping issues for positional tracking.      
        /// </summary>
        /// <param name="currentPos">Current position at time = t</param>
        /// <param name="currentVel">Current velocity at time = t</param>
        /// <param name="previousTargetPos">Target position at time = t, this is used when targetVel == 0</param>
        /// <param name="targetPos">Target position at time = t + deltaT</param>
        /// <param name="targetVel">Target velocity at time = t + deltaT</param>
        /// <param name="targetAccel">Target acceleration at time = t + deltaT</param>
        /// <param name="smoothTime">Timing value how fast it responds</param>
        /// <param name="deltaTime">delta T</param>
        /// <param name="maxDistance">clamps the current Pos to this distance w.r.t. the target if it exceeds it</param>
        /// <param name="antiOvershoot">Clamps the tracker to the targetposition if it overshoots the target. This can only happen when the direction of the velocity is the same but the tracker has a greater velocity upon overshooting</param>
        /// <returns></returns>
        public static float CriticalDampedAntiFrameLagStableClamp(float currentPos, ref float currentVel, float previousTargetPos, float targetPos, float targetVel, float targetAccel, float smoothTime, float deltaTime, float maxDistance = float.PositiveInfinity, bool antiOvershoot = false)
        {
            // This methods combines the frame lag and the non frame lag solution.
            // This methods fixed the clamping issues for positional clamping and at low frame rates

            // Anti frame lag is necessary for limiting the distance in a stable manner.

            float lambda = -2 / smoothTime;                                         // negative equal roots (2x) because D = 0 in quadratic formula

            float C1;
            if (targetVel == 0)
            {
                C1 = currentPos - previousTargetPos;    // constant1 based on initial conditions
            }
            else
            {
                C1 = currentPos - (targetPos + 0.5f * targetAccel * deltaTime * deltaTime - targetVel * deltaTime); // constant1 based on initial conditions with non zero velocity with frame compensation
                // this can cause a small descrepancy with different frame rates. This could be solved by setting it also to currentPos - previousTargetPos but this makes the effectors not usable as we need to know a previous target position of that effector on the fly
            }

            // clamping distance to target
            // target speed * smoothtime = lag distance
            // maxDistance = 1.5f * smoothTime;                    
            C1 = Mathf.Min(Mathf.Max(-maxDistance, C1), maxDistance);               // clamp is neccesary if maxdistance is specified, else it can overshoot like crazy if not clamped and later repositioned
                        
            float C2 = currentVel - targetVel - lambda * C1;                        // constant2 based on initial conditions

            float expDT = Mathf.Exp(lambda * deltaTime);                            // Can be optimized but note that this is always an exp of a negative value. (Continued fraction could improve the time) -> simple test show Mathf.EXP is about 4 times slower than a simple taylor expansion

            float partialY = (C1 + C2 * deltaTime) * expDT;

            currentVel = partialY * lambda + C2 * expDT + targetAccel * deltaTime + targetVel;                          // update current velocity

            float newPos;
            if (targetVel == 0)
            {
                newPos = partialY + previousTargetPos; // when there is no velocity we have to use the previous target point else the sum is equal will be at targetPos and that will not smooth out the no velocity track mode
            }
            else
            {
                newPos = partialY + targetPos; // position for x(t+dt). This is the improvement wrt CritcalDamped(...) as we know the future position exactly now
            }

            float diff = Mathf.Abs(newPos - targetPos);

            // clamping based on maxDistance
            if (diff >= maxDistance)
            {
                newPos = targetPos + Mathf.Sign(newPos - targetPos) * maxDistance;// * clampBiasFactor;
                
                if (targetVel != 0)
                {
                    currentVel = targetVel;
                }
            }

            // anti overshoot:
            if (antiOvershoot)
            {
                float dPp = targetPos - currentPos;
                float dPn = targetPos - newPos;
                float dVel = currentVel - targetVel;

                // check if we overshot the target by checking the delta position signs and also do the same for the velocity. 
                // This results in a overshoot clamp only when the velocity of the tracker is in the same direction of the target but it have to be greater than that
                if (dPp * dPn < 0 && dVel * targetVel >= 0 )
                {
                    newPos = targetPos;
                    currentVel = targetVel;
                }
            }


            return newPos;   // new position for x(t+dt)
        }


        /// <summary>
        /// Solution based on a spring damper differential equation tracking a second order polynomial for the target. This excepts all data at time = t. Warning: missing correct information about the velocity can give wrong results in combination with clamping to a maxdistance, this can cause a noticable snapping to the clamp distance.
        /// </summary>
        /// <param name="currentPos">current position at time = t</param>
        /// <param name="currentVel">current velocity at time = t</param>
        /// <param name="targetPos">position of the target to track at time = t</param>
        /// <param name="targetVel">velocity of the target to track at time = t(set this to zero if you don't want to track this)</param>
        /// <param name="targetAccel">acceleration of the target to track at time = t(set this to zero if you don't want to track this)</param>
        /// <param name="smoothTime">approximate time it takes to reach the target</param>
        /// <param name="deltaTime">delta time of the frame</param>
        /// <param name="dampingRatio">damping ratio between [0, 1]</param>
        /// <param name="maxDistance">clamps to this max distance w.r.t. the target</param>
        /// <returns>The new position</returns>
        public static float UnderDamped(float currentPos, ref float currentVel, float targetPos, float targetVel, float targetAccel, float smoothTime, float deltaTime, float dampingRatio, float maxDistance = float.PositiveInfinity)
        {
            // this is the solution of a mass spring damper with under damping. This means that the damping ratio is < 1 and sine and cosines are in the solution
            float omega = 2 / smoothTime;
            float k = omega * omega;
            float cdamp = 2 * omega * dampingRatio;

            // always assume mass = 1
            float alpha = -cdamp / 2;
            float beta = Mathf.Sqrt(Mathf.Abs(cdamp * cdamp - 4 * k));

            float C1 = currentPos - targetPos;

            C1 = Mathf.Min(Mathf.Max(-maxDistance, C1), maxDistance);
            bool isClamped = (maxDistance - Mathf.Abs(C1) == 0);     // whether or not we have to readjust the initial conditions

            float C2 = (currentVel - targetVel - alpha * C1) / beta;

            float expDT = Mathf.Exp(alpha * deltaTime);
            float sB = Mathf.Sin(beta * deltaTime);
            float cB = Mathf.Cos(beta * deltaTime);

            float partialY = expDT * (C1 * cB + C2 * sB);

            currentVel = alpha * partialY + beta * expDT * (C2 * cB - C1 * sB) + targetVel + targetAccel * deltaTime;

            float newPos = partialY + 0.5f * targetAccel * deltaTime * deltaTime + targetVel * deltaTime + targetPos;   // position for x(t+dt)
            float targetFuturePos = targetPos + targetVel * deltaTime + 0.5f * targetAccel * deltaTime * deltaTime;     // position for the target the next time step
            float diff = Mathf.Abs(newPos - targetFuturePos);

            if (isClamped || diff >= maxDistance)    // (isClamped && targetVel != 0) ||  || diff >= maxDistance
            {
                //newPos = targetFuturePos + C1 * clampBiasFactor;            // compensate targetPos back to its "future pos"
                newPos = targetPos + Mathf.Sign(newPos - targetFuturePos) * maxDistance * clampBiasFactor;// C1 * clampBiasFactor;   
                currentVel = 0.5f * targetAccel * deltaTime + targetVel;    // = targetFutureVel, although futureVelocity can be extracted from the current frame, it is only neccesary at this part and its not that important here
            }

            return newPos;
        }



        /// <summary>
        /// Solution based on a spring damper differential equation tracking a second order polynomial for the target, with frame compensation for the target! This excepts all data at time = t.
        /// Warning: missing correct information about the velocity can give wrong results in combination with clamping to a maxdistance, 
        /// this can cause a noticable snapping to the clamp distance.
        /// </summary>
        /// <param name="currentPos">current position at time = t</param>
        /// <param name="currentVel">current velocity at time = t</param>
        /// <param name="targetPos">position of the target to track at time = t + deltaT</param>
        /// <param name="targetVel">velocity of the target to track at time = t + deltaT (set this to zero if you don't want to track this)</param>
        /// <param name="targetAccel">acceleration of the target to track at time = t + deltaT (set this to zero if you don't want to track this)</param>
        /// <param name="smoothTime">approximate time it takes to reach the target</param>
        /// <param name="deltaTime">delta time of the frame</param>
        /// <param name="dampingRatio">damping ratio between [0, 1]</param>
        /// <param name="maxDistance">clamps to this max distance w.r.t. the target</param>
        /// <returns></returns>
        public static float UnderDampedAntiFrameLag(float currentPos, ref float currentVel, float targetPos, float targetVel, float targetAccel, float smoothTime, float deltaTime, float dampingRatio, float maxDistance = float.PositiveInfinity)
        {
            // this is the solution of a mass spring damper with under damping. This means that the damping ratio is < 1 and sine and cosines are in the solution
            float omega = 2 / smoothTime;
            float k = omega * omega;
            float cdamp = 2 * omega * dampingRatio;

            // always assume mass = 1
            float alpha = -cdamp / 2;                                   // real part
            float beta = Mathf.Sqrt(Mathf.Abs(cdamp * cdamp - 4 * k));  // imaginary part

            float C1 = currentPos - (targetPos + 0.5f * targetAccel * deltaTime * deltaTime - targetVel * deltaTime);

            C1 = Mathf.Min(Mathf.Max(-maxDistance, C1), maxDistance);
            bool isClamped = (maxDistance - Mathf.Abs(C1) == 0);     // whether or not we have to readjust the initial conditions

            float C2 = (currentVel - (targetVel - targetAccel * deltaTime) - alpha * C1) / beta;

            float expDT = Mathf.Exp(alpha * deltaTime);
            float sB = Mathf.Sin(beta * deltaTime);
            float cB = Mathf.Cos(beta * deltaTime);

            float partialY = expDT * (C1 * cB + C2 * sB);

            currentVel = alpha * partialY + beta * expDT * (C2 * cB - C1 * sB) + targetVel;

            float newPos = partialY + targetPos;   // position for x(t+dt)
            float diff = Mathf.Abs(newPos - targetPos);

            if ((isClamped) || diff >= maxDistance)    // (isClamped && targetVel != 0) ||  || diff >= maxDistance
            {
                newPos = targetPos + Mathf.Sign(newPos - targetPos) * maxDistance * clampBiasFactor;// C1 * clampBiasFactor;                
                currentVel = targetVel;
            }

            return newPos;
        }





        /// <summary>
        /// Standard solution to spring damper system that is tracking a second order polynomial. Note that this function does not have frame compensation. It expects all inputs at time t!
        /// </summary>
        /// <param name="currentPos">Current position at time = t</param>
        /// <param name="currentVel">Current velocity at time = t</param>
        /// <param name="targetPos">Target position at time = t</param>
        /// <param name="targetVel">Target velocity at time = t</param>
        /// <param name="targetAccel">Target acceleration at time = t</param>
        /// <param name="smoothTime">Timing value how fast it responds</param>
        /// <param name="deltaTime">delta T</param>
        /// <param name="maxDistance">clamps the current Pos to this distance w.r.t. the target if it exceeds it</param>
        /// <param name="clampType"></param>
        /// <returns></returns>
        public static Vector3 CriticalDamped3D(Vector3 currentPos, ref Vector3 currentVel, Vector3 targetPos, Vector3 targetVel, Vector3 targetAccel, Vector3 smoothTime, float deltaTime, Vector3 maxDistance, ClampType clampType = ClampType.Box)
        {
            Vector3 compensatedMaxDistance = ClampMaxDistancesByType(currentPos, targetPos, maxDistance, clampType);
            Vector3 newPos;
            newPos.x = CriticalDamped(currentPos.x, ref currentVel.x, targetPos.x, targetVel.x, targetAccel.x, smoothTime.x, deltaTime, compensatedMaxDistance.x);
            newPos.y = CriticalDamped(currentPos.y, ref currentVel.y, targetPos.y, targetVel.y, targetAccel.y, smoothTime.y, deltaTime, compensatedMaxDistance.y);
            newPos.z = CriticalDamped(currentPos.z, ref currentVel.z, targetPos.z, targetVel.z, targetAccel.z, smoothTime.z, deltaTime, compensatedMaxDistance.z);

            return newPos;
        }


        /// <summary>
        /// Solution to spring damper system that is tracking a second order polynomial. This takes frame compensation of the target into account. This means that if we want to track a rigid body for example, the position is already calculated before the currentPos had time to update. This function compensates for that fact.
        /// </summary>
        /// <param name="currentPos">Current position at time = t</param>
        /// <param name="currentVel">Current velocity at time = t</param>
        /// <param name="targetPos">Target position at time = t + deltaT</param>
        /// <param name="targetVel">Target velocity at time = t + deltaT</param>
        /// <param name="targetAccel">Target acceleration at time = t + deltaT</param>
        /// <param name="smoothTime">Timing value how fast it responds</param>
        /// <param name="deltaTime">delta T</param>
        /// <param name="maxDistance">Clamps the current Pos to this distance w.r.t. the target if it exceeds it</param>
        /// <param name="clampType">How to clamp the distance when clamped</param>
        /// <param name="antiOvershootX">Anti-overshoot on the x-axis</param>
        /// <param name="antiOvershootY">Anti-overshoot on the y-axis</param>
        /// <param name="antiOvershootZ">Anti-overshoot on the z-axis</param>
        /// <returns></returns>
        public static Vector3 CriticalDampedAntiFrameLag3D(Vector3 currentPos, ref Vector3 currentVel, Vector3 targetPos, Vector3 targetVel, Vector3 targetAccel, Vector3 smoothTime, float deltaTime, Vector3 maxDistance, ClampType clampType = ClampType.Box, bool antiOvershootX = false, bool antiOvershootY = false, bool antiOvershootZ = false)
        {
            // this is an important concept to grasp: assume we use a late update for setting currentPos. This means that targetPos is already set for this frame, however currentPos assumes the targetPos of the previous frame.
            // Therefore targetPos must go back to its position of the previous frame          
            Vector3 compensatedTargetPos = targetPos - targetVel * deltaTime + 0.5f * targetAccel * deltaTime * deltaTime;  // adding acceleration might look weird, but it comes from the differential equation solution

            Vector3 compensatedMaxDistance = ClampMaxDistancesByType(currentPos, compensatedTargetPos, maxDistance, clampType);

            Vector3 newPos;
            newPos.x = CriticalDampedAntiFrameLag(currentPos.x, ref currentVel.x, targetPos.x, targetVel.x, targetAccel.x, smoothTime.x, deltaTime, compensatedMaxDistance.x, antiOvershootX);
            newPos.y = CriticalDampedAntiFrameLag(currentPos.y, ref currentVel.y, targetPos.y, targetVel.y, targetAccel.y, smoothTime.y, deltaTime, compensatedMaxDistance.y, antiOvershootY);
            newPos.z = CriticalDampedAntiFrameLag(currentPos.z, ref currentVel.z, targetPos.z, targetVel.z, targetAccel.z, smoothTime.z, deltaTime, compensatedMaxDistance.z, antiOvershootZ);

            return newPos;
        }


        /// <summary>
        /// Solution to spring damper system that is tracking a second order polynomial. This takes frame compensation of the target into account. This means that if we want to track a rigid body for example, the position is already calculated before the currentPos had time to update. This function compensates for that fact.
        /// </summary>
        /// <param name="currentPos">Current position at time = t</param>
        /// <param name="currentVel">Current velocity at time = t</param>
        /// <param name="previousTargetPos">Target position at time = t</param>
        /// <param name="targetPos">Target position at time = t + deltaT</param>
        /// <param name="targetVel">Target velocity at time = t + deltaT</param>
        /// <param name="targetAccel">Target acceleration at time = t + deltaT</param>
        /// <param name="smoothTime">Timing value how fast it responds</param>
        /// <param name="deltaTime">delta T</param>
        /// <param name="maxDistance">Clamps the current Pos to this distance w.r.t. the target if it exceeds it</param>
        /// <param name="clampType">How to clamp the distance when clamped</param>
        /// <param name="antiOvershootX">Anti-overshoot on the x-axis</param>
        /// <param name="antiOvershootY">Anti-overshoot on the y-axis</param>
        /// <param name="antiOvershootZ">Anti-overshoot on the z-axis</param>
        /// <returns></returns>
        public static Vector3 CriticalDampedAntiFrameLagStableClamp3D(Vector3 currentPos, ref Vector3 currentVel, Vector3 previousTargetPos, Vector3 targetPos, Vector3 targetVel, Vector3 targetAccel, Vector3 smoothTime, float deltaTime, Vector3 maxDistance, ClampType clampType = ClampType.Box, bool antiOvershootX = false, bool antiOvershootY = false, bool antiOvershootZ = false)
        {
            // this is an important concept to grasp: assume we use a late update for setting currentPos. This means that targetPos is already set for this frame, however currentPos assumes the targetPos of the previous frame.
            // Therefore targetPos must go back to its position of the previous frame          
            Vector3 compensatedTargetPos = targetPos - targetVel * deltaTime + 0.5f * targetAccel * deltaTime * deltaTime; // targetPos - targetVel * deltaTime + 0.5f * targetAccel * deltaTime * deltaTime;  // adding acceleration might look weird, but it comes from the differential equation solution

            Vector3 compensatedMaxDistance = ClampMaxDistancesByType(currentPos, compensatedTargetPos, maxDistance, clampType);

            Vector3 newPos;
            newPos.x = CriticalDampedAntiFrameLagStableClamp(currentPos.x, ref currentVel.x, previousTargetPos.x, targetPos.x, targetVel.x, targetAccel.x, smoothTime.x, deltaTime, compensatedMaxDistance.x, antiOvershootX);
            newPos.y = CriticalDampedAntiFrameLagStableClamp(currentPos.y, ref currentVel.y, previousTargetPos.y, targetPos.y, targetVel.y, targetAccel.y, smoothTime.y, deltaTime, compensatedMaxDistance.y, antiOvershootY);
            newPos.z = CriticalDampedAntiFrameLagStableClamp(currentPos.z, ref currentVel.z, previousTargetPos.z, targetPos.z, targetVel.z, targetAccel.z, smoothTime.z, deltaTime, compensatedMaxDistance.z, antiOvershootZ);

            return newPos;
        }

        /// <summary>
        /// Solution based on a spring damper differential equation tracking a second order polynomial for the target. This excepts all data at time = t. Warning: missing correct information about the velocity can give wrong results in combination with clamping to a maxdistance, this can cause a noticable snapping to the clamp distance.
        /// </summary>
        /// <param name="currentPos">current position at time = t</param>
        /// <param name="currentVel">current velocity at time = t</param>
        /// <param name="targetPos">position of the target to track at time = t</param>
        /// <param name="targetVel">velocity of the target to track at time = t(set this to zero if you don't want to track this)</param>
        /// <param name="targetAccel">acceleration of the target to track at time = t(set this to zero if you don't want to track this)</param>
        /// <param name="smoothTime">approximate time it takes to reach the target</param>
        /// <param name="deltaTime">delta time of the frame</param>
        /// <param name="dampingRatio">damping ratio between [0, 1]</param>
        /// <param name="maxDistance">clamps to this max distance w.r.t. the target</param>
        /// <param name="clampType"></param>
        /// <returns></returns>
        public static Vector3 UnderDamped3D(Vector3 currentPos, ref Vector3 currentVel, Vector3 targetPos, Vector3 targetVel, Vector3 targetAccel, Vector3 smoothTime, float deltaTime, Vector3 dampingRatio, Vector3 maxDistance, ClampType clampType = ClampType.Box)
        {
            Vector3 compensatedMaxDistance = ClampMaxDistancesByType(currentPos, targetPos, maxDistance, clampType);
            Vector3 newPos;
            newPos.x = UnderDamped(currentPos.x, ref currentVel.x, targetPos.x, targetVel.x, targetAccel.x, smoothTime.x, deltaTime, dampingRatio.x, compensatedMaxDistance.x);
            newPos.y = UnderDamped(currentPos.y, ref currentVel.y, targetPos.y, targetVel.y, targetAccel.y, smoothTime.y, deltaTime, dampingRatio.y, compensatedMaxDistance.y);
            newPos.z = UnderDamped(currentPos.z, ref currentVel.z, targetPos.z, targetVel.z, targetAccel.z, smoothTime.z, deltaTime, dampingRatio.z, compensatedMaxDistance.z);

            return newPos;
        }


        /// <summary>
        /// Solution based on a spring damper differential equation tracking a second order polynomial for the target, with frame compensation for the target! This excepts all data at time = t. Warning: missing correct information about the velocity can give wrong results in combination with clamping to a maxdistance, this can cause a noticable snapping to the clamp distance.
        /// </summary>
        /// <param name="currentPos">current position at time = t</param>
        /// <param name="currentVel">current velocity at time = t</param>
        /// <param name="targetPos">position of the target to track at time = t + deltaT</param>
        /// <param name="targetVel">velocity of the target to track at time = t + deltaT (set this to zero if you don't want to track this)</param>
        /// <param name="targetAccel">acceleration of the target to track at time = t + deltaT (set this to zero if you don't want to track this)</param>
        /// <param name="smoothTime">approximate time it takes to reach the target</param>
        /// <param name="deltaTime">delta time of the frame</param>
        /// <param name="dampingRatio">damping ratio between [0, 1]</param>
        /// <param name="maxDistance">clamps to this max distance w.r.t. the target</param>
        /// <param name="clampType"></param>
        /// <returns></returns>
        public static Vector3 UnderDampedAntiFrameLag3D(Vector3 currentPos, ref Vector3 currentVel, Vector3 targetPos, Vector3 targetVel, Vector3 targetAccel, Vector3 smoothTime, float deltaTime, Vector3 dampingRatio, Vector3 maxDistance, ClampType clampType = ClampType.Box)
        {
            Vector3 compensatedTargetPos = targetPos - targetVel * deltaTime + 0.5f * targetAccel * deltaTime * deltaTime;
            Vector3 compensatedMaxDistance = ClampMaxDistancesByType(currentPos, compensatedTargetPos, maxDistance, clampType);
            Vector3 newPos;
            newPos.x = UnderDampedAntiFrameLag(currentPos.x, ref currentVel.x, targetPos.x, targetVel.x, targetAccel.x, smoothTime.x, deltaTime, dampingRatio.x, compensatedMaxDistance.x);
            newPos.y = UnderDampedAntiFrameLag(currentPos.y, ref currentVel.y, targetPos.y, targetVel.y, targetAccel.y, smoothTime.y, deltaTime, dampingRatio.y, compensatedMaxDistance.y);
            newPos.z = UnderDampedAntiFrameLag(currentPos.z, ref currentVel.z, targetPos.z, targetVel.z, targetAccel.z, smoothTime.z, deltaTime, dampingRatio.z, compensatedMaxDistance.z);

            return newPos;
        }


        /// <summary>
        /// Clamps the max distance the tracker can have to the target pos. The max distance vector is clamped according to a Box (default), Circle, Ellipse or Sphere.
        /// </summary>
        /// <param name="currentPos"></param>
        /// <param name="targetPos"></param>
        /// <param name="maxDistance"></param>
        /// <param name="clampType"></param>
        /// <returns></returns>
        private static Vector3 ClampMaxDistancesByType(Vector3 currentPos, Vector3 targetPos, Vector3 maxDistance, ClampType clampType)
        {
            // determine individual max range components
            Vector3 dP = targetPos - currentPos;        // difference of target and currentPos
            Vector2 dPXY = new Vector2(dP.x, dP.y);     // 2D version of DP we need to have the compensated targetpos here for a proper direction
            Vector3 compensatedMaxDistance = new Vector3(maxDistance.x, maxDistance.y, maxDistance.z);  // this is the box clamp type by default

            // Circle clamp
            if (clampType == ClampType.Circle)
            {
                if (dPXY.sqrMagnitude > compensatedMaxDistance.x * compensatedMaxDistance.x)
                {
                    Vector2 temp = dPXY.normalized * compensatedMaxDistance.x;
                    compensatedMaxDistance.x = Mathf.Abs(temp.x);
                    compensatedMaxDistance.y = Mathf.Abs(temp.y);
                }
            }
            else if (clampType == ClampType.Ellipse)
            {
                // Ellipse clamp
                float xSqr = (maxDistance.x * maxDistance.x);
                float ySqr = (maxDistance.y * maxDistance.y);

                float t = (xSqr == 0 || ySqr == 0) ? 0 : Mathf.Sqrt(1 / (dPXY.x * dPXY.x / xSqr + dPXY.y * dPXY.y / ySqr));

                Vector3 dPprojectedEllipse = (dPXY * t);

                if (dPprojectedEllipse.sqrMagnitude < dPXY.sqrMagnitude)
                {
                    compensatedMaxDistance.x = Mathf.Abs(dPprojectedEllipse.x);
                    compensatedMaxDistance.y = Mathf.Abs(dPprojectedEllipse.y);
                }
            }
            else if (clampType == ClampType.Sphere)
            {
                // Sphere clamp
                if (dP.sqrMagnitude > compensatedMaxDistance.x * compensatedMaxDistance.x)
                {
                    Vector3 temp = dP.normalized * compensatedMaxDistance.x;
                    compensatedMaxDistance.x = Mathf.Abs(temp.x);
                    compensatedMaxDistance.y = Mathf.Abs(temp.y);
                    compensatedMaxDistance.z = Mathf.Abs(temp.z);
                }

            }

            return compensatedMaxDistance;
        }

    }





}

