using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace GoG
{
    public static class MapFile
    {
        const int BlankWidth = 24;
        const int BlankHeight = 20;

        [Serializable]
        class Probe
        {
            public Header header;
            public string[] grid_terrain;
            public Area[] notable_areas;
            public Army[] armies;
        }

        [Serializable]
        class Header
        {
            public int width;
            public int height;
        }

        [Serializable]
        class Area
        {
            public int x;
            public int y;
        }

        [Serializable]
        class Army
        {
            public int x;
            public int y;
        }

        public static string DefaultDirectory()
        {
            string resources = Path.Combine(Application.dataPath, "Resources");
            if (Directory.Exists(resources))
                return resources;

            return Application.dataPath;
        }

        public static string BlankJson()
        {
            var sb = new StringBuilder(BlankWidth * BlankHeight + 128);
            sb.Append("{\"climate\":\"land\",\"header\":{\"width\":");
            sb.Append(BlankWidth);
            sb.Append(",\"height\":");
            sb.Append(BlankHeight);
            sb.Append("},\"notable_areas\":[],\"armies\":[],\"grid_terrain\":[");
            for (int y = 0; y < BlankHeight; y++)
            {
                if (y > 0)
                    sb.Append(',');
                sb.Append('"');
                sb.Append('.', BlankWidth);
                sb.Append('"');
            }

            sb.Append("]}");
            return sb.ToString();
        }

        public static bool TryRead(string path, out string json, out string error)
        {
            json = null;
            error = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                error = "File not found.";
                return false;
            }

            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception)
            {
                error = "Could not read that file.";
                return false;
            }

            return TryValidate(json, out error);
        }

        public static bool TryValidate(string json, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "File is empty.";
                return false;
            }

            string trim = json.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            if (!trim.StartsWith("{", StringComparison.Ordinal))
            {
                error = "This is not a JSON map file.";
                return false;
            }

            Probe map;
            try
            {
                map = JsonUtility.FromJson<Probe>(json);
            }
            catch (Exception)
            {
                error = "Could not parse JSON.";
                return false;
            }

            if (map == null || map.header == null || map.header.width <= 0 || map.header.height <= 0)
            {
                error = "This JSON is not a map file.";
                return false;
            }

            if (map.grid_terrain == null && (map.notable_areas == null || map.notable_areas.Length == 0))
            {
                error = "This JSON is not a map file.";
                return false;
            }

            return true;
        }
    }
}
