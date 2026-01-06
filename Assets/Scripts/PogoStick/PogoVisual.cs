using UnityEngine;

public class PogoVisual : MonoBehaviour
{
    public Transform pogoRod;
    public float minScale = 0.5f;
    public float maxScale = 1f;

    Rigidbody rb;

    void Start()
    {
        rb = GetComponentInParent<Rigidbody>();
    }

    void LateUpdate()
    {
        float compression = Mathf.InverseLerp(-5, 5, rb.linearVelocity.y);
        float scaleY = Mathf.Lerp(maxScale, minScale, compression);

        pogoRod.localScale = new Vector3(1, scaleY, 1);
    }
}
