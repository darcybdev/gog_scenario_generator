using System.Collections.Generic;
using UnityEngine;

namespace GoG
{
    // Battlefield infantry is a skeletal composite (motiondef res= + motion 2/3),
    // not a single PNG. This builds an idle figure from the country atlas and
    // places unitposdef.xml's five-man formation on the hex.
    public sealed class GoGInfantry
    {
        const int TexSize = 96;
        const float GroundBias = 18f;
        const float BodyDrawX = 0f;
        const float BodyDrawY = -6f;

        static readonly string[] Sheets =
        {
            "infantry_hd", "infantry_de_hd", "infantry_gb_hd", "infantry_am_hd",
            "infantry_fr_hd", "infantry_it_hd", "infantry_jp_hd", "infantry_ru_hd",
            "infantry_cn_hd", "infantry_tw_hd"
        };

        // Painter order, back → front. Offsets are Y-down pixels from the figure
        // origin (same convention as BILE rest translations / unitposdef).
        static readonly Part[] Idle =
        {
            new Part("后鞋.png", -10f, 28f),
            new Part("后小腿.png", -8f, 16f),
            new Part("后大腿.png", -5f, 4f),
            new Part("上后臂.png", -9f, -8f),
            new Part("后手.png", -13f, -2f),
            new Part("身体.png", BodyDrawX, BodyDrawY),
            new Part("前大腿.png", 6f, 2f),
            new Part("前小腿.png", 8f, 14f),
            new Part("前脚.png", 9f, 26f),
            new Part("上前臂.png", 8f, -6f),
            new Part("下臂.png", 12f, -1f),
            new Part("前手.png", 16f, 2f),
        };

        static readonly Part RifleShadow = new Part("步枪影子.png", -6f, 6f);
        static readonly Part Rifle = new Part("步枪.png", -8f, 0f);
        static readonly Part Bazooka = new Part("火箭筒.png", -4f, -18f);

        readonly Dictionary<string, Sprite> _composed = new Dictionary<string, Sprite>();
        readonly List<Texture2D> _owned = new List<Texture2D>();
        readonly List<Slot> _formation = new List<Slot>();
        float _formOriginX;
        float _formOriginY;

        struct Part
        {
            public readonly string Name;
            public readonly float X;
            public readonly float Y;

            public Part(string name, float x, float y)
            {
                Name = name;
                X = x;
                Y = y;
            }
        }

        public struct Slot
        {
            public float Dx;
            public float Dy;
            public float Scale;
        }

        public IList<Slot> Formation
        {
            get { return _formation; }
        }

        public void Load(GoGAtlasLibrary atlas)
        {
            for (int i = 0; i < Sheets.Length; i++)
            {
                string sheet = Sheets[i];
                string prefix = sheet.Replace("_hd", "") + "/";
                atlas.LoadSheet(sheet, prefix, true);
            }

            LoadFormation();
        }

        public Sprite Compose(GoGAtlasLibrary atlas, string artKey, bool assault)
        {
            string prefix = SheetPrefix(artKey);
            string cacheKey = prefix + (assault ? "3" : "2");
            Sprite cached;
            if (_composed.TryGetValue(cacheKey, out cached))
                return cached;

            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var clear = new Color32[TexSize * TexSize];
            tex.SetPixels32(clear);

            int cx = TexSize / 2;
            int cy = TexSize / 2 - 8;
            if (!assault)
                Blit(tex, atlas.Find(prefix + RifleShadow.Name), cx, cy, RifleShadow.X, RifleShadow.Y);
            Sprite body = null;
            for (int i = 0; i < Idle.Length; i++)
            {
                Sprite part = atlas.Find(prefix + Idle[i].Name);
                Blit(tex, part, cx, cy, Idle[i].X, Idle[i].Y);
                if (Idle[i].Name == "身体.png")
                    body = part;
            }

            Sprite head = atlas.Find(prefix + "头.png");
            if (body != null && head != null)
                BlitHeadOnBody(tex, body, head, cx, cy);

            Part weapon = assault ? Bazooka : Rifle;
            Blit(tex, atlas.Find(prefix + weapon.Name), cx, cy, weapon.X, weapon.Y);

            tex.Apply(false, false);
            _owned.Add(tex);

            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, TexSize, TexSize),
                new Vector2(0.5f, 0.35f),
                atlas.PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = cacheKey;
            _composed[cacheKey] = sprite;
            return sprite;
        }

        public Vector3 SlotPosition(int hexX, int hexY, int slot, float pixelsPerUnit)
        {
            return SlotPosition(hexX, hexY, slot, pixelsPerUnit, -4f, -20f, GroundBias);
        }

        public Vector3 SlotPosition(
            int hexX, int hexY, int slot, float pixelsPerUnit,
            float motionX, float motionY, float groundBias)
        {
            GoGHexGrid.CellCenter(hexX, hexY, out float cx, out float cy);
            Slot s = _formation[slot];
            float sx = cx + s.Dx + motionX;
            float sy = cy + s.Dy + motionY + groundBias;
            return GoGHexGrid.ToUnity(sx, sy, pixelsPerUnit);
        }

