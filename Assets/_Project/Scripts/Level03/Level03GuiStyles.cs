using UnityEngine;

internal static class Level03GuiStyles
{
    public static GUIStyle Create(int fontSize, Color color, FontStyle fontStyle)
    {
        return new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = fontSize,
            fontStyle = fontStyle,
            normal = { textColor = color }
        };
    }
}
