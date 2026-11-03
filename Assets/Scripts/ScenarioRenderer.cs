using GoG;
using UnityEngine;

namespace ScenarioEditor
{
    public sealed class ScenarioRenderer
    {
        const int SortLand = 0;
        const int SortForest = 400;
        const int SortSea = 1000;
        const int SortCoast = 1200;
        const int SortBuilding = 1400;
        const int SortSelect = 3000;
        const int LandVariants = 15;
        const int SeaVariants = 7;

        static readonly int[] PortLandMasks = { 0x0E, 0x1C, 0x38, 0x31, 0x23 };
        static readonly int[] PortLandFacing = { 2, 3, 4, 5, 6 };

        readonly float _pixelsPerUnit;
        readonly Transform _root;
        GoGAtlasLibrary _atlas;
        GoGTerrainCatalog _catalog;
        ScenarioMap _map;
        Transform _tilesRoot;
        SpriteRenderer _selection;
        Sprite _selectionSprite;
        Texture2D _selectionTexture;

        public ScenarioRenderer(Transform root, float pixelsPerUnit)
        {
            _root = root;
            _pixelsPerUnit = pixelsPerUnit > 0f ? pixelsPerUnit : 100f;
        }

        public ScenarioMap Map => _map;

        public void Show(ScenarioMap map)
        {
            Clear();
            _map = map;
            _atlas = new GoGAtlasLibrary(_pixelsPerUnit);
            if (map.Climate != LandClimate.Water)
                _atlas.LoadSheet(ScenarioMap.LandSheet(map.Climate));
            _atlas.LoadSheet("tiles_sea_hd");
            _atlas.LoadSheet(ScenarioMap.CoastSheet(map.Climate));
            _atlas.LoadSheet("tiles_hd");
            _atlas.LoadSheet("snowless_hd");
            _catalog = GoGTerrainCatalog.Load();

            RebuildTiles();
        }

        public void RefreshTiles()
        {
            RebuildTiles();
        }

        public Sprite FindSprite(params string[] names)
        {
            return _atlas != null ? _atlas.FindFirst(names) : null;
        }

        public void Select(int x, int y)
        {
            if (_map == null || !_map.InBounds(x, y))
            {
                if (_selection != null)
                    _selection.enabled = false;
                return;
            }

            if (_selection == null)
            {
                var go = new GameObject("Selection");
                go.transform.SetParent(_root, false);
                _selection = go.AddComponent<SpriteRenderer>();
                _selection.sortingOrder = SortSelect;
                _selectionSprite = CreateSelectionSprite();
                _selection.sprite = _selectionSprite;
            }

            GoGHexGrid.CellCenter(x, y, out float cx, out float cy);
            _selection.transform.position = GoGHexGrid.ToUnity(cx, cy, _pixelsPerUnit);
            _selection.enabled = true;
        }

        public void ClearSelection()
        {
            if (_selection != null)
                _selection.enabled = false;
        }

        public void FrameCamera(Camera cam, float padding)
        {
            if (cam == null || _map == null)
                return;

            cam.orthographic = true;
            float worldW = GoGHexGrid.WorldWidth(_map.Width) / _pixelsPerUnit;
            float worldH = GoGHexGrid.WorldHeight(_map.Height) / _pixelsPerUnit;
            float halfH = worldH * 0.5f + padding;
            float halfW = worldW * 0.5f + padding;
            cam.orthographicSize = Mathf.Max(halfH, halfW / Mathf.Max(cam.aspect, 0.01f));
            cam.transform.position = new Vector3(worldW * 0.5f, -worldH * 0.5f, -10f);
            cam.backgroundColor = new Color(0.08f, 0.16f, 0.22f, 1f);
        }

        public bool TryPick(Camera cam, Vector3 screenPos, out int x, out int y)
        {
            x = -1;
            y = -1;
            if (cam == null || _map == null)
                return false;
            var world = cam.ScreenToWorldPoint(screenPos);
            world.z = 0f;
            return GoGHexGrid.TryWorldToCell(world, _pixelsPerUnit, _map.Width, _map.Height, out x, out y);
        }

        public void Clear()
        {
            if (_tilesRoot != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(_tilesRoot.gameObject);
                else
                    Object.DestroyImmediate(_tilesRoot.gameObject);
                _tilesRoot = null;
            }

            if (_atlas != null)
            {
                _atlas.Dispose();
                _atlas = null;
            }

            if (_selectionSprite != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(_selectionSprite);
                else
                    Object.DestroyImmediate(_selectionSprite);
                _selectionSprite = null;
            }

            if (_selectionTexture != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(_selectionTexture);
                else
                    Object.DestroyImmediate(_selectionTexture);
                _selectionTexture = null;
            }

            if (_selection != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(_selection.gameObject);
                else
                    Object.DestroyImmediate(_selection.gameObject);
                _selection = null;
            }

            _map = null;
            _catalog = null;
        }

        void RebuildTiles()
        {
            if (_map == null)
                return;

            if (_tilesRoot != null)
            {
                Object.DestroyImmediate(_tilesRoot.gameObject);
                _tilesRoot = null;
            }

            _tilesRoot = new GameObject("Tiles").transform;
            _tilesRoot.SetParent(_root, false);

            for (int y = 0; y < _map.Height; y++)
            {
                for (int x = 0; x < _map.Width; x++)
                    DrawCell(x, y, _map.Get(x, y));
            }
        }

