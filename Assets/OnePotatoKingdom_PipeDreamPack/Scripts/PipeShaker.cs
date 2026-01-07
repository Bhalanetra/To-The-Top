using UnityEngine;

namespace PipeEffects
{
    public class PipeShaker : MonoBehaviour
    {
        [Header("Shake Settings")]
        [Tooltip("Enable or disable the shaking effect.")]
        [SerializeField] private bool enableShaking = true;

        [Tooltip("The maximum distance the pipe will move from its original position.")]
        [SerializeField] private float shakeMagnitude = 0.05f;

        [Tooltip("The speed of the shaking motion. Higher values are faster.")]
        [SerializeField] private float shakeSpeed = 5f;

        private Vector3 initialPosition;

        private float xOffset;
        private float yOffset;
        private float zOffset;

        void Start()
        {
            initialPosition = transform.localPosition;
            xOffset = Random.Range(0f, 100f);
            yOffset = Random.Range(0f, 100f);
            zOffset = Random.Range(0f, 100f);
        }

        void Update()
        {
            if (!enableShaking)
            {
                return;
            }

            float x = (Mathf.PerlinNoise(Time.time * shakeSpeed, xOffset) * 2f) - 1f;
            float y = (Mathf.PerlinNoise(Time.time * shakeSpeed, yOffset) * 2f) - 1f;
            float z = (Mathf.PerlinNoise(Time.time * shakeSpeed, zOffset) * 2f) - 1f;

            transform.localPosition = initialPosition + new Vector3(x, y, z) * shakeMagnitude;
        }

        public void SetShaking(bool state)
        {
            enableShaking = state;
        }
    }
}