        public void Dispose()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] == null)
                    continue;
                if (Application.isPlaying)
                    Object.Destroy(_owned[i]);
                else
                    Object.DestroyImmediate(_owned[i]);
            }

            _owned.Clear();
            foreach (var pair in _composed)
            {
                if (pair.Value == null)
                    continue;
                if (Application.isPlaying)
                    Object.Destroy(pair.Value);
                else
                    Object.DestroyImmediate(pair.Value);
            }

            _composed.Clear();
            _formation.Clear();
        }

        static string SheetPrefix(string artKey)
        {
            if (artKey == "de") return "infantry_de/";
            if (artKey == "gb") return "infantry_gb/";
            if (artKey == "am") return "infantry_am/";
            if (artKey == "fr") return "infantry_fr/";
            if (artKey == "it") return "infantry_it/";
            if (artKey == "jp") return "infantry_jp/";
            if (artKey == "ru") return "infantry_ru/";
            if (artKey == "cn") return "infantry_cn/";
            if (artKey == "tw") return "infantry_tw/";
            return "infantry/";
        }

        void LoadFormation()
        {
            _formation.Clear();
            var xml = Resources.Load<TextAsset>("unitposdef");
            if (xml == null)
                return;

            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(xml.text);
            var nodes = doc.SelectNodes("/Units/Unit[@name='infantry']/Positions/Position");
            if (nodes == null || nodes.Count == 0)
                return;

            float sx = 0f, sy = 0f;
            var raw = new List<Slot>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                float x = AttrFloat(n, "x");
                float y = AttrFloat(n, "y");
                float scale = AttrFloat(n, "scale");
                if (scale <= 0f) scale = 1f;
                raw.Add(new Slot { Dx = x, Dy = y, Scale = scale });
                sx += x;
                sy += y;
            }

            _formOriginX = sx / raw.Count;
            _formOriginY = sy / raw.Count;
            for (int i = 0; i < raw.Count; i++)
            {
                var s = raw[i];
                s.Dx -= _formOriginX;
                s.Dy -= _formOriginY;
                _formation.Add(s);
            }
        }

        // Align the chin (bottom-center of opaque 头) to the collar
        // (top-center of opaque 身体). Center-blitting both sprites at a
        // shared (x,y) misses the neck because the parts are different sizes.
        static void BlitHeadOnBody(Texture2D dest, Sprite body, Sprite head, int cx, int cy)
        {
            float collarX, collarY, chinX, chinY;
            OpaqueTop(body, out collarX, out collarY);
            OpaqueBottom(head, out chinX, out chinY);
            // Sprite-local pixels, origin at sprite center, Y-down.
            float headX = BodyDrawX + collarX - chinX;
            float headY = BodyDrawY + collarY - chinY + 2f;
            Blit(dest, head, cx, cy, headX, headY);
        }

        static void OpaqueTop(Sprite sprite, out float midX, out float topY)
        {
            int w, h;
            Color[] px = ReadSprite(sprite, out w, out h);
            int maxY = -1, x0 = 0, x1 = 0;
            for (int y = 0; y < h; y++)
            {
                int row0 = -1, row1 = -1;
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a < 0.05f)
                        continue;
                    if (row0 < 0) row0 = x;
                    row1 = x;
                }
                if (row0 < 0)
                    continue;
                // GetPixels y=0 is the sprite bottom; the collar is max y.
                maxY = y;
                x0 = row0;
                x1 = row1;
            }
            midX = (x0 + x1) * 0.5f - w * 0.5f;
            topY = -(maxY + 0.5f - h * 0.5f);
        }

        static void OpaqueBottom(Sprite sprite, out float midX, out float botY)
        {
            int w, h;
            Color[] px = ReadSprite(sprite, out w, out h);
            int x0 = 0, x1 = 0, found = -1;
            for (int y = 0; y < h; y++)
            {
                int row0 = -1, row1 = -1;
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a < 0.05f)
                        continue;
                    if (row0 < 0) row0 = x;
                    row1 = x;
                }
                if (row0 < 0)
                    continue;
                found = y;
                x0 = row0;
                x1 = row1;
                break;
            }
            midX = (x0 + x1) * 0.5f - w * 0.5f;
            botY = -(found + 0.5f - h * 0.5f);
        }

        static Color[] ReadSprite(Sprite sprite, out int w, out int h)
        {
            var rect = sprite.textureRect;
            w = Mathf.RoundToInt(rect.width);
            h = Mathf.RoundToInt(rect.height);
            return sprite.texture.GetPixels(
                Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), w, h);
        }

        static void Blit(Texture2D dest, Sprite sprite, int cx, int cy, float lx, float ly)
        {
            if (sprite == null)
                return;

            var rect = sprite.textureRect;
            int w = Mathf.RoundToInt(rect.width);
            int h = Mathf.RoundToInt(rect.height);
            if (w <= 0 || h <= 0)
                return;

            var src = sprite.texture.GetPixels(
                Mathf.RoundToInt(rect.x),
                Mathf.RoundToInt(rect.y),
                w,
                h);

            // lx, ly are Y-down; dest texture Y is up.
            int dx = cx + Mathf.RoundToInt(lx) - w / 2;
            int dy = cy - Mathf.RoundToInt(ly) - h / 2;
            for (int y = 0; y < h; y++)
            {
                int ty = dy + y;
                if (ty < 0 || ty >= dest.height)
                    continue;
                for (int x = 0; x < w; x++)
                {
                    int tx = dx + x;
                    if (tx < 0 || tx >= dest.width)
                        continue;
                    Color c = src[y * w + x];
                    if (c.a < 0.02f)
                        continue;
                    Color d = dest.GetPixel(tx, ty);
                    dest.SetPixel(tx, ty, Color.Lerp(d, c, c.a));
                }
            }
        }

        static float AttrFloat(System.Xml.XmlNode node, string name)
        {
            if (node.Attributes == null || node.Attributes[name] == null)
                return 0f;
            float parsed;
            float.TryParse(
                node.Attributes[name].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out parsed);
            return parsed;
        }
    }
}
