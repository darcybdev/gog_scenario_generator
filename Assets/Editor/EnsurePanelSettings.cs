#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScenarioEditor.Editor
{
    [InitializeOnLoad]
    static class EnsurePanelSettings
    {
        const string ThemePath = "Assets/UI/UnityDefaultRuntimeTheme.tss";
        const string PanelPath = "Assets/UI/PanelSettings.asset";
        const string TextPath = "Assets/UI/PanelTextSettings.asset";

        static EnsurePanelSettings()
        {
            EditorApplication.delayCall += CreateIfNeeded;
        }

        static void CreateIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
                return;

            bool dirty = false;
            if (panel.themeStyleSheet == null)
            {
                var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
                if (theme != null)
                {
                    panel.themeStyleSheet = theme;
                    dirty = true;
                }
            }

            if (panel.textSettings == null)
            {
                var text = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(TextPath);
                if (text == null)
                {
                    text = ScriptableObject.CreateInstance<PanelTextSettings>();
                    AssetDatabase.CreateAsset(text, TextPath);
                }

                panel.textSettings = text;
                dirty = true;
            }

            if (!dirty)
                return;

            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
