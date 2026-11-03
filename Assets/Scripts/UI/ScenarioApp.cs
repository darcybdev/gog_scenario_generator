using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScenarioEditor
{
    public sealed class ScenarioApp : MonoBehaviour
    {
        const float PixelsPerUnit = 100f;
        const float CameraPadding = 0.6f;
        const int CursorSize = 48;

        static readonly List<string> LandTypes = new List<string>
        {
            "Land", "Sand", "Snow", "Water"
        };

        UIDocument _document;
        VisualElement _settingsScreen;
        VisualElement _editorHud;
        DropdownField _landType;
        IntegerField _width;
        IntegerField _height;
        Slider _water;
        Label _waterValue;
        TextField _scenarioName;
        Label _cellInfo;
        VisualElement _palette;
        ScenarioRenderer _renderer;
        Camera _camera;
        bool _editing;
        bool _uiBound;
        int _selectedX = -1;
        int _selectedY = -1;
        PlaceableItem _tool;
        Button _selectedItemButton;
        Button _selectedTypeButton;
        Texture2D _cursorTexture;
        bool _blockMapClick;

        void Awake()
        {
            _camera = Camera.main;
            _renderer = new ScenarioRenderer(transform, PixelsPerUnit);
        }

        void Start()
        {
                BuildUi();
                ShowSettings();
        }

        void OnDestroy()
        {
            ResetCursor();
            if (_renderer != null)
                _renderer.Clear();
        }

        void Update()
        {
            if (!_editing)
                return;

            HandleCameraInput();
        }

        void LateUpdate()
        {
            if (!_editing)
                return;

            if (!Input.GetMouseButtonDown(0))
            {
                _blockMapClick = false;
                return;
            }

            bool overOverlay = _blockMapClick || PointerOverHud();
            _blockMapClick = false;
            if (!overOverlay)
                TrySelectCell();
        }

        void BuildUi()
        {
            if (_uiBound)
                return;

            _document = gameObject.GetComponent<UIDocument>();
            if (_document == null)
            {
                Debug.LogError("UIDocument is missing on Scenario. Add it in the scene.");
                return;
            }

            if (_document.visualTreeAsset == null)
        {
            var tree = LoadSettingsTree();
            if (tree == null)
            {
                Debug.LogError("Settings.uxml not found at Assets/UI/Settings.uxml");
                return;
            }

                _document.visualTreeAsset = tree;
            }
            var root = _document.rootVisualElement;
            if (root == null)
                return;

            ApplySettingsStyle(root);
            _settingsScreen = root.Q<VisualElement>("settings-screen");
            _editorHud = root.Q<VisualElement>("editor-hud");
            _landType = root.Q<DropdownField>("land-type");
            _width = root.Q<IntegerField>("map-width");
            _height = root.Q<IntegerField>("map-height");
            _water = root.Q<Slider>("water-percent");
            _waterValue = root.Q<Label>("water-value");
            _scenarioName = root.Q<TextField>("scenario-name");
            _cellInfo = root.Q<Label>("cell-info");
            _palette = root.Q<VisualElement>("palette");

            if (_landType != null)
            {
                _landType.choices = LandTypes;
                _landType.index = 0;
                _landType.RegisterValueChangedCallback(_ => SyncWaterForClimate());
            }

            if (_water != null)
            {
                _water.RegisterValueChangedCallback(evt =>
                {
                    if (_waterValue != null)
                        _waterValue.text = Mathf.RoundToInt(evt.newValue) + "%";
                });
            }

            var generate = root.Q<Button>("generate");
            if (generate != null)
                generate.clicked += Generate;

            var save = root.Q<Button>("save");
            if (save != null)
                save.clicked += Save;

            var settings = root.Q<Button>("settings");
            if (settings != null)
                settings.clicked += ShowSettings;

            SyncWaterForClimate();
            _uiBound = true;
        }

        static void ApplySettingsStyle(VisualElement root)
        {
            var sheet = LoadSettingsStyle();
            if (sheet == null)
                return;
            for (int i = 0; i < root.styleSheets.count; i++)
            {
                if (root.styleSheets[i] == sheet)
                    return;
            }

            root.styleSheets.Add(sheet);
        }

        static VisualTreeAsset LoadSettingsTree()
        {
#if UNITY_EDITOR
            var fromPath = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Settings.uxml");
            if (fromPath != null)
                return fromPath;
#endif
            return Resources.Load<VisualTreeAsset>("Settings");
        }

        static StyleSheet LoadSettingsStyle()
        {
#if UNITY_EDITOR
            var fromPath = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/USS/Settings.uss");
            if (fromPath != null)
                return fromPath;
#endif
            return Resources.Load<StyleSheet>("Settings");
        }

        void SyncWaterForClimate()
        {
            bool allWater = CurrentClimate() == LandClimate.Water;
            if (_water != null)
            {
                _water.SetEnabled(!allWater);
                if (allWater)
                {
                    _water.SetValueWithoutNotify(100f);
                    if (_waterValue != null)
                        _waterValue.text = "100%";
                }
            }
        }

        void Generate()
        {
            int width = _width != null ? _width.value : 24;
            int height = _height != null ? _height.value : 20;
            float water = _water != null ? _water.value : 35f;
            string name = _scenarioName != null ? _scenarioName.value : "generated";
            var climate = CurrentClimate();

            var map = ScenarioGenerator.Generate(
                width, height, climate, water, name, Random.Range(1, int.MaxValue));
            _renderer.Show(map);
            _renderer.FrameCamera(_camera, CameraPadding);
            _renderer.ClearSelection();
            _selectedX = -1;
            _selectedY = -1;
            if (_cellInfo != null)
            {
                _cellInfo.text = "";
                _cellInfo.style.display = DisplayStyle.None;
            }

            _editing = true;
            if (_settingsScreen != null)
                _settingsScreen.style.display = DisplayStyle.None;
            if (_editorHud != null)
                _editorHud.style.display = DisplayStyle.Flex;
            BuildPalette();
        }

        void ShowSettings()
        {
            _editing = false;
            _renderer.Clear();
            _selectedX = -1;
            _selectedY = -1;
            ClearTool();
            if (_palette != null)
                _palette.Clear();
            if (_settingsScreen != null)
                _settingsScreen.style.display = DisplayStyle.Flex;
            if (_editorHud != null)
                _editorHud.style.display = DisplayStyle.None;
        }

        void Save()
        {
            var map = _renderer != null ? _renderer.Map : null;
            if (map == null)
                return;
            if (_scenarioName != null && !string.IsNullOrWhiteSpace(_scenarioName.value))
                map.Name = _scenarioName.value;
            string path = ScenarioJson.Save(map);
            Debug.Log("Saved scenario JSON: " + path);
        }

        void TrySelectCell()
        {
            if (!_renderer.TryPick(_camera, Input.mousePosition, out int x, out int y))
                return;

            if (_tool != null)
                ApplyTool(x, y);

            _selectedX = x;
            _selectedY = y;
            _renderer.Select(x, y);
            UpdateCellInfo(x, y);
        }

        void ApplyTool(int x, int y)
        {
            var map = _renderer.Map;
            if (map == null || !map.InBounds(x, y) || _tool == null)
                return;

            var cell = map.Get(x, y);
            switch (_tool.Kind)
            {
                case PlaceKind.Land:
                    cell.IsSea = false;
                    cell.Variant = _tool.Index;
                    cell.MarkType = 0;
                    cell.MarkIdx = _tool.Index;
                    break;
                case PlaceKind.Sea:
                    cell.IsSea = true;
                    cell.Variant = _tool.Index;
                    cell.MarkType = 1;
                    cell.MarkIdx = _tool.Index;
                    cell.ConstructionId = 0;
                    cell.ConstructionLevel = 0;
                    cell.InstallationId = 0;
                    break;
                case PlaceKind.Mark:
                    cell.IsSea = false;
                    cell.MarkType = _tool.Id;
                    cell.MarkIdx = _tool.Index;
                    break;
                case PlaceKind.Construction:
                    cell.ConstructionId = _tool.Id;
                    cell.ConstructionLevel = _tool.Id == 0 ? 0 : Mathf.Max(1, _tool.Level);
                    if (_tool.Id == 4)
                    {
                        cell.IsSea = true;
                        if (cell.MarkType == 0 || cell.MarkType >= 4)
                        {
                            cell.MarkType = 1;
                            cell.MarkIdx = cell.Variant;
                        }
                    }
                    else if (_tool.Id > 0)
                    {
                        cell.IsSea = false;
                    }
                    break;
                case PlaceKind.Installation:
                    cell.InstallationId = _tool.Id;
                    if (_tool.Id > 0)
                        cell.IsSea = false;
                    break;
            }

            map.Set(x, y, cell);
            _renderer.RefreshTiles();
        }

        void UpdateCellInfo(int x, int y)
        {
            var map = _renderer != null ? _renderer.Map : null;
            if (_cellInfo == null || map == null || !map.InBounds(x, y))
                return;

            var cell = map.Get(x, y);
            string type = cell.DisplayType(map.Climate);
            string extra = cell.DescribePlace();
                _cellInfo.style.display = DisplayStyle.Flex;
            _cellInfo.text = "Land: " + type + "    X: " + x + "    Y: " + y
                + (string.IsNullOrEmpty(extra) ? "" : "    " + extra);
        }

        void BuildPalette()
        {
            if (_palette == null || _renderer == null)
                return;

            _palette.Clear();
            ClearTool();
            var climate = _renderer.Map != null ? _renderer.Map.Climate : CurrentClimate();
            var groups = ScenarioPlaceables.Groups(climate);
            for (int g = 0; g < groups.Length; g++)
            {
                var group = groups[g];
                var row = new VisualElement();
                row.AddToClassList("palette-row");
                row.RegisterCallback<MouseEnterEvent>(_ => row.AddToClassList("open"));
                row.RegisterCallback<MouseLeaveEvent>(_ => row.RemoveFromClassList("open"));
                row.RegisterCallback<PointerDownEvent>(OnOverlayPointerDown, TrickleDown.TrickleDown);

                var items = new VisualElement();
                items.AddToClassList("palette-items");
                int count = group.Items != null ? group.Items.Length : 0;
                if (count > 0)
                    items.style.width = Mathf.Min(count, 10) * 106f + 16f;
                items.pickingMode = PickingMode.Position;
                items.RegisterCallback<PointerDownEvent>(OnOverlayPointerDown, TrickleDown.TrickleDown);
                items.RegisterCallback<GeometryChangedEvent>(evt =>
                {
                    float rowH = row.layout.height;
                    float itemH = evt.newRect.height;
                    items.style.top = itemH > 0f ? (rowH - itemH) * 0.5f : 0f;
                });

                var typeBtn = new Button();
                typeBtn.text = group.Title;
                typeBtn.AddToClassList("palette-type");

                var capturedGroup = group;
                var capturedType = typeBtn;
                typeBtn.clicked += () =>
                {
                    if (capturedGroup.Items != null && capturedGroup.Items.Length > 0)
                    {
                        Button first = items.childCount > 0 ? items[0] as Button : null;
                        SelectTool(capturedGroup.Items[0], first, capturedType);
                    }
                };

                if (group.Items != null)
                {
                    for (int i = 0; i < group.Items.Length; i++)
                    {
                        var item = group.Items[i];
                        var btn = new Button();
                        btn.AddToClassList("palette-item");
                        btn.userData = item;

                        var thumb = new Image();
                        thumb.AddToClassList("palette-thumb");
                        thumb.pickingMode = PickingMode.Ignore;
                        thumb.scaleMode = ScaleMode.ScaleToFit;
                        Sprite sprite = _renderer.FindSprite(item.ImageNames);
                        if (sprite != null)
                            thumb.sprite = sprite;
                        btn.Add(thumb);

                        var label = new Label(item.Label);
                        label.AddToClassList("palette-item-label");
                        label.pickingMode = PickingMode.Ignore;
                        btn.Add(label);

                        var capturedItem = item;
                        var capturedBtn = btn;
                        btn.clicked += () => SelectTool(capturedItem, capturedBtn, capturedType);
                        items.Add(btn);
                    }
                }

                row.Add(items);
                row.Add(typeBtn);
                _palette.Add(row);
            }
        }

        void OnOverlayPointerDown(PointerDownEvent evt)
        {
            _blockMapClick = true;
            evt.StopPropagation();
        }

        void SelectTool(PlaceableItem item, Button itemButton, Button typeButton)
        {
            _blockMapClick = true;
            if (item != null && SameTool(item, _tool))
            {
                ClearTool();
                return;
            }

            if (_selectedItemButton != null)
                _selectedItemButton.RemoveFromClassList("selected");
            if (_selectedTypeButton != null)
                _selectedTypeButton.RemoveFromClassList("selected");

            _tool = item;
            _selectedItemButton = itemButton;
            _selectedTypeButton = typeButton;
            if (_selectedItemButton != null)
                _selectedItemButton.AddToClassList("selected");
            if (_selectedTypeButton != null)
                _selectedTypeButton.AddToClassList("selected");
            ApplyToolCursor(item);
        }

        void ClearTool()
        {
            if (_selectedItemButton != null)
                _selectedItemButton.RemoveFromClassList("selected");
            if (_selectedTypeButton != null)
                _selectedTypeButton.RemoveFromClassList("selected");
            _selectedItemButton = null;
            _selectedTypeButton = null;
            _tool = null;
            ResetCursor();
        }

        void ApplyToolCursor(PlaceableItem item)
        {
            Texture2D next = null;
            if (item != null && !item.IsClear && _renderer != null)
            {
                Sprite sprite = _renderer.FindSprite(item.ImageNames);
                if (sprite != null)
                    next = CreateCursorTexture(sprite, CursorSize);
            }

            DestroyCursorTexture();
            _cursorTexture = next;
            if (_cursorTexture != null)
            {
                UnityEngine.Cursor.SetCursor(
                    _cursorTexture,
                    new Vector2(_cursorTexture.width * 0.5f, _cursorTexture.height * 0.5f),
                    CursorMode.ForceSoftware);
            }
            else
            {
                UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }

        void ResetCursor()
        {
            UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            DestroyCursorTexture();
        }

        void DestroyCursorTexture()
        {
            if (_cursorTexture == null)
                return;
            if (Application.isPlaying)
                Destroy(_cursorTexture);
            else
                DestroyImmediate(_cursorTexture);
            _cursorTexture = null;
        }

        static Texture2D CreateCursorTexture(Sprite sprite, int size)
        {
            var tex = sprite.texture;
            if (tex == null || !tex.isReadable)
                return null;

            Rect rect = sprite.textureRect;
            int srcX = Mathf.Clamp(Mathf.RoundToInt(rect.x), 0, tex.width - 1);
            int srcY = Mathf.Clamp(Mathf.RoundToInt(rect.y), 0, tex.height - 1);
            int srcW = Mathf.Max(1, Mathf.Min(Mathf.RoundToInt(rect.width), tex.width - srcX));
            int srcH = Mathf.Max(1, Mathf.Min(Mathf.RoundToInt(rect.height), tex.height - srcY));
            Color[] src = tex.GetPixels(srcX, srcY, srcW, srcH);

            float scale = Mathf.Min(size / (float)srcW, size / (float)srcH);
            int dstW = Mathf.Max(1, Mathf.RoundToInt(srcW * scale));
            int dstH = Mathf.Max(1, Mathf.RoundToInt(srcH * scale));
            int ox = (size - dstW) / 2;
            int oy = (size - dstH) / 2;

            var dst = new Texture2D(size, size, TextureFormat.RGBA32, false);
            dst.filterMode = FilterMode.Bilinear;
            dst.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (int dy = 0; dy < dstH; dy++)
            {
                int sy = Mathf.Clamp((dy * srcH) / dstH, 0, srcH - 1);
                for (int dx = 0; dx < dstW; dx++)
                {
                    int sx = Mathf.Clamp((dx * srcW) / dstW, 0, srcW - 1);
                    pixels[(oy + dy) * size + (ox + dx)] = src[sy * srcW + sx];
                }
            }

            dst.SetPixels(pixels);
            dst.Apply(false, false);
            return dst;
        }

        static bool SameTool(PlaceableItem a, PlaceableItem b)
        {
            return a != null && b != null
                && a.Kind == b.Kind
                && a.Id == b.Id
                && a.Index == b.Index
                && a.Level == b.Level;
        }

        void HandleCameraInput()
        {
            if (_camera == null)
                return;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                _camera.orthographicSize = Mathf.Clamp(
                    _camera.orthographicSize * (scroll > 0f ? 0.9f : 1.1f), 2f, 80f);

            if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                float scale = _camera.orthographicSize * 0.35f;
                _camera.transform.position -= new Vector3(
                    Input.GetAxis("Mouse X") * scale,
                    Input.GetAxis("Mouse Y") * scale,
                    0f);
            }
        }

        bool PointerOverHud()
        {
            if (_document == null || _document.rootVisualElement == null || _document.rootVisualElement.panel == null)
                return false;

            var panel = _document.rootVisualElement.panel;
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, Input.mousePosition);
            var picked = panel.Pick(panelPos);
            if (IsHudElement(picked))
                return true;

            if (_palette != null)
            {
                foreach (var child in _palette.Children())
                {
                    if (child.worldBound.Contains(panelPos))
                        return true;
                    for (int i = 0; i < child.childCount; i++)
                    {
                        var nested = child[i];
                        if (nested.resolvedStyle.display == DisplayStyle.None)
                            continue;
                        if (nested.worldBound.Contains(panelPos))
                            return true;
                    }
                }
            }

            return false;
        }

        static bool IsHudElement(VisualElement picked)
        {
            if (picked == null)
                return false;
            if (picked is Button)
                return true;

            for (var el = picked; el != null; el = el.parent)
            {
                if (el.ClassListContains("palette") || el.ClassListContains("palette-row")
                    || el.ClassListContains("palette-items") || el.ClassListContains("palette-item")
                    || el.ClassListContains("palette-type") || el.ClassListContains("hud-actions"))
                    return true;
            }

            return false;
        }

        LandClimate CurrentClimate()
        {
            string value = _landType != null ? _landType.value : null;
            if (string.IsNullOrEmpty(value) && _landType != null && _landType.index >= 0 &&
                _landType.index < LandTypes.Count)
                value = LandTypes[_landType.index];
            if (string.IsNullOrEmpty(value))
                return LandClimate.Land;

            switch (value.Trim().ToLowerInvariant())
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
    }
}
