using UnityEngine;

namespace ETD.Utilities
{
    public class ScaleLoop : MonoBehaviour
    {
        public Transform target;     // Assign your child VFX object
        public float speed = 2f;     // How fast it scales
        public float scaleAmount = 0.3f; // How much it scales up/down

        private Vector3 initialScale;

        void Start()
        {
            if (target == null)
                target = transform;

            initialScale = target.localScale;
        }

        void Update()
        {
            float scale = 1 + Mathf.Sin(Time.time * speed) * scaleAmount;
            target.localScale = initialScale * scale;
        }
    }
}
