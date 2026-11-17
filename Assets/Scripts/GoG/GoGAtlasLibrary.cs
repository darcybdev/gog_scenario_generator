using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using UnityEngine;

namespace GoG
{
    public sealed class GoGAtlasLibrary
    {
        readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        readonly List<Sprite> _owned = new List<Sprite>();
        readonly List<Texture2D> _ownedTextures = new List<Texture2D>();
        readonly List<string> _sheets = new List<string>();

        public float PixelsPerUnit { get; }

        public GoGAtlasLibrary(float pixelsPerUnit)
        {
            PixelsPerUnit = pixelsPerUnit;
        }

        public IEnumerable<string> LoadedSheets => _sheets;

        public Sprite FindFirst(params string[] names)
        {
            if (names == null)
                return null;
            for (int i = 0; i < names.Length; i++)
            {
                if (string.IsNullOrEmpty(names[i]))
                    continue;
                Sprite sprite = Find(names[i]);
                if (sprite != null)
                    return sprite;
            }

            return null;
        }

        public void LoadSheet(string resourceName)
        {
            LoadSheet(resourceName, null, false);
        }

        public void LoadSheet(string resourceName, string namePrefix, bool centerPivot)
        {
            var xml = LoadXml(resourceName);
            if (xml == null)
            {
                Debug.LogWarning("GoG atlas missing xml: " + resourceName);
                return;
            }

            var doc = new XmlDocument();
            doc.LoadXml("<root>" + xml.text + "</root>");
            var images = doc.SelectNodes("//Image");
            if (images == null || images.Count == 0)
                return;

            int atlasW = 0;
            int atlasH = 0;
            foreach (XmlNode node in images)
            {
                int x = AttrInt(node, "x");
                int y = AttrInt(node, "y");
                int w = AttrInt(node, "w");
                int h = AttrInt(node, "h");
                if (x + w > atlasW) atlasW = x + w;
                if (y + h > atlasH) atlasH = y + h;
            }

            if (atlasW <= 0 || atlasH <= 0)
                return;

            var texture = LoadTexture(resourceName, atlasW, atlasH);
            if (texture == null)
            {
                Debug.LogWarning("GoG atlas missing texture: " + resourceName);
                return;
            }

            if (!texture.isReadable)
            {
                Debug.LogError(
                    "Atlas '" + resourceName + "' is not readable. Enable Read/Write in the importer.");
                return;
            }

            texture = EnsureUncompressed(texture);

            float scaleX = texture.width / (float)Mathf.Max(atlasW, texture.width);
            float scaleY = texture.height / (float)Mathf.Max(atlasH, texture.height);
            if (texture.width < atlasW) scaleX = texture.width / (float)atlasW;
            if (texture.height < atlasH) scaleY = texture.height / (float)atlasH;

            foreach (XmlNode node in images)
            {
                string name = Attr(node, "name");
                if (string.IsNullOrEmpty(name))
                    continue;
                if (!string.IsNullOrEmpty(namePrefix))
                    name = namePrefix + name;
                if (_sprites.ContainsKey(name))
                    continue;

                int x = AttrInt(node, "x");
                int y = AttrInt(node, "y");
                int w = AttrInt(node, "w");
                int h = AttrInt(node, "h");
                int refx = AttrInt(node, "refx");
                int refy = AttrInt(node, "refy");
                if (w <= 0 || h <= 0)
                    continue;

                float rx = x * scaleX;
                float ry = y * scaleY;
                float rw = w * scaleX;
                float rh = h * scaleY;
                float unityY = texture.height - ry - rh;
                var rect = new Rect(rx, unityY, rw, rh);
                if (rect.xMin < 0f || rect.yMin < 0f || rect.xMax > texture.width + 0.5f ||
                    rect.yMax > texture.height + 0.5f)
                    continue;

                Vector2 pivot;
                if (centerPivot || (refx == 0 && refy == 0))
                    pivot = new Vector2(0.5f, 0.5f);
                else
                    pivot = new Vector2(refx / (float)w, 1f - refy / (float)h);

                var sprite = Sprite.Create(texture, rect, pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = name;
                _sprites[name] = sprite;
                _owned.Add(sprite);
            }

            _sheets.Add(resourceName);
        }

        static Texture2D LoadTexture(string resourceName, int atlasW, int atlasH)
        {
#if UNITY_EDITOR
            var png = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Resources/" + resourceName + ".png");
            if (png != null)
                return png;
#endif
            // PNG atlases are imported as Sprite; PVR files with the same Resources
            // name are IHV Texture2D only. Editor always bound the PNG. A player
            // Resources.Load<Texture2D> can bind the PVR instead and slice wrong tiles.
            var sprite = Resources.Load<Sprite>(resourceName);
            if (sprite != null && sprite.texture != null)
                return sprite.texture;

            var loaded = Resources.LoadAll<Texture2D>(resourceName);
            if (loaded == null || loaded.Length == 0)
                return Resources.Load<Texture2D>(resourceName);
            if (loaded.Length == 1)
                return loaded[0];

            Texture2D best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < loaded.Length; i++)
            {
                var candidate = loaded[i];
                if (candidate == null)
                    continue;
                int score = 0;
                if (candidate.wrapMode == TextureWrapMode.Clamp)
                    score += 10000;
                score -= Mathf.Abs(candidate.width - atlasW) + Mathf.Abs(candidate.height - atlasH);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best != null ? best : loaded[0];
        }

        static TextAsset LoadXml(string resourceName)
        {
#if UNITY_EDITOR
            var fromPath = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Resources/" + resourceName + ".xml");
            if (fromPath != null)
                return fromPath;
#endif
            var xml = Resources.Load<TextAsset>(resourceName);
            if (xml != null && xml.text.IndexOf("<Image", System.StringComparison.Ordinal) >= 0)
                return xml;

            var loaded = Resources.LoadAll<TextAsset>(resourceName);
            if (loaded == null)
                return xml;
            for (int i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] != null &&
                    loaded[i].text.IndexOf("<Image", System.StringComparison.Ordinal) >= 0)
                    return loaded[i];
            }

