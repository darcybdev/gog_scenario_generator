using System;
using UnityEngine;

namespace GoG
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BattleTerrainRenderer : MonoBehaviour
    {
        const string DefaultMap = "western_axis_1.parsed";
        const int LandVariantCount = 15;
        const int SeaVariantCount = 7;
        const int SortLand = 0;
        const int SortForest = 400;
        const int SortSea = 1000;
        const int SortCoast = 1200;
        const int SortBuilding = 1400;
        const int SortUnit = 1600;

        static readonly string[][] BuildingImages =
        {
            null,
            new[] { "city1.png", "city2.png", "city3.png", "city4.png", "city5.png" },
            new[] { "industy1.png", "industy2.png", "industy3.png" },
            new[] { "airport1.png", "airport2.png", "airport3.png" },
            new[] { "port1.png", "port2.png", "port3.png", "port4.png", "port5.png", "port6.png" },
            new[] { "oilfield1.png", "oilfield2.png" }
        };

        static readonly string[][] InstallationImages =
        {
            null,
            new[] { "fort.png", "fort_gun.png" },
            new[] { "bunker.png" },
            new[] { "radar.png" },
            new[] { "entrenchment.png" },
            new[] { "antiaircraft.png" }
        };

        [SerializeField] string mapResource = DefaultMap;
        [SerializeField] float pixelsPerUnit = 100f;
        [SerializeField] bool generateCoasts = true;
        [SerializeField] bool drawMarkOverlays = true;
        [SerializeField] bool drawBuildings = true;
        [SerializeField] bool drawUnits = true;
        [SerializeField] bool frameCamera = true;
        [SerializeField] float cameraPadding = 0.6f;

        GoGAtlasLibrary _atlas;
        GoGTerrainCatalog _catalog;
        GoGInfantry _infantry;
        CountryJson[] _countries;
        Transform _tilesRoot;
        string _builtKey;
        bool[] _sea;
        int _width;
        int _height;
        Camera _cachedCamera;
        string _landPrefix = "land";
        Vector3 _lastPanMouse;
        bool _panning;

        [Serializable]
        class BattleMapJson
        {
            public string climate;
            public HeaderJson header;
            public CountryJson[] countries;
            public AreaJson[] notable_areas;
            public ArmyJson[] armies;
            public string[] grid_terrain;
        }

        [Serializable]
        class CountryJson
        {
            public int index;
            public string name;
            public int[] building_levels;
        }

        [Serializable]
        class HeaderJson
        {
            public int width;
            public int height;
        }

        [Serializable]
        class AreaJson
        {
            public int x;
            public int y;
            public bool is_sea;
            public int terrain_idx;
            public int tile_variant;
            public int mark_type;
            public int mark_idx;
            public int construction_id;
            public int construction_level;
            public int installation_id;
            public string occupying_country_name;
            public bool is_key;
            public string name;
        }

        [Serializable]
        class ArmyJson
        {
            public int x;
            public int y;
            public int type_id;
            public string type;
            public string country;
            public int strength;
            public float float_24; // CArmy+0x3C facing: +1 right, -1 left
        }

        struct Cell
        {
            public bool isSea;
            public int variant;
            public int markType;
            public int markIdx;
            public bool hasDetail;
            public int constructionId;
            public int constructionLevel;
            public int installationId;
            public string occupyingCountryName;
            public bool isKey;
            public string name;
        }

        void OnEnable()
        {
            Rebuild();
        }

        void OnDisable()
        {
            ClearGenerated();
        }

        void OnValidate()
        {
            _builtKey = null;
        }

        void LateUpdate()
        {
            if (_builtKey != BuildKey())
                Rebuild();

            if (Application.isPlaying)
                HandleCameraInput();
        }

        string BuildKey()
        {
            string source = mapResource;
            if (Application.isPlaying && !string.IsNullOrEmpty(MapSelection.JsonText))
                source = "json|" + MapSelection.FilePath + "|" + MapSelection.JsonText.Length + "|" +
                         MapSelection.JsonText.GetHashCode();
            else if (Application.isPlaying && !string.IsNullOrEmpty(MapSelection.ResourceName))
                source = MapSelection.ResourceName;
            return source + "|" + pixelsPerUnit + "|" + generateCoasts + "|" + drawMarkOverlays + "|" +
                   drawBuildings + "|" + drawUnits;
        }

        void Rebuild()
        {
            ClearGenerated();
            _builtKey = BuildKey();

            if (pixelsPerUnit <= 0f)
                pixelsPerUnit = 100f;

            string json = null;
            if (Application.isPlaying && !string.IsNullOrEmpty(MapSelection.JsonText))
            {
                json = MapSelection.JsonText;
            }
            else
            {
                if (Application.isPlaying && !string.IsNullOrEmpty(MapSelection.ResourceName))
                    mapResource = MapSelection.ResourceName;

                var jsonAsset = LoadMapJson(mapResource);
                if (jsonAsset == null)
                {
                    Debug.LogError("Battle map JSON not found: " + mapResource);
                    return;
                }

                json = jsonAsset.text;
            }

            var map = JsonUtility.FromJson<BattleMapJson>(json);
            if (map == null || map.header == null)
            {
                Debug.LogError("Failed to parse battle map: " + mapResource);
                return;
            }

            _width = map.header.width;
            _height = map.header.height;
            if (_width <= 0 || _height <= 0)
                return;

            if (map.grid_terrain == null)
                map.grid_terrain = new string[0];

            var cells = BuildCells(map);
            _atlas = new GoGAtlasLibrary(pixelsPerUnit);
            LoadClimateSheets(map.climate);
            _atlas.LoadSheet("tiles_sea_hd");
            _atlas.LoadSheet("tiles_hd");
            _atlas.LoadSheet("snowless_hd");
            _atlas.LoadSheet("units_hd");
            _atlas.LoadSheet("ui_hd", null, true);
            _atlas.LoadSheet("mtank_hd", "mtank/", true);
            _atlas.LoadSheet("mtank_de_hd", "mtank_de/", true);
            _atlas.LoadSheet("artillery_hd", "artillery/", true);
            _atlas.LoadSheet("artillery_de_hd", "artillery_de/", true);
            _infantry = new GoGInfantry();
            _infantry.Load(_atlas);
            _catalog = GoGTerrainCatalog.Load();
            _countries = map.countries;

            _tilesRoot = new GameObject("Tiles").transform;
            _tilesRoot.SetParent(transform, false);
            _tilesRoot.gameObject.hideFlags = HideFlags.DontSave;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                    DrawCell(x, y, cells[y * _width + x]);
            }

            if (drawUnits)
                DrawArmies(map.armies);

            if (frameCamera)
                FrameCamera();
        }

        Cell[] BuildCells(BattleMapJson map)
        {
            var cells = new Cell[_width * _height];
            _sea = new bool[_width * _height];

            for (int y = 0; y < _height; y++)
            {
                string row = y < map.grid_terrain.Length ? map.grid_terrain[y] : string.Empty;
                for (int x = 0; x < _width; x++)
                {
                    bool isSea = x < row.Length && row[x] == '~';
                    var cell = new Cell
                    {
                        isSea = isSea,
                        variant = isSea ? 0 : LandFallbackVariant(x, y),
                        markType = 0,
                        markIdx = 0,
                        hasDetail = false
                    };
                    cells[y * _width + x] = cell;
                    _sea[y * _width + x] = isSea;
                }
            }

            if (map.notable_areas == null)
                return cells;

            for (int i = 0; i < map.notable_areas.Length; i++)
            {
                var area = map.notable_areas[i];
                if (area.x < 0 || area.y < 0 || area.x >= _width || area.y >= _height)
                    continue;

                int index = area.y * _width + area.x;
                var cell = cells[index];
                cell.isSea = area.is_sea;
                cell.variant = area.tile_variant;
                cell.markType = area.mark_type;
                cell.markIdx = area.mark_idx;
                cell.constructionId = area.construction_id;
                cell.constructionLevel = area.construction_level;
                cell.installationId = area.installation_id;
                cell.occupyingCountryName = area.occupying_country_name;
                cell.isKey = area.is_key;
                cell.name = area.name;
                cell.hasDetail = true;
                cells[index] = cell;
                _sea[index] = area.is_sea;
            }

            return cells;
        }

        void DrawCell(int x, int y, Cell cell)
        {
            GoGHexGrid.CellCenter(x, y, out float cx, out float cy);
            Vector3 pos = GoGHexGrid.ToUnity(cx, cy, pixelsPerUnit);

            int catalogType = GoGTerrainCatalog.FromGameType(cell.markType);
            if (drawMarkOverlays && cell.hasDetail && catalogType >= 3)
            {
                string image = _catalog != null ? _catalog.ImageName(catalogType, cell.markIdx) : null;
                Sprite overlay = _atlas.Find(image);
                if (overlay != null)
                {
                    string kind = catalogType < GoGTerrainCatalog.TypeNames.Length
                        ? GoGTerrainCatalog.TypeNames[catalogType]
                        : "mark";
                    SpawnTile(overlay, pos, SortForest + y * 2, kind, x, y);
                }
            }

            if (cell.isSea)
            {
                Sprite sea = _atlas.Find(SeaImage(cell.variant));
                if (sea != null)
                    SpawnTile(sea, pos, SortSea + y * 2, "sea", x, y);

                if (generateCoasts)
                {
                    int mask = SeaNeighborMask(x, y);
                    if (mask != 63)
                    {
                        Sprite coast = _atlas.Find("coast" + mask + ".png");
                        if (coast != null)
                            SpawnTile(coast, pos, SortCoast + y * 2, "coast", x, y);
                    }
                }
            }
            else
            {
                Sprite land = _atlas.FindFirst(LandImage(cell.variant), "land" + (Mathf.Clamp(cell.variant, 0, LandVariantCount - 1) + 1) + ".png");
                if (land != null)
                    SpawnTile(land, pos, SortLand + y * 2, "land", x, y);
            }

            if (drawBuildings)
                DrawBuilding(x, y, cell, pos);
        }

        void DrawBuilding(int x, int y, Cell cell, Vector3 pos)
        {
            int constructionId = cell.constructionId;
            if (constructionId <= 0 && (cell.isKey || !string.IsNullOrEmpty(cell.name)))
                constructionId = 1;

            if (constructionId > 0 && constructionId < BuildingImages.Length)
            {
                string image = BuildingImage(constructionId, cell.constructionLevel, cell.occupyingCountryName);
                Sprite sprite = _atlas.Find(image);
                if (sprite != null)
                {
                    string kind = constructionId == 1 ? "city"
                        : constructionId == 2 ? "factory"
                        : constructionId == 3 ? "airport"
                        : constructionId == 4 ? "port"
                        : "oil";
                    SpawnTile(sprite, pos, SortBuilding + y * 2, kind, x, y);
                }
            }

            if (cell.installationId > 0 && cell.installationId < InstallationImages.Length)
            {
                var images = InstallationImages[cell.installationId];
                for (int i = 0; i < images.Length; i++)
                {
                    Sprite sprite = _atlas.Find(images[i]);
                    if (sprite != null)
                        SpawnTile(sprite, pos, SortBuilding + 20 + i + y * 2, images[i].Replace(".png", ""), x, y);
                }
            }
        }

        void DrawArmies(ArmyJson[] armies)
        {
            if (armies == null)
                return;

            for (int i = 0; i < armies.Length; i++)
            {
                var army = armies[i];
                if (army.x < 0 || army.y < 0 || army.x >= _width || army.y >= _height)
                    continue;

                if (army.type_id <= 2)
                {
                    DrawInfantrySquad(army);
                    continue;
                }

                Sprite sprite = UnitSprite(army.type_id, army.country);
                if (sprite == null)
                    continue;

                GoGHexGrid.CellCenter(army.x, army.y, out float cx, out float cy);
                Vector3 pos = GoGHexGrid.ToUnity(cx, cy, pixelsPerUnit);
                string kind = string.IsNullOrEmpty(army.type) ? "unit" : army.type.Replace(' ', '_');
                SpawnTile(sprite, pos, SortUnit + army.y * 2, kind, army.x, army.y, 1f, FaceRight(army));
            }
        }

        void DrawInfantrySquad(ArmyJson army)
        {
            if (_infantry == null || _infantry.Formation.Count == 0)
                return;

            bool faceRight = FaceRight(army);
            bool assault = army.type_id != 0;
            Sprite figure = _infantry.Compose(_atlas, InfantryArtKey(army.country), assault);
            if (figure == null)
                return;

            if (army.type_id == 2)
            {
                Sprite truck = _atlas.Find("truck_common.png");
                if (truck != null)
                {
                    GoGHexGrid.CellCenter(army.x, army.y, out float cx, out float cy);
                    Vector3 truckPos = GoGHexGrid.ToUnity(cx, cy - 12f, pixelsPerUnit);
                    SpawnTile(truck, truckPos, SortUnit + army.y * 2, "armoured_truck",
                        army.x, army.y, 1f, faceRight);
                }
            }

            string kind = army.type_id == 2 ? "armoured_infantry"
                : army.type_id == 1 ? "assault_infantry"
                : "infantry";
            var formation = _infantry.Formation;
            for (int i = 0; i < formation.Count; i++)
            {
                Vector3 pos = _infantry.SlotPosition(army.x, army.y, i, pixelsPerUnit);
                int sort = SortUnit + army.y * 2 + 1 + (formation.Count - 1 - i);
                SpawnTile(figure, pos, sort, kind + "_" + i,
                    army.x, army.y, formation[i].Scale, faceRight);
            }
        }

        // motiondef.xml image= names, plus hull frames for skeletal tanks/guns.
        Sprite UnitSprite(int typeId, string country)
        {
            string art = CountryArtKey(country);
            switch (typeId)
            {
                case 2:
                    return _atlas.Find("truck_common.png");
                case 3:
                    return _atlas.FindFirst(
                        "armoredcar_" + art + ".png",
                        "armoredcar_common.png");
                case 4:
                    return _atlas.FindFirst(
                        "lighttank_" + art + ".png",
                        "lighttank_common.png");
                case 5:
                    return _atlas.FindFirst(
                        "mtank_" + art + "/Symbol 1.png",
                        "mtank/Symbol 1.png",
                        "button_unit_mediumtank.png");
                case 6:
                    return _atlas.Find("button_unit_army_heavytank.png");
                case 7:
                    return _atlas.FindFirst(
                        art == "gb" || art == "fr" ? "antitankgun_gbfr.png" : null,
                        "antitankgun_" + art + ".png",
                        "antitankgun_common.png");
                case 8:
                    return _atlas.FindFirst(
                        "artillery_" + (art == "gb" || art == "fr" ? "de" : art) + "/Symbol 1.png",
                        art == "de" ? "artillery_de/Symbol 1.png" : null,
                        "artillery/Symbol 1.png",
                        "button_unit_army_artillery.png");
                case 9:
                    return _atlas.FindFirst(
                        "rocket_" + art + ".png",
                        "rocket_common.png");
                case 10:
                    return _atlas.Find("submarine.png");
                case 11:
                    return _atlas.Find("destroyer.png");
                case 12:
                    return _atlas.Find("cruiser.png");
                case 13:
                    return _atlas.Find("battleship.png");
                case 14:
                    return _atlas.Find("aircraftcarrier.png");
                case 15:
                    return _atlas.Find("transportship.png");
                case 16:
                    return _atlas.Find("button_unit_airforce_scout.png");
                case 17:
                    return _atlas.FindFirst("fighter.png", "button_unit_airforce_fighter.png");
                case 18:
                    return _atlas.FindFirst("bomber.png", "button_unit_airforce_bomber.png");
                case 19:
                    return _atlas.Find("button_unit_airforce_airborne.png");
                case 20:
                    return _atlas.Find("button_unit_airforce_nuclear.png");
                default:
                    return null;
            }
        }

        // Art (motiondef dir=-1) faces left. CArmy+0x3C +1 flips to face right.
        static bool FaceRight(ArmyJson army)
        {
            return army.float_24 > 0f;
        }

        static string CountryArtKey(string country)
        {
            if (string.IsNullOrEmpty(country))
                return "common";

            int end = 0;
            while (end < country.Length && char.IsLetter(country[end]))
                end++;
            string key = end > 0 ? country.Substring(0, end) : country;

            if (key == "hu") return "de";
            if (key == "ca" || key == "au" || key == "in") return "gb";
            if (key == "rk") return "am";
            if (key == "ja" || key == "mc") return "jp";
            if (key == "cn" || key == "nk") return "ru";
            if (key == "it" || key == "pl" || key == "es" || key == "no" ||
                key == "nl" || key == "be" || key == "dk" || key == "gr" ||
                key == "yu" || key == "pt" || key == "tr" || key == "bg" ||
                key == "fl" || key == "tw")
                return "common";
            return key;
        }

        static string InfantryArtKey(string country)
        {
            if (string.IsNullOrEmpty(country))
                return "common";

            int end = 0;
            while (end < country.Length && char.IsLetter(country[end]))
                end++;
            string key = end > 0 ? country.Substring(0, end) : country;

            if (key == "hu") return "de";
            if (key == "ca" || key == "au" || key == "in") return "gb";
            if (key == "rk") return "am";
            if (key == "ja" || key == "mc") return "jp";
            if (key == "nk") return "ru";
            if (key == "de" || key == "gb" || key == "am" || key == "fr" ||
                key == "it" || key == "jp" || key == "ru" || key == "cn" || key == "tw")
                return key;
            return "common";
        }

        string BuildingImage(int constructionId, int hexLevel, string countryName)
        {
            var images = BuildingImages[constructionId];
            int level = hexLevel > 0 ? hexLevel : BuildingLevel(constructionId, countryName) + 1;
            int index = Mathf.Clamp(level - 1, 0, images.Length - 1);
            return images[index];
        }

        int BuildingLevel(int constructionId, string countryName)
        {
            if (string.IsNullOrEmpty(countryName) || _countries == null)
                return 0;

            for (int i = 0; i < _countries.Length; i++)
            {
                var country = _countries[i];
                if (country == null || country.name != countryName || country.building_levels == null)
                    continue;
                int slot = constructionId - 1;
                if (slot < 0 || slot >= country.building_levels.Length)
                    return 0;
                return Mathf.Max(0, country.building_levels[slot]);
            }

            return 0;
        }

        int SeaNeighborMask(int x, int y)
        {
            int mask = 0;
            for (int bit = 0; bit < 6; bit++)
            {
                GoGHexGrid.CoastBitNeighbor(x, y, bit, out int nx, out int ny);
                bool neighborIsSea = nx < 0 || ny < 0 || nx >= _width || ny >= _height
                    || _sea[ny * _width + nx];
                if (neighborIsSea)
                    mask |= 1 << bit;
            }

            return mask;
        }

        void SpawnTile(Sprite sprite, Vector3 position, int sortingOrder, string kind, int x, int y)
        {
            SpawnTile(sprite, position, sortingOrder, kind, x, y, 1f, false);
        }

        void SpawnTile(
            Sprite sprite, Vector3 position, int sortingOrder, string kind, int x, int y,
            float scale, bool flipX)
        {
            var go = new GameObject(kind + "_" + x + "_" + y);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(_tilesRoot, false);
            go.transform.localPosition = position;
            float sx = (flipX ? -1f : 1f) * scale;
            go.transform.localScale = new Vector3(sx, scale, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
        }

        void FrameCamera()
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            cam.orthographic = true;
            float worldW = GoGHexGrid.WorldWidth(_width) / pixelsPerUnit;
            float worldH = GoGHexGrid.WorldHeight(_height) / pixelsPerUnit;
            float halfH = worldH * 0.5f + cameraPadding;
            float halfW = worldW * 0.5f + cameraPadding;
            float size = Mathf.Max(halfH, halfW / Mathf.Max(cam.aspect, 0.01f));
            cam.orthographicSize = size;
            cam.transform.position = new Vector3(worldW * 0.5f, -worldH * 0.5f, -10f);
            cam.backgroundColor = new Color(0.08f, 0.16f, 0.22f, 1f);
            _cachedCamera = cam;
        }

        void HandleCameraInput()
        {
            var cam = _cachedCamera != null ? _cachedCamera : Camera.main;
            if (cam == null)
                return;
            _cachedCamera = cam;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * (scroll > 0f ? 0.9f : 1.1f), 2f, 80f);

            bool pan = Input.GetMouseButton(1) || Input.GetMouseButton(2) ||
                (Input.GetMouseButton(0) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)));
            Vector3 mouse = Input.mousePosition;
            if (pan)
            {
                if (_panning)
                    PanByMouseDelta(cam, _lastPanMouse, mouse);
                _panning = true;
                _lastPanMouse = mouse;
            }
            else
            {
                _panning = false;
            }
        }

        static void PanByMouseDelta(Camera cam, Vector3 previousScreen, Vector3 currentScreen)
        {
            previousScreen.z = Mathf.Abs(cam.transform.position.z);
            currentScreen.z = previousScreen.z;
            Vector3 prev = cam.ScreenToWorldPoint(previousScreen);
            Vector3 now = cam.ScreenToWorldPoint(currentScreen);
            cam.transform.position -= new Vector3(now.x - prev.x, now.y - prev.y, 0f);
        }

        void ClearGenerated()
        {
            if (_tilesRoot != null)
            {
                if (Application.isPlaying)
                    Destroy(_tilesRoot.gameObject);
                else
                    DestroyImmediate(_tilesRoot.gameObject);
                _tilesRoot = null;
            }

            if (_infantry != null)
            {
                _infantry.Dispose();
                _infantry = null;
            }

            if (_atlas != null)
            {
                _atlas.Dispose();
                _atlas = null;
            }

            _catalog = null;
            _countries = null;
            _sea = null;
        }

        static TextAsset LoadMapJson(string resourceName)
        {
#if UNITY_EDITOR
            var fromPath = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Resources/" + resourceName + ".json");
            if (fromPath != null)
                return fromPath;
#endif
            var asset = Resources.Load<TextAsset>(resourceName);
            if (asset != null && asset.text.TrimStart().StartsWith("{"))
                return asset;
            return asset;
        }

        void LoadClimateSheets(string climate)
        {
            if (string.Equals(climate, "sand", StringComparison.OrdinalIgnoreCase))
            {
                _landPrefix = "sand";
                _atlas.LoadSheet("tiles_sand_hd");
                _atlas.LoadSheet("coast_sand_hd");
            }
            else if (string.Equals(climate, "snow", StringComparison.OrdinalIgnoreCase))
            {
                _landPrefix = "snow";
                _atlas.LoadSheet("tiles_snow_hd");
                _atlas.LoadSheet("coast_snow_hd");
            }
            else
            {
                _landPrefix = "land";
            }

            _atlas.LoadSheet("tiles_land_hd");
            _atlas.LoadSheet("coast_land_hd");
        }

        static int LandFallbackVariant(int x, int y)
        {
            return Mathf.Abs(x * 7 + y * 13 + x * y * 3) % LandVariantCount;
        }

        string LandImage(int variant)
        {
            int idx = Mathf.Clamp(variant, 0, LandVariantCount - 1) + 1;
            return _landPrefix + idx + ".png";
        }

        static string SeaImage(int variant)
        {
            int idx = Mathf.Clamp(variant, 0, SeaVariantCount - 1) + 1;
            return "sea" + idx + ".png";
        }
    }
}
