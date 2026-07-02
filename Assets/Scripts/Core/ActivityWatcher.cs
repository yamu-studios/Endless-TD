using System;
using UnityEngine;

namespace ETD.Core
{
    public class ActivityWatcher : MonoBehaviour
    {
        public static event Action<GameObject, bool> OnActivityChanged;


        bool isBeingDestroy;
        private void OnEnable()
        {
            OnActivityChanged?.Invoke(gameObject, true);
        }

        private void OnDisable()
        {
            if(gameObject != null && !isBeingDestroy)
                OnActivityChanged?.Invoke(gameObject, false);
        }

        private void OnDestroy()
        {
            isBeingDestroy = true;
        }
    }
}
