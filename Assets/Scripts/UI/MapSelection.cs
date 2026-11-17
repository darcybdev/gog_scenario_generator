using System.IO;

namespace GoG
{
    public static class MapSelection
    {
        public const string ListScene = "Main";
        public const string ViewScene = "MapView";
        public const string EditorScene = "ScenarioEditor";

        public static string ResourceName { get; private set; }
        public static string JsonText { get; private set; }
        public static string FilePath { get; private set; }

        public static string Title
        {
            get
            {
                if (!string.IsNullOrEmpty(FilePath))
                {
                    string name = Path.GetFileNameWithoutExtension(FilePath);
                    if (name.EndsWith(".parsed", System.StringComparison.OrdinalIgnoreCase))
                        name = name.Substring(0, name.Length - 7);
                    return name.Replace('_', ' ');
                }

                if (!string.IsNullOrEmpty(ResourceName))
                    return MapCatalog.DisplayName(ResourceName);

                if (!string.IsNullOrEmpty(JsonText))
                    return "New Map";

                return string.Empty;
            }
        }

        public static void SelectResource(string resource)
        {
            ResourceName = resource;
            JsonText = null;
            FilePath = null;
        }

        public static void SelectFile(string path, string json)
        {
            FilePath = path;
            JsonText = json;
            ResourceName = null;
        }

        public static void SelectNew(string json)
        {
            FilePath = null;
            JsonText = json;
            ResourceName = null;
        }

        public static void Clear()
        {
            ResourceName = null;
            JsonText = null;
            FilePath = null;
        }
    }
}
