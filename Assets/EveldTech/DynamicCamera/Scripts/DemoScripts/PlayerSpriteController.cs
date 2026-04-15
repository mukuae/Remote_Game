using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera.Demo
{
    /// <summary>
    /// Simple 2D player movement script for a sprite
    /// </summary>
    public class PlayerSpriteController : MonoBehaviour
    {
        // very simple movement functions for moving around a 2D player with W and D keys. You can adjust the stopping speeds in the inspector to see what effect it has on the tracker.
        public GameObject playerRig;                    // the main player game object
        public Transform playerVisual;                  // needed to flip the sprite around
        public float movementSpeed = 5;
        public float jumpspeed = 7;

        public bool useLinearSlowDown = false;          // which type of slowdown to use

        [Range(0.0f, 1)]
        public float exponentialStoppingFactor = 0.8f;  // reduces velocity factored by this every frame
        [Range(0.02f, 1)]
        public float linearStoppingTime = 0.3f;         // linear reduces velocity to zero over this amount of time (like friction)


        private bool jumpPressed = false;

        private Rigidbody2D playerRigidbody;

        void Start()
        {
            playerRigidbody = playerRig.GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            // jumping
            if (Input.GetKeyDown(KeyCode.Space))
            {
                jumpPressed = true;
            }
        }



        void FixedUpdate()
        {
            // very simple movement
            if (Input.GetKey(KeyCode.A) && !Input.GetKey(KeyCode.D))
            {
                playerRigidbody.linearVelocity = new Vector2(-movementSpeed, playerRigidbody.linearVelocity.y);
                playerVisual.transform.rotation = Quaternion.Euler(0, 180, 0);
            }

            else if (!Input.GetKey(KeyCode.A) && Input.GetKey(KeyCode.D))
            {
                playerRigidbody.linearVelocity = new Vector2(movementSpeed, playerRigidbody.linearVelocity.y);
                playerVisual.transform.rotation = Quaternion.Euler(0, 0, 0);
            }
            else
            {
                if (!useLinearSlowDown)
                {
                    // simple exponential slow down on the x axis. This results in a non constant de/accelleration, the tracker can't track this perfectly.
                    playerRigidbody.linearVelocity = new Vector2(playerRigidbody.linearVelocity.x * exponentialStoppingFactor, playerRigidbody.linearVelocity.y);
                }
                else
                {
                    // linear stopping -> tracker can track constant accleration. This stops the player by roughly constant acceleration (dry friction does the same)
                    float velX = playerRigidbody.linearVelocity.x;
                    float signX = Mathf.Sign(velX);

                    float deltaV = movementSpeed / (linearStoppingTime / Time.fixedDeltaTime);  // the amount of velocity reduction per frame

                    float velXnew = velX - signX * deltaV;
                    if (signX < 0)
                    {
                        velXnew = Mathf.Min(velXnew, 0);
                    }
                    else
                    {
                        velXnew = Mathf.Max(velXnew, 0);
                    }

                    playerRigidbody.linearVelocity = new Vector2(velXnew, playerRigidbody.linearVelocity.y);
                }
            }



            if (jumpPressed)
            {
                playerRigidbody.linearVelocity = new Vector2(playerRigidbody.linearVelocity.x, jumpspeed);
                jumpPressed = false;
            }


        }

        private void OnGUI()
        {
            int width = 200;
            Rect screenRect = new Rect(Screen.width - width, 1, width, 200);

            GUILayout.BeginArea(screenRect);
            GUILayout.Label("Press A or D to move and space to jump! (you can press jump muliple times)\nH: hide crosshair\nQ: Camera Shake");
            GUILayout.EndArea();
        }
    }
}