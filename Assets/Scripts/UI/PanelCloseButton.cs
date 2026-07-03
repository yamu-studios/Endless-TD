// ============================================================================
// ETD.UI - PanelCloseButton.cs
// Creates a small "X" close button in the top-right corner of a panel at
// runtime. Used by info/stat panels whose prefabs have no authored close
// button, so no scene/prefab editing is required.
// ============================================================================
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ETD.UI
{
    public static class PanelCloseButton
    {
        private const string ButtonName = "[RuntimeCloseButton]";

        /// <summary>
        /// Ensures the panel has a close button wired to <paramref name="onClose"/>.
        /// Safe to call multiple times; re-wires the existing button if present.
        /// </summary>
        public static void Ensure(GameObject panel, UnityAction onClose)
        {
            if (panel == null || onClose == null)
                return;

            Transform existing = panel.transform.Find(ButtonName);
            if (existing != null)
            {
                Button existingButton = existing.GetComponent<Button>();
                if (existingButton != null)
                {
                    existingButton.onClick.RemoveAllListeners();
                    existingButton.onClick.AddListener(onClose);
                }

                existing.SetAsLastSibling();
                return;
            }

            var buttonGO = new GameObject(ButtonName, typeof(RectTransform));
            buttonGO.transform.SetParent(panel.transform, false);
            buttonGO.layer = panel.layer;

            var rect = (RectTransform)buttonGO.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(26f, 26f);
            rect.anchoredPosition = new Vector2(-4f, -4f);

            Image background = buttonGO.AddComponent<Image>();
            background.color = new Color(0.16f, 0.16f, 0.2f, 0.9f);

            Button button = buttonGO.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.75f, 0.22f, 0.22f, 1f);
            colors.pressedColor = new Color(0.55f, 0.12f, 0.12f, 1f);
            button.colors = colors;
            button.onClick.AddListener(onClose);

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(buttonGO.transform, false);
            labelGO.layer = panel.layer;

            var labelRect = (RectTransform)labelGO.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = "X";
            label.fontSize = 15f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.92f, 0.92f, 0.95f, 1f);
            label.raycastTarget = false;

            // Render above the panel's other content (and any full-panel drag
            // handle) so the click always lands on the button.
            buttonGO.transform.SetAsLastSibling();
        }
    }
}
