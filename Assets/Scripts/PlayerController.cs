using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float jumpForce = 7f;
    public float mouseSensitivity = 200f;

    private Rigidbody rb;
    private bool isGrounded;

    public Transform cameraTransform;
    public Vector3 cameraOffset = new Vector3(0, 3, -6);

    float xRotation = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }

        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        Move();
        Jump();
        MouseLook();
        FollowCamera();
    }

    void Move()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 move = transform.right * moveX + transform.forward * moveZ;

        // Achtung: in "normalem" Rigidbody heißt es rb.velocity, nicht rb.linearVelocity
        rb.linearVelocity = new Vector3(move.x * moveSpeed, rb.linearVelocity.y, move.z * moveSpeed);
    }

    void Jump()
    {
        // Springen nur erlauben, wenn man auf dem Boden ist
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false; // Direkt nach dem Sprung auf "in der Luft" setzen
        }
    }

    void MouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void FollowCamera()
    {
        cameraTransform.position = transform.position + transform.TransformDirection(cameraOffset);
    }

    void OnCollisionEnter(Collision collision)
    {
        // Wenn wir den Boden berühren, sind wir wieder "grounded"
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }
}
