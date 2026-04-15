using System;
using UnityEngine;

namespace Eveld.DynamicCamera.Demo
{
    /// <summary>
    /// An example of a fast type of player game object for the tracker to track
    /// </summary>
    public class BallController : MonoBehaviour
    {
        // a simple ball player for fast movements to test the tracker its capabilties on fast targets

        Rigidbody2D ballRigidbody;

        private Vector3 clickedMousePosition;           // mouse position at the start of the drag
        private Vector3 longDeltaMouse = Vector3.zero;  // the delta position made by the mouse while holding the mouse button
        private bool launchBall;                        // for communicating when to launch with the fixed update

        public Transform velocityArrow;                 // a visual effect for the mouse drag amount

        // Start is called before the first frame update
        void Start()
        {
            ballRigidbody = this.GetComponent<Rigidbody2D>();
        }


        private void Update()
        {

            // simulate sudden shocks by flipping the velocity in the X direction
            if (Input.GetKeyDown(KeyCode.F))
            {
                Vector2 newVel = ballRigidbody.linearVelocity;
                newVel.x = -ballRigidbody.linearVelocity.x;
                ballRigidbody.linearVelocity = newVel;
            }

            // simulate sudden shocks by flipping the velocity in the Y direction
            if (Input.GetKeyDown(KeyCode.G))
            {
                Vector2 newVel = ballRigidbody.linearVelocity;
                newVel.y = -ballRigidbody.linearVelocity.y;
                ballRigidbody.linearVelocity = newVel;
            }

            if (Input.GetMouseButtonDown(0))
            {
                clickedMousePosition = Input.mousePosition;
                Time.timeScale = 0.1f;  // start slow mo after LMB click
            }

            if (Input.GetMouseButton(0))
            {
                longDeltaMouse = Input.mousePosition - clickedMousePosition;


                Vector2 velNew = new Vector2(longDeltaMouse.x, longDeltaMouse.y) * 0.1f;

                if (velocityArrow != null)
                {
                    velocityArrow.localScale = new Vector3(velNew.magnitude * 0.2f, 1, 1);
                    velocityArrow.position = this.transform.position;
                    velocityArrow.rotation = Quaternion.Euler(new Vector3(0, 0, Vector2.SignedAngle(new Vector2(1, 0), velNew)));
                }

            }

            if (Input.GetMouseButtonUp(0))
            {
                Time.timeScale = 1.0f;  // restore slow mo
                launchBall = true;
            }

        }



        void FixedUpdate()
        {
            if (launchBall)
            {
                Vector2 velNew = new Vector2(longDeltaMouse.x, longDeltaMouse.y) * 0.1f;
                ballRigidbody.linearVelocity = velNew;
                longDeltaMouse = Vector3.zero;
                launchBall = false;
            }
        }

        private void OnGUI()
        {
            int width = 200;
            Rect screenRect = new Rect(Screen.width - width, 1, width, 200);

            GUILayout.BeginArea(screenRect);
            GUILayout.Label("Press LMB and drag to launch the ball. GO FAST!!\nF: flips X velocity\nG: flips Y velocity\nH: hide crosshair\nQ: Camera Shake");
            GUILayout.EndArea();
        }

    }
}

