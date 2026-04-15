using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpielerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 5f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    private Rigidbody rb;
    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        if (groundCheck == null)
        {
            GameObject gc = new GameObject("GroundCheck");
            gc.transform.parent = transform;
            gc.transform.localPosition = new Vector3(0, -0.5f, 0);
            groundCheck = gc.transform;
        }

        // Automatisch beim Kamera-Script registrieren
        var cameraController = FindObjectOfType<Eveld.DynamicCamera.DynamicCameraController>();
        if (cameraController != null)
        {
            // Verwende Reflection um das private Feld zu setzen
            var field = cameraController.GetType().GetField("targetRigidbody", 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                field.SetValue(cameraController, rb);
                Debug.Log("Player automatisch bei Kamera registriert!");
            }
        }
    }

    void Update()
    {
        // Ground Check
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        // Input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // Bewegung
        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        rb.MovePosition(rb.position + move * moveSpeed * Time.deltaTime);

        // Jump
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
}