        void DrawCell(int x, int y, HexCell cell)
        {
            GoGHexGrid.CellCenter(x, y, out float cx, out float cy);
            Vector3 pos = GoGHexGrid.ToUnity(cx, cy, _pixelsPerUnit);

            if (cell.MarkType >= 4)
            {
                int catalogType = GoGTerrainCatalog.FromGameType(cell.MarkType);
                string image = _catalog != null ? _catalog.ImageName(catalogType, cell.MarkIdx) : null;
                Sprite overlay = _atlas.Find(image);
                if (overlay != null)
                    Spawn(overlay, pos, SortForest + y * 2, "mark", x, y);
            }

            if (cell.IsSea || _map.Climate == LandClimate.Water)
            {
                Sprite sea = _atlas.Find(SeaImage(cell.Variant));
                if (sea != null)
                    Spawn(sea, pos, SortSea + y * 2, "sea", x, y);

                int mask = SeaNeighborMask(x, y);
                if (mask != 63)
                {
                    Sprite coast = _atlas.Find("coast" + mask + ".png");
                    if (coast != null)
                        Spawn(coast, pos, SortCoast + y * 2, "coast", x, y);
                }
            }
            else
            {
                int idx = Mathf.Clamp(cell.Variant, 0, LandVariants - 1) + 1;
                string prefix = ScenarioMap.LandPrefix(_map.Climate);
                Sprite land = _atlas.FindFirst(prefix + idx + ".png", "land" + idx + ".png", "sand" + idx + ".png",
                    "snow" + idx + ".png");
                if (land != null)
                    Spawn(land, pos, SortLand + y * 2, prefix, x, y);
            }

            DrawPlaceables(x, y, cell, pos);
        }

        void DrawPlaceables(int x, int y, HexCell cell, Vector3 pos)
        {
            int constructionId = cell.ConstructionId;
            if (constructionId > 0 && constructionId <= 5)
            {
                string image = constructionId == 4
                    ? (cell.ConstructionLevel > 0
                        ? ConstructionImage(constructionId, cell.ConstructionLevel)
                        : "port" + PortFacing(x, y) + ".png")
                    : ConstructionImage(constructionId, cell.ConstructionLevel);
                Sprite sprite = _atlas.Find(image);
                if (sprite != null)
                    Spawn(sprite, pos, SortBuilding + y * 2, "construction", x, y);
            }

            var installImages = ScenarioPlaceables.InstallationImageSet(cell.InstallationId);
            if (installImages != null)
            {
                for (int i = 0; i < installImages.Length; i++)
                {
                    Sprite sprite = _atlas.Find(installImages[i]);
                    if (sprite != null)
                        Spawn(sprite, pos, SortBuilding + 20 + i + y * 2, "installation", x, y);
                }
            }
        }

        static string ConstructionImage(int constructionId, int level)
        {
            var images = ScenarioPlaceables.ConstructionImageSet(constructionId);
            if (images == null || images.Length == 0)
                return null;
            int index = Mathf.Clamp(Mathf.Max(1, level) - 1, 0, images.Length - 1);
            return images[index];
        }

        int PortFacing(int x, int y)
        {
            int land = 0;
            for (int bit = 0; bit < 6; bit++)
            {
                GoGHexGrid.CoastBitNeighbor(x, y, bit, out int nx, out int ny);
                bool neighborIsLand = !_map.InBounds(nx, ny) || !_map.Get(nx, ny).IsSea;
                if (neighborIsLand)
                    land |= 1 << bit;
            }

            for (int i = 0; i < PortLandMasks.Length; i++)
            {
                if (PortLandMasks[i] == land)
                    return PortLandFacing[i];
            }

            return 1;
        }

        int SeaNeighborMask(int x, int y)
        {
            int mask = 0;
            for (int bit = 0; bit < 6; bit++)
            {
                GoGHexGrid.CoastBitNeighbor(x, y, bit, out int nx, out int ny);
                bool neighborIsSea = ! _map.InBounds(nx, ny) || _map.Get(nx, ny).IsSea
                    || _map.Climate == LandClimate.Water;
                if (neighborIsSea)
                    mask |= 1 << bit;
            }

            return mask;
        }

        void Spawn(Sprite sprite, Vector3 position, int sortingOrder, string kind, int x, int y)
        {
            var go = new GameObject(kind + "_" + x + "_" + y);
            go.transform.SetParent(_tilesRoot, false);
            go.transform.localPosition = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
        }

        static string SeaImage(int variant)
        {
            int idx = Mathf.Clamp(variant, 0, SeaVariants - 1) + 1;
            return "sea" + idx + ".png";
        }

        Sprite CreateSelectionSprite()
        {
            const int w = 186;
            const int h = 162;
            const float inset = 3f;
            const float thickness = 5f;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            _selectionTexture = tex;

            var clear = new Color(0f, 0f, 0f, 0f);
            var fill = new Color(1f, 0.9f, 0.28f, 0.12f);
            var edge = new Color(1f, 0.86f, 0.2f, 1f);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            float cx = (w - 1) * 0.5f;
            float cy = (h - 1) * 0.5f;
            float outerW = GoGHexGrid.TileWidth * 0.5f - inset;
            float outerH = GoGHexGrid.TileHeight * 0.5f - inset;
            float innerW = Mathf.Max(8f, outerW - thickness);
            float innerH = Mathf.Max(8f, outerH - thickness);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    if (!GoGHexGrid.ContainsPoint(dx, dy, outerW, outerH))
                        continue;
                    pixels[y * w + x] = GoGHexGrid.ContainsPoint(dx, dy, innerW, innerH) ? fill : edge;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), _pixelsPerUnit, 0,
                SpriteMeshType.FullRect);
        }
    }
}
