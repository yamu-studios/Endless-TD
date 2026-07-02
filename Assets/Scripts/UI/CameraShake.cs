using UnityEngine;

namespace ETD.UI
{
    using System.Collections;
    using UnityEngine;

    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Header("Default Shake")]
        [SerializeField] private float defaultDuration = 0.2f;
        [SerializeField] private float defaultStrength = 0.08f;
        [SerializeField] private float defaultFrequency = 35f;

        private Vector3 originalLocalPosition;
        private Coroutine shakeRoutine;

        private void Awake()
        {
            Instance = this;
            originalLocalPosition = transform.localPosition;
        }

        public void Shake()
        {
            Shake(defaultDuration, defaultStrength, defaultFrequency);
        }

        public void Shake(float duration, float strength)
        {
            Shake(duration, strength, defaultFrequency);
        }

        public void Shake(float duration, float strength, float frequency)
        {
            if (shakeRoutine != null)
                StopCoroutine(shakeRoutine);

            shakeRoutine = StartCoroutine(ShakeCoroutine(duration, strength, frequency));
        }

        private IEnumerator ShakeCoroutine(float duration, float strength, float frequency)
        {
            float timer = 0f;
            originalLocalPosition = transform.localPosition;
            while (timer < duration)
            {
                timer += Time.deltaTime;

                float progress = timer / duration;
                float fade = 1f - progress;

                float x = Mathf.PerlinNoise(Time.time * frequency, 0f) - 0.5f;
                float y = Mathf.PerlinNoise(0f, Time.time * frequency) - 0.5f;

                Vector3 offset = new Vector3(x, y, 0f) * strength * fade;

                transform.localPosition = originalLocalPosition + offset;

                yield return null;
            }

            transform.localPosition = originalLocalPosition;
            shakeRoutine = null;
        }
    }
}
