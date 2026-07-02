using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ETD.UI
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    public class UIInteractiveEffect : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [Header("Scale")]
        [SerializeField] private bool useScale = true;
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField] private float pressedScale = 0.95f;
        [SerializeField] private float speed = 14f;

        [Header("Color")]
        [SerializeField] private bool useColor = true;

        [Tooltip("Leave empty if you want to auto-find Graphics.")]
        [SerializeField] private List<Graphic> targetGraphics = new List<Graphic>();

        [SerializeField] private bool includeChildrenGraphics = false;

        [SerializeField] private Color hoverColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        [SerializeField] private Color pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        private Vector3 originalScale;
        private Vector3 targetScale;

        private readonly List<Color> normalColors = new List<Color>();
        private readonly List<Color> targetColors = new List<Color>();

        private bool isHovering;

        private void Awake()
        {
            originalScale = transform.localScale;
            targetScale = originalScale;

            SetupGraphics();
        }

        private void OnEnable()
        {
            ResetAll();
        }
        private void SetupGraphics()
        {
            if (targetGraphics.Count == 0)
            {
                if (includeChildrenGraphics)
                {
                    targetGraphics.AddRange(GetComponentsInChildren<Graphic>(true));
                }
                else
                {
                    Graphic graphic = GetComponent<Graphic>();

                    if (graphic != null)
                        targetGraphics.Add(graphic);
                }
            }

            normalColors.Clear();
            targetColors.Clear();

            for (int i = 0; i < targetGraphics.Count; i++)
            {
                if (targetGraphics[i] == null)
                    continue;

                normalColors.Add(targetGraphics[i].color);
                targetColors.Add(targetGraphics[i].color);
            }
        }

        private void Update()
        {
            if (useScale)
            {
                transform.localScale = Vector3.Lerp(
                    transform.localScale,
                    targetScale,
                    Time.unscaledDeltaTime * speed
                );
            }

            if (useColor)
            {
                for (int i = 0; i < targetGraphics.Count; i++)
                {
                    if (targetGraphics[i] == null)
                        continue;

                    if (i >= targetColors.Count)
                        continue;

                    targetGraphics[i].color = Color.Lerp(
                        targetGraphics[i].color,
                        targetColors[i],
                        Time.unscaledDeltaTime * speed
                    );
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovering = true;

            if (useScale)
                targetScale = originalScale * hoverScale;

            SetTargetColorForAll(hoverColor);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;

            if (useScale)
                targetScale = originalScale;

            SetTargetColorsToNormal();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (useScale)
                targetScale = originalScale * pressedScale;

            SetTargetColorForAll(pressedColor);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (isHovering)
            {
                if (useScale)
                    targetScale = originalScale * hoverScale;

                SetTargetColorForAll(hoverColor);
            }
            else
            {
                if (useScale)
                    targetScale = originalScale;

                SetTargetColorsToNormal();
            }
        }

        public void ResetAll()
        {
            targetScale = originalScale;
            transform.localScale = originalScale;

            SetTargetColorsToNormal();

        }

        private void SetTargetColorForAll(Color color)
        {
            if (!useColor)
                return;

            for (int i = 0; i < targetColors.Count; i++)
            {
                targetColors[i] = color;
            }
        }

        private void SetTargetColorsToNormal()
        {
            if (!useColor)
                return;

            for (int i = 0; i < targetColors.Count; i++)
            {
                if (i < normalColors.Count)
                    targetColors[i] = normalColors[i];
            }
        }
    }
}
