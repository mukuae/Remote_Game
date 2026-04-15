using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera.Demo
{
    /// <summary>
    /// Class that sets the target frame rate of the applciation, this allows for testing scripts at low fps
    /// </summary>
    public class TargetFrameRate : MonoBehaviour
    {
        public int targetFrameRate = 10;
        public bool useTargetFrameRate = false;

        private const int targetFramerateDefault = -1;


        void Start()
        {
            if (useTargetFrameRate)
            {
                QualitySettings.vSyncCount = 0;
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (targetFrameRate != Application.targetFrameRate && useTargetFrameRate)
            {
                Application.targetFrameRate = targetFrameRate;
            }
            else if (targetFramerateDefault != Application.targetFrameRate)
            {
                Application.targetFrameRate = targetFramerateDefault;
            }

            // set target frame rate to test low fps conditions
            if (Input.GetKeyDown(KeyCode.F1) && !useTargetFrameRate)
            {
                QualitySettings.vSyncCount = 0;
                useTargetFrameRate = true;
            }
            // reset
            else if (Input.GetKeyDown(KeyCode.F1) && useTargetFrameRate)
            {
                QualitySettings.vSyncCount = 2; // set to monitor refresh rate
                useTargetFrameRate = false;
            }
        }
    }
}
