using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera.Demo
{
    /// <summary>
    /// Simple crosshair for the camera.
    /// </summary>
    public class SetCrossHair : MonoBehaviour
    {
        // Start is called before the first frame update
        public Transform crosshair;
        public float offset = 2;

        private bool pressedHideButton = false;

        void Start()
        {
            crosshair.position = Camera.main.transform.position + Camera.main.transform.forward * offset;
            crosshair.parent = Camera.main.transform;
        }

        // Update is called once per frame
        void Update()
        {
            // hides the crosshair or shows it when pressed again
            if (Input.GetKeyDown(KeyCode.H) && !pressedHideButton)
            {
                crosshair.GetComponent<MeshRenderer>().enabled = !crosshair.GetComponent<MeshRenderer>().enabled;
                pressedHideButton = true;
            }
            else
            {
                pressedHideButton = false;
            }


        }
    }
}