            return xml;
        }

        Texture2D EnsureUncompressed(Texture2D source)
        {
            if (source.format == TextureFormat.RGBA32 || source.format == TextureFormat.ARGB32)
                return source;

            Color[] pixels = source.GetPixels();
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.name = source.name;
            copy.filterMode = source.filterMode;
            copy.wrapMode = TextureWrapMode.Clamp;
            copy.SetPixels(pixels);
            copy.Apply(false, false);
            _ownedTextures.Add(copy);
            return copy;
        }

        public Sprite Find(string imageName)
        {
            if (string.IsNullOrEmpty(imageName))
                return null;
            _sprites.TryGetValue(imageName, out var sprite);
            return sprite;
        }

        public void Dispose()
        {
            DestroyOwned(_owned);
            DestroyOwned(_ownedTextures);
            _sprites.Clear();
            _sheets.Clear();
        }

        static void DestroyOwned<T>(List<T> items) where T : Object
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                    continue;
                if (Application.isPlaying)
                    Object.Destroy(items[i]);
                else
                    Object.DestroyImmediate(items[i]);
            }

            items.Clear();
        }

        static string Attr(XmlNode node, string name)
        {
            return node.Attributes != null && node.Attributes[name] != null
                ? node.Attributes[name].Value
                : null;
        }

        static int AttrInt(XmlNode node, string name)
        {
            string value = Attr(node, name);
            if (string.IsNullOrEmpty(value))
                return 0;
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed);
            return parsed;
        }
    }

    public sealed class GoGTerrainCatalog
    {
        readonly List<string[]> _byType = new List<string[]>();

        public static readonly string[] TypeNames =
        {
            "land", "sea", "coast", "boreal forest", "temperate forest",
            "tropical forest", "desert forest", "green hills", "green mountains",
            "poor hills", "poor mountains", "snow mountains", "swamp", "farmland"
        };

        // CArea::GetTerrainType / GenerateTile IDs are 1-based against
        // terraindef.xml: 1 land, 2 sea, 3 coast, 4 boreal … 14 farmland.
        // File base land/sea stays 0/1 when decoration is 16.
        public static int FromGameType(int gameType)
        {
            return gameType <= 2 ? gameType : gameType - 1;
        }

        public static GoGTerrainCatalog Load()
        {
            var catalog = new GoGTerrainCatalog();
            var xml = Resources.Load<TextAsset>("terraindef");
            if (xml == null)
                return catalog;

            var doc = new XmlDocument();
            doc.LoadXml(xml.text);
            foreach (string typeName in TypeNames)
            {
                var terrain = doc.SelectSingleNode("/terrains/terrain[@name='" + typeName + "']");
                var tiles = new List<string>();
                if (terrain != null)
                {
                    var nodes = terrain.SelectNodes("tile");
                    if (nodes != null)
                    {
                        foreach (XmlNode tile in nodes)
                        {
                            string image = tile.Attributes != null && tile.Attributes["image"] != null
                                ? tile.Attributes["image"].Value
                                : null;
                            tiles.Add(image);
                        }
                    }
                }

                catalog._byType.Add(tiles.ToArray());
            }

            return catalog;
        }

        public string ImageName(int terrainType, int index)
        {
            if (terrainType < 0 || terrainType >= _byType.Count)
                return null;
            var tiles = _byType[terrainType];
            if (index < 0 || index >= tiles.Length)
                return null;
            return tiles[index];
        }

        public string[] Images(int terrainType)
        {
            if (terrainType < 0 || terrainType >= _byType.Count)
                return new string[0];
            return _byType[terrainType];
        }
    }
}
