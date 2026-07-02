using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ETD.Core
{
    public class BlurController : MonoBehaviour
    {
        public static BlurController Instance;

        public GameObject[] observingTargets;
        public GameObject[] elementsToDisable;

        bool[] lastSates;
         DepthOfField dof;

        public GameObject blurBG;
        public bool isMenuScene;
        bool isBeingDestroyed;
        private void OnEnable()
        {
            ActivityWatcher.OnActivityChanged += HandleActivityChange;
        }

        void HandleActivityChange(GameObject target, bool isActive)
        {
            for (int i = 0; i < observingTargets.Length; i++)
            {
                if (observingTargets[i] == target)
                {
                    OnChangeActivity(target, isActive);
                }
            }
        }

        void OnChangeActivity(GameObject target, bool isActive)
        {
            if (isBeingDestroyed) return;
            foreach (var element in elementsToDisable)
            {
                if (element == null) return;
                element.SetActive(!isActive);
            }

            if (!isMenuScene)
            {
                GetComponent<Volume>().profile.TryGet<DepthOfField>(out dof);
                if (dof != null) dof.active = isActive;
                EventBus.Publish(new TurretDeselectedEvent());
            }
            else
            {
                if (blurBG != null) blurBG.SetActive(isActive);
            }

        }

        private void OnDestroy()
        {
            isBeingDestroyed = true;
        }
    }
}
