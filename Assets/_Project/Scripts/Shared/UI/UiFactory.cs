using UnityEngine;
using UnityEngine.UI;

namespace TinyDragon.Shared.UI
{
    public static class UiFactory
    {
        public static RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject rectObject = new GameObject(objectName);
            rectObject.transform.SetParent(parent, false);
            return rectObject.AddComponent<RectTransform>();
        }

        public static void SetTopLeftRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        public static Text CreateText(string objectName, Transform parent, int fontSize, FontStyle fontStyle, Color color)
        {
            RectTransform rectTransform = CreateRect(objectName, parent);
            Text text = rectTransform.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        public static Shadow AddShadow(GameObject target, Color effectColor, Vector2 effectDistance)
        {
            Shadow shadow = target.AddComponent<Shadow>();
            shadow.effectColor = effectColor;
            shadow.effectDistance = effectDistance;
            return shadow;
        }
    }
}
