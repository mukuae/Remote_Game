using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera.Demo
{
    /// <summary>
    /// Example of a simple Moving platform with the possiblity to attach an effector to it. 
    /// </summary>
    public class MovingPlatform : MonoBehaviour
    {
        public Vector2 amplitude;
        public float speed;
        public Vector2 phase;

        public Vector3 initPos;

        // Example how to move effectors
        public string effectorName;
        public DCEffector effector;
        private Vector3 effectorOffset;

        void Start()
        {
            initPos = transform.position;

            effector = DCEffectorManager.GetEffectorByName(effectorName);   // look up effector and store it
            if (effector != null)
            {
                effectorOffset = (Vector3)effector.rootPosition - initPos;  // offset between moving object and effector
            }

        }

        private void Update()
        {
            if (effector != null)
            {
                effector.MoveEffectorTo(this.transform.position + effectorOffset);  // move the effector wrt this
            }
        }


        void FixedUpdate()
        {
            // very simpel platform movement by using sin and cosine for exact positions and velocity
            float degToRad = Mathf.PI / 180;
            float t = Time.time;
            float x = Mathf.Sin(t * speed + phase.x * degToRad) * amplitude.x;
            float y = Mathf.Sin(t * speed + phase.y * degToRad) * amplitude.y;

            // time derivatives of the position
            float dx = speed * Mathf.Cos(t * speed + phase.x * degToRad) * amplitude.x;
            float dy = speed * Mathf.Cos(t * speed + phase.y * degToRad) * amplitude.y;

            this.GetComponent<Rigidbody2D>().position = initPos + new Vector3(x, y, 0);
            this.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(dx, dy);

            // by setting the exact velocity and position we get a properly physics based platform where the player can stand on
        }

    }
}