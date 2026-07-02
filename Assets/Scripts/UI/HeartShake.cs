// ============================================================================
// ETD.UI - HeartShake.cs  [NEW]
// Attach to your heart/lives icon. Shakes when EnemyReachedEndEvent fires.
// ============================================================================
using System.Collections;
using UnityEngine;
using ETD.Core;

namespace ETD.UI
{
    public class HeartShake : MonoBehaviour
    {
        [Header("Shake Settings")]
        [SerializeField] private float _duration    = 0.4f;
        [SerializeField] private float _magnitude   = 8f;
        [SerializeField] private int   _vibrato     = 20;   // shakes per second

        private Vector3 _originalPos;
        private Coroutine _shakeCoroutine;

        private void Awake()
        {
            _originalPos = transform.localPosition;
            EventBus.Subscribe<EnemyReachedEndEvent>(OnDamageTaken);
        }

        private void OnDamageTaken(EnemyReachedEndEvent evt)
        {
            if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
            if (!gameObject.activeInHierarchy) return;
            _shakeCoroutine = StartCoroutine(ShakeCoroutine());
            CameraShake.Instance.Shake(_duration,.15f);
        }

        private IEnumerator ShakeCoroutine()
        {
            float elapsed  = 0f;
            float interval = 1f / _vibrato;
        
            while (elapsed < _duration)
            {
                // Strength fades out toward end
                float strength = _magnitude * (1f - elapsed / _duration);

                float x = Random.Range(-1f, 1f) * strength;
                float y = Random.Range(-1f, 1f) * strength;
                transform.localPosition = _originalPos + new Vector3(x, y, 0f);

                elapsed += interval;
                yield return new WaitForSecondsRealtime(interval);
            }

            transform.localPosition = _originalPos;
            _shakeCoroutine = null;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<EnemyReachedEndEvent>(OnDamageTaken);
        }
    }
}
