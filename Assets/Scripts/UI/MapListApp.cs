using UnityEngine;

namespace GoG
{
    public sealed class MapListApp : MonoBehaviour
    {
        GUIStyle _actionStyle;
        GUIStyle _errorStyle;
        Texture2D _actionTex;
        Texture2D _actionHoverTex;
        string _error;
        bool _pendingNew;
        bool _pendingOpen;

        void Start()
        {
            var cam = Camera.main;
            if (cam != null)
                cam.backgroundColor = new Color(0.08f, 0.10f, 0.08f, 1f);
        }

        void Update()
        {
            if (_pendingNew)
            {
                _pendingNew = false;
                CreateNew();
            }

            if (_pendingOpen)
            {
                _pendingOpen = false;
                OpenFromDisk();
            }
        }

        void OnDestroy()
        {
            if (_actionTex != null)
                Destroy(_actionTex);
            if (_actionHoverTex != null)
                Destroy(_actionHoverTex);
        }

        void OnGUI()
        {
            EnsureStyles();
            const float buttonW = 280f;
            const float buttonH = 72f;
            float totalH = buttonH * 2f + 16f;
            if (!string.IsNullOrEmpty(_error))
                totalH += 48f;
            var area = new Rect((Screen.width - buttonW) * 0.5f, (Screen.height - totalH) * 0.5f, buttonW, totalH);
            GUILayout.BeginArea(area);
            if (GUILayout.Button("NEW", _actionStyle, GUILayout.Height(buttonH)))
                _pendingNew = true;
            GUILayout.Space(16f);
            if (GUILayout.Button("OPEN", _actionStyle, GUILayout.Height(buttonH)))
                _pendingOpen = true;
            if (!string.IsNullOrEmpty(_error))
            {
                GUILayout.Space(12f);
                GUILayout.Label(_error, _errorStyle);
            }

            GUILayout.EndArea();
        }

        void CreateNew()
        {
            _error = null;
            MapSelection.Clear();
            SceneLoad.NextFrame(MapSelection.EditorScene);
        }

        void OpenFromDisk()
        {
            _error = null;
            string path = NativeFileDialog.OpenJson("Open Map", MapFile.DefaultDirectory());
            if (string.IsNullOrEmpty(path))
                return;

            string json;
            string error;
            if (!MapFile.TryRead(path, out json, out error))
            {
                _error = error;
                return;
            }

            MapSelection.SelectFile(path, json);
            SceneLoad.NextFrame(MapSelection.EditorScene);
        }

        void EnsureStyles()
        {
            if (_actionStyle != null)
                return;

            _actionTex = Solid(new Color(0.32f, 0.28f, 0.14f, 1f));
            _actionHoverTex = Solid(new Color(0.46f, 0.40f, 0.18f, 1f));

            _errorStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                wordWrap = true,
                alignment = TextAnchor.UpperCenter
            };
            _errorStyle.normal.textColor = new Color(0.92f, 0.42f, 0.32f, 1f);

            _actionStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _actionStyle.normal.textColor = new Color(0.96f, 0.90f, 0.62f, 1f);
            _actionStyle.hover.textColor = Color.white;
            _actionStyle.normal.background = _actionTex;
            _actionStyle.hover.background = _actionHoverTex;
            _actionStyle.active.background = _actionHoverTex;
        }

        static Texture2D Solid(Color color)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { color, color, color, color });
            tex.Apply(false, true);
            return tex;
        }
    }
}
