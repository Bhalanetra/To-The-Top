using UnityEngine;

public class PogoWobble : MonoBehaviour
{
    public float leanAmount = 12f;
    public float wobbleSpeed = 6f;

    Rigidbody rb;

    void Start()
    {
        rb = GetComponentInParent<Rigidbody>();
    }

    void LateUpdate()
    {
        Vector3 vel = rb.linearVelocity;

        float x = Mathf.Clamp(-vel.x * leanAmount, -15, 15);
        float z = Mathf.Clamp(-vel.z * leanAmount, -15, 15);

        Quaternion target =
            Quaternion.Euler(z, 0, x);

        transform.localRotation =
            Quaternion.Slerp(transform.localRotation, target, Time.deltaTime * wobbleSpeed);
    }
}
