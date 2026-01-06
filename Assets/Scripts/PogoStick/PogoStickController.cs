using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PogoStickController : MonoBehaviour
{
    [Header("Spring")]
    public float springStrength = 1200f;
    public float springDamping = 80f;
    public float rideHeight = 1.2f;

    [Header("Jump")]
    public float jumpBoost = 1.1f;
    public float maxUpVelocity = 15f;

    [Header("Ground")]
    public Transform groundCheck;
    public LayerMask groundMask;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (Physics.Raycast(
            groundCheck.position,
            Vector3.down,
            out RaycastHit hit,
            rideHeight,
            groundMask))
        {
            ApplySpring(hit);
        }
    }

    void ApplySpring(RaycastHit hit)
    {
        float compression = rideHeight - hit.distance;

        float velocity = Vector3.Dot(rb.linearVelocity, Vector3.up);

        float force =
            (compression * springStrength)
            - (velocity * springDamping);

        rb.AddForce(Vector3.up * force, ForceMode.Acceleration);

        // Optional boost for arcade feel
        if (rb.linearVelocity.y < maxUpVelocity)
        {
            rb.linearVelocity += Vector3.up * jumpBoost * Time.fixedDeltaTime;
        }
    }
}
