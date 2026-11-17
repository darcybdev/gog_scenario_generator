using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoG
{
    public static class MapCatalog
    {
        public static List<string> FindParsedMaps()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/Resources" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".parsed.json", StringComparison.OrdinalIgnoreCase))
                    continue;
                const string root = "Assets/Resources/";
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    continue;
                string resource = path.Substring(root.Length, path.Length - root.Length - 5);
                names.Add(resource.Replace('\\', '/'));
            }
#endif

            if (names.Count == 0)
            {
                var assets = Resources.LoadAll<TextAsset>(string.Empty);
                for (int i = 0; i < assets.Length; i++)
                {
                    var asset = assets[i];
                    if (asset == null || string.IsNullOrEmpty(asset.name))
                        continue;
                    if (!asset.name.EndsWith(".parsed", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string text = asset.text;
                    if (string.IsNullOrEmpty(text) || !text.TrimStart().StartsWith("{"))
                        continue;
                    names.Add(asset.name);
                }
            }

            var list = new List<string>(names);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        public static string DisplayName(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName))
                return resourceName;
            string name = resourceName.Replace('\\', '/');
            int slash = name.LastIndexOf('/');
            if (slash >= 0)
                name = name.Substring(slash + 1);
            if (name.EndsWith(".parsed", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 7);
            return name.Replace('_', ' ');
        }
    }
}
