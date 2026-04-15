using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera
{
    /// <summary>
    /// Camera shake with additive strength, exponential decay and ramp up
    /// </summary>
    public class DCCameraShake : MonoBehaviour
    {

        public Transform cameraRig;                 // this must be the parent of the camera, it is used a reference point for the shake
        public Camera cameraToShake;                // the camera attached to the rig

        [HideInInspector]
        public float strength = 0;
        private float addedStrength = 0;            // the to be added strength, normalized between 0 and 1
        private float strengthTimer = 0;            // normalized between 0 and 1

        public float strengthDecayTime = 1;         // from strength = 1 to strength = 0
        public float strengthRampUpTime = 0.1f;     // time for strength 0 to 1

        public float shakeSpeed = 10;               // the main speed that slides over the perlin noise

        // initialize amplitudes at reasonable values
        public float xAmplitude = 1;
        public float yAmplitude = 1;
        public float zAmplitude = 0;

        // amplitude is in degrees
        public float pitchAmplitude = 10f;
        public float yawAmplitude = 10f;
        public float rollAmplitude = 10f;

        [Range(0, 1)]
        public float xSpeedFactor = 1;  // speed factor for shakeSpeed how fast it slides over the perlin noise
        [Range(0, 1)]
        public float ySpeedFactor = 1;  // speed factor for shakeSpeed how fast it slides over the perlin noise
        [Range(0, 1)]
        public float zSpeedFactor = 1;  // speed factor for shakeSpeed how fast it slides over the perlin noise

        [Range(0, 1)]
        public float pitchSpeedFactor = 1;  // speed factor for shakeSpeed how fast it slides over the perlin noise
        [Range(0, 1)]
        public float yawSpeedFactor = 1;    // speed factor for shakeSpeed how fast it slides over the perlin noise
        [Range(0, 1)]
        public float rollSpeedFactor = 1;   // speed factor for shakeSpeed how fast it slides over the perlin noise



        private bool rampUp = false;    // keeps track if the strength needs to be ramped up

        private Vector3 offset;         // positional offset for the camera caused by the shake with respect to the reference

        // rotational offsets wrt the reference
        private float pitch;
        private float yaw;
        private float roll;


        void Update()
        {            
            if (strengthTimer != 0 || rampUp)
            {
                UpdateShakeStrength();      // must update strength first
                UpdateShakeOffsetValues();  // update offset values with current strengths
                ApplyCameraOffsets();       // apply the offsets to the camera
            }          
        }


        private void ApplyCameraOffsets()
        {
            Quaternion offsetRot = Quaternion.Euler(pitch, yaw, roll);
            cameraToShake.transform.rotation = cameraRig.rotation * offsetRot;                                        // rotate the reference rotation by the offset rotation
            cameraToShake.transform.position = cameraRig.transform.position + cameraRig.TransformDirection(offset);   // apply offset aligned with the reference axis
        }

        private void UpdateShakeOffsetValues()
        {
            float time = Time.time % 5000;  // wrap around for keeping float value relative low for precision
            // use perlin noise for smooth value noise
            float x = xAmplitude * strength * (Mathf.PerlinNoise(time * shakeSpeed * xSpeedFactor, 0.21f) - 0.5f) * 2;
            float y = yAmplitude * strength * (Mathf.PerlinNoise(time * shakeSpeed * ySpeedFactor, 4.45f) - 0.5f) * 2;
            float z = zAmplitude * strength * (Mathf.PerlinNoise(time * shakeSpeed * zSpeedFactor, 2.93f) - 0.5f) * 2;

            offset = new Vector3(x, y, z);

            pitch = pitchAmplitude * strength * (Mathf.PerlinNoise(time * shakeSpeed * pitchSpeedFactor, 0.62f) - 0.5f) * 2;
            yaw = yawAmplitude * strength * (Mathf.PerlinNoise(time * shakeSpeed * yawSpeedFactor, 3.14f) - 0.5f) * 2;
            roll = rollAmplitude * strength * (Mathf.PerlinNoise(time * shakeSpeed * rollSpeedFactor, 1.87f) - 0.5f) * 2;
        }

        private void UpdateShakeStrength()
        {
            if (strengthTimer > 0 && !rampUp)
            {
                strengthTimer -= Time.deltaTime / strengthDecayTime;
                strengthTimer = Mathf.Max(strengthTimer, 0);
                strength = strengthTimer * strengthTimer;       // strength is quadratically proportional to the strength timer, this gives a better feel than linear scaling.
                addedStrength = strength;
            }
            else if (strengthTimer >= 0 && rampUp)
            {
                strengthTimer += Time.deltaTime / strengthRampUpTime;
                strengthTimer = Mathf.Min(strengthTimer, 1);
                strength = strengthTimer * strengthTimer;       // strength is quadratically proportional to the strength timer, this gives a better feel than linear scaling.
                if (strength >= addedStrength || strengthTimer == 1)
                {
                    rampUp = false;
                }
            }
            else
            {
                strengthTimer = 0;
                strength = 0;
            }
        }


        /// <summary>
        /// Adds strength to the shaker, note that strength is capped at 1
        /// </summary>
        /// <param name="strengthValue">strength to be added, between 0 and 1</param>
        public void AddShake(float strengthValue)
        {
            addedStrength += strengthValue;
            addedStrength = Mathf.Min(Mathf.Max(addedStrength, 0), 1);
            rampUp = true;
        }


    }

}