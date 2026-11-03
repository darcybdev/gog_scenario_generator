using System.IO;
using System.Text;
using UnityEngine;

namespace ScenarioEditor
{
    public static class ScenarioJson
    {
        public static string Save(ScenarioMap map)
        {
            string fileName = Sanitize(map.Name) + ".parsed.json";
            string dir = Path.Combine(Application.dataPath, "Resources", "Scenarios");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllText(path, ToJson(map), Encoding.UTF8);
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.AssetDatabase.Refresh();
#endif
            return path;
        }

        public static string ToJson(ScenarioMap map)
        {
            var sb = new StringBuilder(map.Width * map.Height * 180);
            int areas = map.Width * map.Height;
            string climate = map.Climate.ToString().ToLowerInvariant();

            sb.Append("{\n");
            sb.Append("  \"file\": \"").Append(Escape(map.Name)).Append(".btl\",\n");
            sb.Append("  \"climate\": \"").Append(climate).Append("\",\n");
            sb.Append("  \"map\": ").Append(ScenarioMap.TilesetId(map.Climate)).Append(",\n");
            sb.Append("  \"header\": {\n");
            sb.Append("    \"version\": 1,\n");
            sb.Append("    \"width\": ").Append(map.Width).Append(",\n");
            sb.Append("    \"height\": ").Append(map.Height).Append(",\n");
            sb.Append("    \"unknown_0C\": 1,\n");
            sb.Append("    \"num_countries\": 0,\n");
            sb.Append("    \"num_armies\": 0,\n");
            sb.Append("    \"num_dialogues\": 0,\n");
            sb.Append("    \"unknown_1C\": 0,\n");
            sb.Append("    \"turn_limit\": 0,\n");
            sb.Append("    \"unknown_24\": 0,\n");
            sb.Append("    \"unknown_28\": 1,\n");
            sb.Append("    \"unknown_2C\": 5,\n");
            sb.Append("    \"compressed\": 1,\n");
            sb.Append("    \"unknown_34\": 0,\n");
            sb.Append("    \"unknown_38\": 0,\n");
            sb.Append("    \"unknown_3C\": 0\n");
            sb.Append("  },\n");
            sb.Append("  \"countries\": [],\n");
            sb.Append("  \"notable_areas\": [\n");

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = map.Get(x, y);
                    bool last = x == map.Width - 1 && y == map.Height - 1;
                    int index = y * map.Width + x;
                    string terrainName = cell.DisplayType(map.Climate);
                    int terrainType = cell.IsSea ? 1 : 0;
                    int terrainIdx = cell.IsSea ? 1 : 0;
                    int markType = cell.MarkType;
                    if (markType == 0 && cell.IsSea)
                        markType = 1;
                    int markIdx = cell.MarkType >= 4 ? cell.MarkIdx : cell.Variant;
                    string markName = cell.MarkType >= 4
                        ? ScenarioPlaceables.MarkName(cell.MarkType)
                        : (cell.IsSea ? "sea" : terrainName);
                    string construction = ScenarioPlaceables.ConstructionName(cell.ConstructionId);
                    string installation = ScenarioPlaceables.InstallationName(cell.InstallationId);

                    sb.Append("    {\n");
                    sb.Append("      \"x\": ").Append(x).Append(",\n");
                    sb.Append("      \"y\": ").Append(y).Append(",\n");
                    sb.Append("      \"index\": ").Append(index).Append(",\n");
                    sb.Append("      \"occupying_country\": null,\n");
                    sb.Append("      \"occupying_country_name\": null,\n");
                    sb.Append("      \"terrain_type\": ").Append(terrainType).Append(",\n");
                    sb.Append("      \"terrain_name\": \"").Append(terrainName).Append("\",\n");
                    sb.Append("      \"terrain_idx\": ").Append(terrainIdx).Append(",\n");
                    sb.Append("      \"is_sea\": ").Append(cell.IsSea ? "true" : "false").Append(",\n");
                    sb.Append("      \"mark_type\": ").Append(markType).Append(",\n");
                    sb.Append("      \"mark_name\": \"").Append(Escape(markName)).Append("\",\n");
                    sb.Append("      \"mark_idx\": ").Append(markIdx).Append(",\n");
                    sb.Append("      \"construction_id\": ").Append(cell.ConstructionId).Append(",\n");
                    sb.Append("      \"construction\": ").Append(JsonStringOrNull(construction)).Append(",\n");
                    sb.Append("      \"construction_level\": ").Append(cell.ConstructionLevel).Append(",\n");
                    sb.Append("      \"installation_id\": ").Append(cell.InstallationId).Append(",\n");
                    sb.Append("      \"installation\": ").Append(JsonStringOrNull(installation)).Append(",\n");
                    sb.Append("      \"tile_variant\": ").Append(cell.Variant).Append(",\n");
                    sb.Append("      \"overlay\": ").Append(cell.Overlay).Append(",\n");
                    sb.Append("      \"byte7\": ").Append(cell.MarkType >= 4 ? cell.MarkIdx : 0).Append(",\n");
                    sb.Append("      \"road_type\": null,\n");
                    sb.Append("      \"road_idx\": null,\n");
                    sb.Append("      \"is_key\": false,\n");
                    sb.Append("      \"area_commander_id\": null,\n");
                    sb.Append("      \"name_id\": null,\n");
                    sb.Append("      \"name\": null\n");
                    sb.Append(last ? "    }\n" : "    },\n");
                }
            }

            sb.Append("  ],\n");
            sb.Append("  \"armies\": [],\n");
            sb.Append("  \"dialogues\": [],\n");
            sb.Append("  \"grid_terrain\": [\n");
            for (int y = 0; y < map.Height; y++)
            {
                sb.Append("    \"");
                for (int x = 0; x < map.Width; x++)
                    sb.Append(map.Get(x, y).IsSea ? '~' : '.');
                sb.Append(y == map.Height - 1 ? "\"\n" : "\",\n");
            }

            sb.Append("  ],\n");
            sb.Append("  \"grid_occupying_country\": [\n");
            string emptyRow = new string('.', map.Width);
            for (int y = 0; y < map.Height; y++)
                sb.Append("    \"").Append(emptyRow).Append(y == map.Height - 1 ? "\"\n" : "\",\n");
            sb.Append("  ],\n");
            sb.Append("  \"counts\": {\n");
            sb.Append("    \"areas\": ").Append(areas).Append(",\n");
            sb.Append("    \"notable_areas\": ").Append(areas).Append(",\n");
            sb.Append("    \"armies\": 0,\n");
            sb.Append("    \"key_areas\": 0,\n");
            sb.Append("    \"named_areas\": 0\n");
            sb.Append("  }\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "generated";
            var sb = new StringBuilder(name.Length);
            foreach (char c in name.Trim())
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                    sb.Append(c);
                else
                    sb.Append('_');
            }

            return sb.Length == 0 ? "generated" : sb.ToString();
        }

        static string JsonStringOrNull(string value)
        {
            return string.IsNullOrEmpty(value) ? "null" : "\"" + Escape(value) + "\"";
        }

        static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
