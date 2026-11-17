using UnityEngine;

namespace GoG
{
    public sealed class MapViewHud : MonoBehaviour
    {
        GUIStyle _buttonStyle;
        GUIStyle _labelStyle;
        Texture2D _buttonTex;

        void OnDestroy()
        {
            if (_buttonTex != null)
                Destroy(_buttonTex);
        }

        void OnGUI()
        {
            EnsureStyles();
            string title = MapSelection.Title;
            GUILayout.BeginArea(new Rect(16f, 12f, 520f, 72f));
            if (GUILayout.Button("BACK TO LIST", _buttonStyle, GUILayout.Height(32f), GUILayout.Width(160f)))
                SceneLoad.NextFrame(MapSelection.ListScene);
            if (!string.IsNullOrEmpty(title))
                GUILayout.Label(title, _labelStyle);
            GUILayout.EndArea();
        }

        void EnsureStyles()
        {
            if (_buttonStyle != null)
                return;

            _buttonTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var color = new Color(0.18f, 0.21f, 0.14f, 0.92f);
            _buttonTex.SetPixels(new[] { color, color, color, color });
            _buttonTex.Apply(false, true);

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            _buttonStyle.normal.textColor = new Color(0.93f, 0.89f, 0.71f, 1f);
            _buttonStyle.normal.background = _buttonTex;
            _buttonStyle.hover.background = _buttonTex;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            _labelStyle.normal.textColor = new Color(0.93f, 0.91f, 0.84f, 1f);
        }
    }
}
