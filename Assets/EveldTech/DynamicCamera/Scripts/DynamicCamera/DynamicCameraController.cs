using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera
{
    /// <summary>
    /// Extensive example on how to create a Dynamic Camera Controller.
    /// </summary>
    public class DynamicCameraController : MonoBehaviour
    {
        public DynamicCameraTracker cameraTracker;              // takes care of smoothing out the montion of the camera towards the target
        public MassSpringDamperTracker1D orthoSizeSmoother;     // takes care of smoothing the orthograpic size to a new value
        public DynamicCameraFunctions dynamicCameraFunctions;   // contains specialized functions
        public Rigidbody2D targetRigidbody;                     // rigidbody2D as target we want to track
        public Transform cameraRig;                             // transform that is the camera or is the parent of the camera. It is better to have the camera parented to the rig as this allows for the camera shake effect.
        public Camera currentCamera;                            // camera attached to the rig

        public bool useUnderDampedSmoothing = false;            // warning using under damped motion can cause motion sickness

        public Vector2 cameraTargetoffset;                      // offset added to the final position to shift the camera
        public float cameraRigPositionOffsetZ = 0;              // z position of the camera at initialization
        public float intialCameraOrthoSize = 1;                 // orthographic size at initialization

        private bool useVelocityTracking = true;                        // track velocity of the target for more accurate tracking
        private bool useAccelerationTracking = false;                   // track acceleration (tracking acceleration + velocity gives the most accurate tracking)
        
        private Vector2 targetAcceleration = Vector2.zero;              // We need to calculate the acceleration as deltaV / deltaT
        private Vector2 previousTargetVelocity = Vector2.zero;          // We also need to keep track of the previous velocity to calculate the acceleration
        private Vector3 initialTargetPositionOnLoad;                    // this is here so we can reset the position easy by pressing a reset button for demo, which also shows the behaviour of the camera tracker when it has to move quite a bit to a new position

        // Initialize camera offsets and orthographic size
        void Start()
        {
            cameraRigPositionOffsetZ = cameraRig.position.z;
            intialCameraOrthoSize = currentCamera.orthographicSize;
            cameraTracker.SetInitialConditions(cameraRig.position, Vector3.zero);   // set position to the current camera position and velocity to zero
            orthoSizeSmoother.SetInitialConditions(intialCameraOrthoSize, 0);       // set initial ortho size to the current camera its size and velocity to zero

            initialTargetPositionOnLoad = targetRigidbody.transform.position;    // set the target position for a quick reset
        }

        // example for smoothing acceleration with a moving average
        // Vector2[] accelerationArray = new Vector2[] { Vector2.zero, Vector2.zero, Vector2.zero};

        private void FixedUpdate()
        {
            // If you want to use acceleration, calculate acceleration like this (must be in fixed update for most stable results):
            targetAcceleration = (targetRigidbody.linearVelocity - previousTargetVelocity) / Time.fixedDeltaTime;   // calculates the acceleration
            previousTargetVelocity = targetRigidbody.linearVelocity;

            // note that acceleration could be calculated in Late/Update(), however this only works a frame rates > 1/Time.fixedDeltaTime
            // at lower frame rates you will miss information.

            // Using the acceleration of the target allows us to track the target perfectly while it is accelerating.
            // - when velocity makes a large change, acceleration spikes. This is physically correct, but can feel too sudden for a player.
            // - A solution for preventing this is to smooth out the acceleration by using a weighted moving average for example.
            // 
            // accelerationArray[0] = accelerationArray[1];
            // accelerationArray[1] = accelerationArray[2];
            // accelerationArray[2] = currentTargetAcceleration;
            // currentTargetAcceleration = (accelerationArray[0] * 0.15f + accelerationArray[1] * 0.3f + accelerationArray[2] * 0.55f);

            // Most of the time it is fine to use only velocity tracking, tuning acceleration tracking can be somewhat tedious to get good results

            // Note that acceleration is directly proportional to the force, in the tracker its case it is a 1:1 ratio. This means that we can add a force to nudge the camera tracker.
            // Nudging the camera can create cool effects such as when the player is hit you nudge the camera in the opposite direction by applying a "force" shortly.
        }



        // It is advised to use the LateUpdate() for the camera tracker update for best results. Note that it works with Update() properly only if the tracker target is a rigidbody2D with interpolation enabled (which should be also for LateUpdate()).
        void LateUpdate()
        {            
            if (cameraRig != null)
            {
                
                Vector3 targetPosition = targetRigidbody.transform.position; // dont use Rigidbody2D.position as that gives a non interpolated location!

                // Collision Prediction compensation
                Vector2 targetVelocity = dynamicCameraFunctions.LookAheadTrackingVelocityCompensation(targetRigidbody);         // this has to be called first before the effector displacement


                // Lead/Lag (type 1) by compensating the velocity. This version of calculating lead lag is insensitive to the target position so this can be done before the effector displacement. However it can cause jitter at smoothTimes < 0.1.
                targetVelocity = dynamicCameraFunctions.LeadLagTargetByVelocityCompensation(targetVelocity, cameraTracker);     // you can also give an argument for the influence of the lead/lag but then you have to place it after the effector if you want to use that data


                // Displacement by Effectors. This displace the target position by the effectors and adjusts the velocity and acceleration in such a way that it gets clamped to the tangent direction of the displacement
                targetPosition = dynamicCameraFunctions.DisplaceByEffectors(targetPosition, ref targetVelocity, ref targetAcceleration, out DCEffectorOutputData displacementOutput);  // we need the displacement for orthographic cameras as the depth is stored in the Z component


                // Lead/Lag (type 2) by displacing the target by the current velocity and scale the influence of it by the effector influence. This lead/lag method can handle smoothTimes < 0.1
                // targetPosition = dynamicCameraFunctions.LeadLagTargetByPositionCompensation(targetPosition, targetVelocity, 1 - displacementOutput.influence); // the outcome is similar to the type1 lead/lag

                // the order is important: if we would offset by lead/lag by position first and then do effector displacement, we displace from the perspective of the extrapolated position.
                // This can give unwanted behaviour! Also note that using lead/lag always extrapolates the velocity!

                Vector3 cameraTargetPosition;
                if (currentCamera.orthographic)
                {
                    // OrthographicSize smooth (type 1):
                    // smooth orthographic size by its individual smoother (see line 132 for type 2 smoothing)
                    cameraTargetPosition = new Vector3(targetPosition.x, targetPosition.y, cameraRigPositionOffsetZ);   // no displacement in the z direction for orthographic cameras                    
                    float targetOrthoSize = Mathf.Max(intialCameraOrthoSize - displacementOutput.displacement.z * DCEffector.depthToOrthgrapicSizeFactor, 0.1f);
                    currentCamera.orthographicSize = orthoSizeSmoother.CriticalDampedStep(targetOrthoSize, 0, Time.deltaTime, false);
                }
                else
                {
                    cameraTargetPosition = new Vector3(targetPosition.x, targetPosition.y, targetPosition.z + cameraRigPositionOffsetZ);    // offset target with camera z offset
                }

                // apply a final offset to the target if needed
                cameraTargetPosition = cameraTargetPosition + (Vector3)cameraTargetoffset;
                
                // sets target velocity to zero (UI adjustable)
                if (!useVelocityTracking)
                {
                    targetVelocity = Vector2.zero;
                }

                // Sets target acceleration to zero (UI adjustable)
                if (!useAccelerationTracking)
                {
                    targetAcceleration = Vector2.zero;     // sets current acceleration to zero if we dont want to use the target its acceleration tracking
                }

                // Two option for updating the camera tracker.
                if (useUnderDampedSmoothing)
                {
                    // Under damped makes the camera position oscillate, which damps out based on the damping ratio
                    cameraRig.position = cameraTracker.UnderDampedStep(cameraTargetPosition, targetVelocity, targetAcceleration, Time.deltaTime, MassSpringDamperFunctions.ClampType.Circle);
                }
                else
                {         
                    // Update the critical damped tracker
                    //cameraRig.position = cameraTracker.CriticalDampedStep(cameraTargetPosition, targetVelocity, targetAcceleration, Time.deltaTime, MassSpringDamperFunctions.ClampType.Circle);
                    
                    // The following method is the better option if you are going to clamp the tracker position
                    cameraRig.position = cameraTracker.CriticalDampedStableClampStep(cameraTargetPosition, targetVelocity, targetAcceleration, Time.deltaTime, MassSpringDamperFunctions.ClampType.Circle);
                }


                /*
                // OrthographicSize smoothing (type 2):
                // This is another way to create a ortho graphic zoom effect. The (cameraRig.position.z - cameraRigPositionOffsetZ) is taken here as the already smoothed factor for setting the orthographic size
                // The disadvantage of this is that it also displaces the camera position on the Z axis, which could be something that is unwanted.
                if (currentCamera.orthographic)
                {                    
                    float targetOrthoSize = Mathf.Max(intialCameraOrthoSize - (cameraRig.position.z - cameraRigPositionOffsetZ) * DCEffector.depthToOrthgrapicSizeFactor, 0.1f);
                    currentCamera.orthographicSize = targetOrthoSize;
                }
                */
            }
        }



        // UI related variables
        private bool showSettings = false;

        // used for exponential slider values
        private float sliderValueExpDistanceXY = 1;
        private float sliderValueExpDistanceZ = 1;

        private float sliderValueExpAccelLimitX = 1;
        private float sliderValueExpAccelLimitY = 1;

        // Simple Test UI for adjusting values of the camera tracker behaviour
        private void OnGUI()
        {
            int width = 300;
            int heigth = 650;
            int dampingGUIHeight = 70;

            heigth = useUnderDampedSmoothing ? heigth + dampingGUIHeight : heigth;

            int sliderWidth = 130;
            Rect rect = new Rect(1, 1, width, heigth);
            GUILayout.BeginArea(rect);
            
            GUILayout.Label("Camera Tracker settings:");
            if (GUILayout.Button("Reset Position", GUILayout.Width(100)))
            {
                targetRigidbody.transform.position = initialTargetPositionOnLoad;    // reset to the initial position, you can see the effects of the max follow distance clamps when pressing this
                targetRigidbody.linearVelocity = Vector2.zero;

                // force camera rig position to this position instantly by setting the cameraTracker initial conditions as:
                // cameraTracker.SetInitialConditions(initialTargetPositionOnLoad + new Vector3(0, 0, cameraRigPositionOffsetZ), Vector3.zero);
            }
            showSettings = GUILayout.Toggle(showSettings, "Show camera settings", GUILayout.Width(width));

            if (showSettings)
            {
                GUILayout.Box("", GUILayout.Width(width), GUILayout.Height(heigth));
                Rect rectSettings = new Rect(1, 70, width, heigth);
                GUILayout.BeginArea(rectSettings);

                // convergence time settings
                GUILayout.Label("Smooth time (TIP: use RMB to adjust sliders):");
                GUILayout.BeginHorizontal();
                cameraTracker.smoothTimeX = GUILayout.HorizontalSlider(cameraTracker.smoothTimeX, 0.01f, 2, GUILayout.Width(sliderWidth));
                GUILayout.Label($"{cameraTracker.smoothTimeX:F2}: smoothTime XY");
                cameraTracker.smoothTimeY = cameraTracker.smoothTimeX;              // link smoothtimeY with X as that is more convenient
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                cameraTracker.smoothTimeZ = GUILayout.HorizontalSlider(cameraTracker.smoothTimeZ, 0.01f, 2, GUILayout.Width(sliderWidth));
                GUILayout.Label($"{cameraTracker.smoothTimeZ:F2}: smoothTime Z");
                GUILayout.EndHorizontal();

                // maximum distance clamp settings
                GUILayout.Label("Distance clamp:");
                GUILayout.BeginHorizontal();
                sliderValueExpDistanceXY = GUILayout.HorizontalSlider(sliderValueExpDistanceXY, 0f, 1f, GUILayout.Width(sliderWidth));
                cameraTracker.maxFollowDistanceX = sliderValueExpDistanceXY * sliderValueExpDistanceXY * 100f;
                GUILayout.Label($"{cameraTracker.maxFollowDistanceX:F2}: max Distance XY");
                cameraTracker.maxFollowDistanceY = cameraTracker.maxFollowDistanceX;              // link max follow distance Y with X as that is more convenient
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                sliderValueExpDistanceZ = GUILayout.HorizontalSlider(sliderValueExpDistanceZ, 0f, 1f, GUILayout.Width(sliderWidth));
                cameraTracker.maxFollowDistanceZ = sliderValueExpDistanceZ * sliderValueExpDistanceZ * 100f;
                GUILayout.Label($"{cameraTracker.maxFollowDistanceZ:F2}: max Distance Z");
                GUILayout.EndHorizontal();

                // anti overshoot settings
                GUILayout.BeginHorizontal();
                cameraTracker.antiOvershootX = GUILayout.Toggle(cameraTracker.antiOvershootX, "antiOvershoot X");
                cameraTracker.antiOvershootY = GUILayout.Toggle(cameraTracker.antiOvershootY, "antiOvershoot Y");
                GUILayout.EndHorizontal();

                // acceleration limit settings
                GUILayout.Label("Acceleration LimitX:");
                GUILayout.BeginHorizontal();
                sliderValueExpAccelLimitX = GUILayout.HorizontalSlider(sliderValueExpAccelLimitX, 0f, 1f, GUILayout.Width(sliderWidth * 2));
                cameraTracker.accelerationThresholdX = sliderValueExpAccelLimitX * sliderValueExpAccelLimitX * 10000f;
                GUILayout.Label($"{cameraTracker.accelerationThresholdX:F0}");
                GUILayout.EndHorizontal();
                GUILayout.Label("Acceleration LimitY:");
                GUILayout.BeginHorizontal();
                sliderValueExpAccelLimitY = GUILayout.HorizontalSlider(sliderValueExpAccelLimitY, 0f, 1f, GUILayout.Width(sliderWidth * 2));
                cameraTracker.accelerationThresholdY = sliderValueExpAccelLimitY * sliderValueExpAccelLimitY * 10000f;
                GUILayout.Label($"{cameraTracker.accelerationThresholdY:F0}");
                GUILayout.EndHorizontal();

                cameraTracker.accelerationOverThresholdIsZero = GUILayout.Toggle(cameraTracker.accelerationOverThresholdIsZero, "zero if > threshold");

                // look ahead settings
                GUILayout.Label("Look ahead times:");
                GUILayout.BeginHorizontal();
                dynamicCameraFunctions.lookAheadTimeFirst = GUILayout.HorizontalSlider(dynamicCameraFunctions.lookAheadTimeFirst, 0, 1, GUILayout.Width(sliderWidth));
                GUILayout.Label($"{dynamicCameraFunctions.lookAheadTimeFirst:F2}: first look ahead time");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                dynamicCameraFunctions.lookAheadTimeSecond = GUILayout.HorizontalSlider(dynamicCameraFunctions.lookAheadTimeSecond, 0, 1, GUILayout.Width(sliderWidth));
                GUILayout.Label($"{dynamicCameraFunctions.lookAheadTimeSecond:F2}: second look ahead time");
                GUILayout.EndHorizontal();

                // lead/lag settings
                GUILayout.BeginHorizontal();
                GUILayout.Label("Lead/Lag:");
                dynamicCameraFunctions.leadLagBoxClamp = GUILayout.Toggle(dynamicCameraFunctions.leadLagBoxClamp, " use box clamp?");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                dynamicCameraFunctions.leadLagMaxDistanceX = GUILayout.HorizontalSlider(dynamicCameraFunctions.leadLagMaxDistanceX, -4, 4, GUILayout.Width(sliderWidth));
                GUILayout.Label($"{dynamicCameraFunctions.leadLagMaxDistanceX:F2}: max lead distX");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                dynamicCameraFunctions.leadLagMaxDistanceY = GUILayout.HorizontalSlider(dynamicCameraFunctions.leadLagMaxDistanceY, -4, 4, GUILayout.Width(sliderWidth));
                GUILayout.Label($"{dynamicCameraFunctions.leadLagMaxDistanceY:F2}: max lead distY");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                dynamicCameraFunctions.leadLagMaxAtVelocity = GUILayout.HorizontalSlider(dynamicCameraFunctions.leadLagMaxAtVelocity, 0, 30, GUILayout.Width(sliderWidth));
                GUILayout.Label($"{dynamicCameraFunctions.leadLagMaxAtVelocity:F2}: max lead at vel");
                GUILayout.EndHorizontal();


                currentCamera.orthographic = GUILayout.Toggle(currentCamera.orthographic, "use Orthographic View?", GUILayout.Width(width));

                // use velocity tracking?
                useVelocityTracking = GUILayout.Toggle(useVelocityTracking, "use Velocity Tracking?", GUILayout.Width(width));
                // use acceleration tracking?
                useAccelerationTracking = GUILayout.Toggle(useAccelerationTracking, "use Acceleration Tracking?", GUILayout.Width(width));



                // under damping settings
                useUnderDampedSmoothing = GUILayout.Toggle(useUnderDampedSmoothing, "use Under Damping?", GUILayout.Width(sliderWidth));
                if (useUnderDampedSmoothing)
                {
                    GUILayout.BeginHorizontal();
                    cameraTracker.dampingRatioX = GUILayout.HorizontalSlider(cameraTracker.dampingRatioX, 0.0f, 0.99f, GUILayout.Width(sliderWidth));
                    GUILayout.Label($"{cameraTracker.dampingRatioX:F2}: damping ratio X");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    cameraTracker.dampingRatioY = GUILayout.HorizontalSlider(cameraTracker.dampingRatioY, 0.0f, 0.99f, GUILayout.Width(sliderWidth));
                    GUILayout.Label($"{cameraTracker.dampingRatioY:F2}: damping ratio Y");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    cameraTracker.dampingRatioZ = GUILayout.HorizontalSlider(cameraTracker.dampingRatioZ, 0.0f, 0.99f, GUILayout.Width(sliderWidth));
                    GUILayout.Label($"{cameraTracker.dampingRatioZ:F2}: damping ratio Z");
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndArea();
            }        

            GUILayout.EndArea();
        }
    }
}

