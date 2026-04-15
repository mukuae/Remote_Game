using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera.Demo
{
    /// <summary>
    /// Example on how to use the camera shaker
    /// </summary>
    public class CameraShakeUsage : MonoBehaviour
    {

        public GameObject shakerObject;
        [Range(0, 1)]
        public float shakeStrength = 0.5f;

        private DCCameraShake cameraShaker;

        // Start is called before the first frame update
        void Start()
        {
            cameraShaker = shakerObject.GetComponent<DCCameraShake>();
        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                cameraShaker.AddShake(shakeStrength);
            }
        }
    }
}