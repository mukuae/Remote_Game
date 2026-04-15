using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Eveld.DynamicCamera
{
    /// <summary>
    /// Example of a minimalistic dynamic camera controller that still uses the effectors.
    /// </summary>
    public class DynamicCameraControllerMinimal : MonoBehaviour
    {
        public DynamicCameraTracker cameraTracker;              // takes care of smoothing out the montion of the camera towards the target
        public MassSpringDamperTracker1D orthoSizeSmoother;     // takes care of smoothing the orthograpic size to a new value
        public DynamicCameraFunctions dynamicCameraFunctions;   // contains specialized functions
        public Rigidbody2D targetRigidbody;                     // rigidbody2D as target we want to track
        public Transform cameraRig;                             // transform that is the camera or is the parent of the camera. It is better to have the camera parented to the rig as this allows for the camera shake effect.
        public Camera currentCamera;                            // camera attached to the rig

        public Vector2 cameraTargetoffset;                      // offset added to the final position to shift the camera
        public float cameraRigPositionOffsetZ = 0;              // z position of the camera at initialization
        
        private Vector2 targetAcceleration = Vector2.zero;       // We need to calculate the acceleration as deltaV / deltaT
        private Vector2 previousTargetVelocity = Vector2.zero;   // We also need to keep track of the previous velocity to calculate the acceleration
    
        // Initialize camera and camera offsets
        void Start()
        {
            cameraRigPositionOffsetZ = cameraRig.position.z;
            cameraTracker.SetInitialConditions(cameraRig.position, Vector3.zero);
        }

        private void FixedUpdate()
        {
            // calculate acceleration like this (must be in fixed update):
            targetAcceleration = (targetRigidbody.linearVelocity - previousTargetVelocity) / Time.fixedDeltaTime;   // calculates the acceleration
            previousTargetVelocity = targetRigidbody.linearVelocity;

            // we can ommit acceleration if it is not neccesary.
        }


        // It is advised to use the LateUpdate() for the camera tracker update for best results. Note that it works with Update() properly only if the tracker target is a rigidbody2D with interpolation enabled (which should be also for LateUpdate()).
        void LateUpdate()
        {

            if (cameraRig != null)
            {

                Vector3 targetPosition = targetRigidbody.transform.position; // dont use Rigidbody2D.position!
                Vector2 targetVelocity = dynamicCameraFunctions.LookAheadTrackingVelocityCompensation(targetRigidbody); // this has to be called before the effector displacement


                // Lead/Lag (type 1) by compensating the velocity. This version of calculating lead lag is insensitive to the target position so this can be done before the effector displacement. However it can cause jitter at smoothTimes < 0.1.
                targetVelocity = dynamicCameraFunctions.LeadLagTargetByVelocityCompensation(targetVelocity, cameraTracker);


                DCEffectorOutputData displacementOutput;

                // Displacement by Effectors. This displace the target position by the effectors
                targetPosition = dynamicCameraFunctions.DisplaceByEffectors(targetPosition, ref targetVelocity, ref targetAcceleration, out displacementOutput);  // we need the displacement for orthographic cameras as the depth is stored in the Z component

                // If you want to use your own camera for displacment by effectors use: DCEffectorManager.GetDisplacementAt(...)
                // If your camera is not a dynamic system that requires an update step then you can omit everything below   


                // Lead/Lag (type 2) by displacing the target by the current velocity and scale the influence of it by the effector influence.
                // targetPosition = dynamicCameraFunctions.LeadLagTargetByPositionCompensation(targetPosition, targetVelocity, (1 - displacementOutput.influence));
                // the order is important: if we would offset by lead/lag by position first and then do effector displacement, we displace from the perspective of the extrapolated position.
                // This can give unwanted behaviour!

                // offset the target position by the z axis offset of the camera
                Vector3 cameraTargetPosition = new Vector3(targetPosition.x, targetPosition.y, targetPosition.z + cameraRigPositionOffsetZ);

                // apply the final offset to the target
                cameraTargetPosition = cameraTargetPosition + (Vector3)cameraTargetoffset;

                // Update the camera rig with critical damped step                
                cameraRig.position = cameraTracker.CriticalDampedStableClampStep(cameraTargetPosition, targetVelocity, targetAcceleration, Time.deltaTime, MassSpringDamperFunctions.ClampType.Circle);

                // NOTE: used cameraTracker.CriticalDampedStableClampStep(...) here instead of cameraTracker.CriticalDampedStep(...). 
                // When we only do positional tracking and we want to clamp it the StableClamp method is the better option.                

            }
        }
       
    }
}

