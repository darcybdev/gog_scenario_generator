using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ScenarioEditor
{
    public static class ScenarioJson
    {
        [Serializable]
        class BattleFile
        {
            public string climate;
            public HeaderFile header;
            public AreaFile[] notable_areas;
            public string[] grid_terrain;
        }

        [Serializable]
        class HeaderFile
        {
            public int width;
            public int height;
        }

        [Serializable]
        class AreaFile
        {
            public int x;
            public int y;
            public bool is_sea;
            public int tile_variant;
            public int mark_type;
            public int mark_idx;
            public int construction_id;
            public int construction_level;
            public int installation_id;
        }

        public static bool TryLoad(string json, string name, out ScenarioMap map, out string error)
        {
            map = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "File is empty.";
                return false;
            }

            BattleFile file;
            try
            {
                file = JsonUtility.FromJson<BattleFile>(json);
            }
            catch (Exception)
            {
                error = "Could not parse JSON.";
                return false;
            }

            if (file == null || file.header == null || file.header.width <= 0 || file.header.height <= 0)
            {
                error = "This JSON is not a map file.";
                return false;
            }

            int width = file.header.width;
            int height = file.header.height;
            if (string.IsNullOrWhiteSpace(name))
                name = "opened";
            map = new ScenarioMap(width, height, ParseClimate(file.climate), name);
            map.SourceJson = json;

            if (file.grid_terrain == null)
                file.grid_terrain = new string[0];

            for (int y = 0; y < height; y++)
            {
                string row = y < file.grid_terrain.Length ? file.grid_terrain[y] : string.Empty;
                for (int x = 0; x < width; x++)
                {
                    bool sea = x < row.Length && row[x] == '~';
                    int mix = Mathf.Abs(x * 7 + y * 13 + x * y * 3);
                    int variant = sea ? mix % 7 : mix % 15;
                    map.Set(x, y, new HexCell
                    {
                        IsSea = sea,
                        Variant = sea ? 0 : variant,
                        MarkType = sea ? 1 : 0,
                        MarkIdx = sea ? 0 : variant
                    });
                }
            }

            if (file.notable_areas == null)
                return true;

            for (int i = 0; i < file.notable_areas.Length; i++)
            {
                var area = file.notable_areas[i];
                if (area.x < 0 || area.y < 0 || area.x >= width || area.y >= height)
                    continue;
                map.Set(area.x, area.y, new HexCell
                {
                    IsSea = area.is_sea,
                    Variant = area.tile_variant,
                    MarkType = area.mark_type,
                    MarkIdx = area.mark_idx,
                    ConstructionId = area.construction_id,
                    ConstructionLevel = area.construction_level,
                    InstallationId = area.installation_id
                });
            }

            return true;
        }

        static LandClimate ParseClimate(string climate)
        {
            if (string.IsNullOrEmpty(climate))
                return LandClimate.Land;
            switch (climate.Trim().ToLowerInvariant())
            {
                case "sand":
                    return LandClimate.Sand;
                case "snow":
                    return LandClimate.Snow;
                case "water":
                    return LandClimate.Water;
                default:
                    return LandClimate.Land;
            }
        }

        public static string Save(ScenarioMap map, string path = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                string fileName = Sanitize(map.Name) + ".parsed.json";
                string dir = Path.Combine(Application.dataPath, "Resources", "Scenarios");
                Directory.CreateDirectory(dir);
                path = Path.Combine(dir, fileName);
            }

            string json = TerrainSaveJson(map, path);
            File.WriteAllText(path, json, Encoding.UTF8);
            map.SourceJson = json;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.AssetDatabase.Refresh();
#endif
            return path;
        }

        static string TerrainSaveJson(ScenarioMap map, string path)
        {
            string existing = null;
            if (File.Exists(path))
            {
                try
                {
                    existing = File.ReadAllText(path, Encoding.UTF8);
                }
                catch (Exception)
                {
                    existing = null;
                }
            }

            if (string.IsNullOrEmpty(existing))
                existing = map.SourceJson;

            if (!string.IsNullOrEmpty(existing) && LooksLikeMapJson(existing))
            {
                string patched = PatchTerrain(existing, map);
                if (!string.IsNullOrEmpty(patched))
                    return patched;
            }

            return ToJson(map);
        }

        static bool LooksLikeMapJson(string json)
        {
            return json.IndexOf("\"grid_terrain\"", StringComparison.Ordinal) >= 0
                || json.IndexOf("\"notable_areas\"", StringComparison.Ordinal) >= 0;
        }

        static string PatchTerrain(string json, ScenarioMap map)
        {
            string climate = map.Climate.ToString().ToLowerInvariant();
            json = ReplaceRootValue(json, "climate", JsonString(climate));
            json = ReplaceRootValue(json, "map", ScenarioMap.TilesetId(map.Climate).ToString());
            json = ReplaceRootValue(json, "grid_terrain", GridTerrainArray(map));
            json = PatchNotableAreas(json, map);
            return json;
        }

        static string PatchNotableAreas(string json, ScenarioMap map)
        {
            int keyPos = FindRootKey(json, "notable_areas");
            if (keyPos < 0)
                return json;

            int colon = json.IndexOf(':', keyPos);
            if (colon < 0)
                return json;
            int vs = SkipWs(json, colon + 1);
            if (vs >= json.Length || json[vs] != '[')
                return json;
            int ve = EndOfJsonValue(json, vs);
            string array = json.Substring(vs, ve - vs);
            var objects = ExtractObjects(array);
            var seen = new bool[map.Width * map.Height];
            var patched = new List<string>(objects.Count + 8);

            for (int i = 0; i < objects.Count; i++)
            {
                string obj = objects[i];
                int x = ReadIntField(obj, "x", int.MinValue);
                int y = ReadIntField(obj, "y", int.MinValue);
                if (!map.InBounds(x, y))
                {
                    patched.Add(obj);
                    continue;
                }

                seen[y * map.Width + x] = true;
                patched.Add(ApplyTerrainToArea(obj, map, x, y));
            }

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    if (seen[y * map.Width + x])
                        continue;
                    var cell = map.Get(x, y);
                    if (cell.MarkType < 4 && cell.ConstructionId <= 0 && cell.InstallationId <= 0)
                        continue;
                    patched.Add(NewAreaObject(map, x, y));
                }
            }

            string rebuilt = RebuildObjectArray(array, patched);
            return json.Substring(0, vs) + rebuilt + json.Substring(ve);
        }

        static string ApplyTerrainToArea(string obj, ScenarioMap map, int x, int y)
        {
            FillTerrain(map, x, y, out int terrainType, out string terrainName, out int terrainIdx,
                out bool isSea, out int markType, out string markName, out int markIdx,
                out int constructionId, out string construction, out int constructionLevel,
                out int installationId, out string installation, out int variant, out int overlay,
                out int byte7);

            obj = SetField(obj, "terrain_type", terrainType.ToString());
            obj = SetField(obj, "terrain_name", JsonString(terrainName));
            obj = SetField(obj, "terrain_idx", terrainIdx.ToString());
            obj = SetField(obj, "is_sea", isSea ? "true" : "false");
            obj = SetField(obj, "mark_type", markType.ToString());
            obj = SetField(obj, "mark_name", JsonString(markName));
            obj = SetField(obj, "mark_idx", markIdx.ToString());
            obj = SetField(obj, "construction_id", constructionId.ToString());
            obj = SetField(obj, "construction", JsonStringOrNull(construction));
            obj = SetField(obj, "construction_level", constructionLevel.ToString());
            obj = SetField(obj, "installation_id", installationId.ToString());
            obj = SetField(obj, "installation", JsonStringOrNull(installation));
            obj = SetField(obj, "tile_variant", variant.ToString());
            obj = SetField(obj, "overlay", overlay.ToString());
            obj = SetField(obj, "byte7", byte7.ToString());
            return obj;
        }

        static string NewAreaObject(ScenarioMap map, int x, int y)
        {
            var sb = new StringBuilder(280);
            int index = y * map.Width + x;
            FillTerrain(map, x, y, out int terrainType, out string terrainName, out int terrainIdx,
                out bool isSea, out int markType, out string markName, out int markIdx,
                out int constructionId, out string construction, out int constructionLevel,
                out int installationId, out string installation, out int variant, out int overlay,
                out int byte7);
            sb.Append("{\n");
            sb.Append("      \"x\": ").Append(x).Append(",\n");
            sb.Append("      \"y\": ").Append(y).Append(",\n");
            sb.Append("      \"index\": ").Append(index).Append(",\n");
            sb.Append("      \"terrain_type\": ").Append(terrainType).Append(",\n");
            sb.Append("      \"terrain_name\": ").Append(JsonString(terrainName)).Append(",\n");
            sb.Append("      \"terrain_idx\": ").Append(terrainIdx).Append(",\n");
            sb.Append("      \"is_sea\": ").Append(isSea ? "true" : "false").Append(",\n");
            sb.Append("      \"mark_type\": ").Append(markType).Append(",\n");
            sb.Append("      \"mark_name\": ").Append(JsonString(markName)).Append(",\n");
            sb.Append("      \"mark_idx\": ").Append(markIdx).Append(",\n");
            sb.Append("      \"construction_id\": ").Append(constructionId).Append(",\n");
            sb.Append("      \"construction\": ").Append(JsonStringOrNull(construction)).Append(",\n");
            sb.Append("      \"construction_level\": ").Append(constructionLevel).Append(",\n");
            sb.Append("      \"installation_id\": ").Append(installationId).Append(",\n");
            sb.Append("      \"installation\": ").Append(JsonStringOrNull(installation)).Append(",\n");
            sb.Append("      \"tile_variant\": ").Append(variant).Append(",\n");
            sb.Append("      \"overlay\": ").Append(overlay).Append(",\n");
            sb.Append("      \"byte7\": ").Append(byte7).Append("\n");
            sb.Append("    }");
            return sb.ToString();
        }

        static void FillTerrain(ScenarioMap map, int x, int y,
            out int terrainType, out string terrainName, out int terrainIdx,
            out bool isSea, out int markType, out string markName, out int markIdx,
            out int constructionId, out string construction, out int constructionLevel,
            out int installationId, out string installation, out int variant, out int overlay,
            out int byte7)
        {
            var cell = map.Get(x, y);
            terrainName = cell.DisplayType(map.Climate);
            terrainType = cell.IsSea ? 1 : 0;
            terrainIdx = cell.IsSea ? 1 : 0;
            isSea = cell.IsSea;
            markType = cell.MarkType;
            if (markType == 0 && cell.IsSea)
                markType = 1;
            markIdx = cell.MarkType >= 4 ? cell.MarkIdx : cell.Variant;
            markName = cell.MarkType >= 4
                ? ScenarioPlaceables.MarkName(cell.MarkType)
                : (cell.IsSea ? "sea" : terrainName);
            constructionId = cell.ConstructionId;
            construction = ScenarioPlaceables.ConstructionName(cell.ConstructionId);
            constructionLevel = cell.ConstructionLevel;
            installationId = cell.InstallationId;
            installation = ScenarioPlaceables.InstallationName(cell.InstallationId);
            variant = cell.Variant;
            overlay = cell.Overlay;
            byte7 = cell.MarkType >= 4 ? cell.MarkIdx : 0;
        }

        static string GridTerrainArray(ScenarioMap map)
        {
            var sb = new StringBuilder(map.Height * (map.Width + 8) + 16);
            sb.Append("[\n");
            for (int y = 0; y < map.Height; y++)
            {
                sb.Append("    \"");
                for (int x = 0; x < map.Width; x++)
                    sb.Append(map.Get(x, y).IsSea ? '~' : '.');
                sb.Append(y == map.Height - 1 ? "\"\n" : "\",\n");
            }

            sb.Append("  ]");
            return sb.ToString();
        }

        static string RebuildObjectArray(string originalArray, List<string> objects)
        {
            var sb = new StringBuilder(originalArray.Length + 64);
            sb.Append("[\n");
            for (int i = 0; i < objects.Count; i++)
            {
                sb.Append("    ");
                sb.Append(objects[i].Trim());
                sb.Append(i == objects.Count - 1 ? "\n" : ",\n");
            }

            sb.Append("  ]");
            return sb.ToString();
        }

        static List<string> ExtractObjects(string array)
        {
            var list = new List<string>();
            int i = array.IndexOf('[');
            if (i < 0)
                return list;
            i = SkipWs(array, i + 1);
            while (i < array.Length)
            {
                i = SkipWs(array, i);
                if (i >= array.Length || array[i] == ']')
                    break;
                if (array[i] == ',')
                {
                    i++;
                    continue;
                }

                if (array[i] == '{')
                {
                    int end = EndOfJsonValue(array, i);
                    list.Add(array.Substring(i, end - i));
                    i = end;
                    continue;
                }

                i++;
            }

            return list;
        }

        static string ReplaceRootValue(string json, string key, string literal)
        {
            int keyPos = FindRootKey(json, key);
            if (keyPos < 0)
                return json;
            int colon = json.IndexOf(':', keyPos);
            if (colon < 0)
                return json;
            int vs = SkipWs(json, colon + 1);
            int ve = EndOfJsonValue(json, vs);
            if (ve < vs)
                return json;
            return json.Substring(0, vs) + literal + json.Substring(ve);
        }

        static string SetField(string obj, string key, string literal)
        {
            int keyPos = FindRootKey(obj, key);
            if (keyPos >= 0)
            {
                int colon = obj.IndexOf(':', keyPos);
                if (colon < 0)
                    return obj;
                int vs = SkipWs(obj, colon + 1);
                int ve = EndOfJsonValue(obj, vs);
                return obj.Substring(0, vs) + literal + obj.Substring(ve);
            }

            int close = obj.LastIndexOf('}');
            if (close < 0)
                return obj;
            int k = close - 1;
            while (k >= 0 && char.IsWhiteSpace(obj[k]))
                k--;
            string insert = "\n      \"" + key + "\": " + literal;
            if (k >= 0 && obj[k] != '{' && obj[k] != ',')
                insert = "," + insert;
            return obj.Substring(0, close) + insert + obj.Substring(close);
        }

        static int ReadIntField(string obj, string key, int fallback)
        {
            int keyPos = FindRootKey(obj, key);
            if (keyPos < 0)
                return fallback;
            int colon = obj.IndexOf(':', keyPos);
            if (colon < 0)
                return fallback;
            int vs = SkipWs(obj, colon + 1);
            int ve = EndOfJsonValue(obj, vs);
            int.TryParse(obj.Substring(vs, ve - vs).Trim(), out int parsed);
            return parsed;
        }

        static int FindRootKey(string json, string key)
        {
            int brace = json.IndexOf('{');
            if (brace < 0)
                return -1;
            int i = SkipWs(json, brace + 1);
            while (i < json.Length)
            {
                i = SkipWs(json, i);
                if (i >= json.Length)
                    break;
                if (json[i] == '}')
                    break;
                if (json[i] == ',')
                {
                    i++;
                    continue;
                }

                if (json[i] != '"')
                    break;

                int keyStart = i;
                string found = ReadJsonString(json, ref i);
                i = SkipWs(json, i);
                if (i < json.Length && json[i] == ':')
                    i++;
                i = SkipWs(json, i);
                if (found == key)
                    return keyStart;
                i = EndOfJsonValue(json, i);
            }

            return -1;
        }

        static string ReadJsonString(string json, ref int i)
        {
            if (i >= json.Length || json[i] != '"')
                return "";
            i++;
            var sb = new StringBuilder();
            while (i < json.Length)
            {
                char c = json[i++];
                if (c == '\\' && i < json.Length)
                {
                    sb.Append(json[i++]);
                    continue;
                }

                if (c == '"')
                    break;
                sb.Append(c);
            }

            return sb.ToString();
        }

        static int EndOfJsonValue(string json, int i)
        {
            i = SkipWs(json, i);
            if (i >= json.Length)
                return json.Length;
            char c = json[i];
            if (c == '"')
            {
                i++;
                while (i < json.Length)
                {
                    char ch = json[i++];
                    if (ch == '\\' && i < json.Length)
                    {
                        i++;
                        continue;
                    }

                    if (ch == '"')
                        break;
                }

                return i;
            }

            if (c == '{' || c == '[')
            {
                char open = c;
                char close = c == '{' ? '}' : ']';
                int depth = 1;
                i++;
                while (i < json.Length && depth > 0)
                {
                    char ch = json[i++];
                    if (ch == '"')
                    {
                        i--;
                        i = EndOfJsonValue(json, i);
                        continue;
                    }

                    if (ch == open)
                        depth++;
                    else if (ch == close)
                        depth--;
                }

                return i;
            }

            if (c == 't' || c == 'f' || c == 'n')
            {
                while (i < json.Length && char.IsLetter(json[i]))
                    i++;
                return i;
            }

            if (c == '-' || char.IsDigit(c))
            {
                i++;
                while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' || json[i] == 'e' ||
                       json[i] == 'E' || json[i] == '+' || json[i] == '-'))
                    i++;
                return i;
            }

            return i + 1;
        }

        static int SkipWs(string json, int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i]))
                i++;
            return i;
        }

        static string JsonString(string value)
        {
            return "\"" + Escape(value) + "\"";
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
