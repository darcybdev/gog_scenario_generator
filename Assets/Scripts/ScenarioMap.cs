namespace ScenarioEditor
{
    public enum LandClimate
    {
        Land,
        Sand,
        Snow,
        Water
    }

    public struct HexCell
    {
        public bool IsSea;
        public int Variant;
        public int MarkType;
        public int MarkIdx;
        public int ConstructionId;
        public int ConstructionLevel;
        public int InstallationId;

        public int Overlay
        {
            get { return MarkType >= 4 ? MarkType : 16; }
        }

        public string DisplayType(LandClimate climate)
        {
            if (IsSea || climate == LandClimate.Water)
                return "water";
            switch (climate)
            {
                case LandClimate.Sand:
                    return "sand";
                case LandClimate.Snow:
                    return "snow";
                default:
                    return "land";
            }
        }

        public string MarkName()
        {
            if (IsSea)
                return "sea";
            if (MarkType >= 4)
                return ScenarioPlaceables.MarkName(MarkType);
            return DisplayType(LandClimate.Land);
        }

        public string DescribePlace()
        {
            string mark = MarkType >= 4 ? "Mark: " + ScenarioPlaceables.MarkName(MarkType) : null;
            string construction = ConstructionId > 0
                ? "Construction: " + ScenarioPlaceables.ConstructionName(ConstructionId) + " L" + ConstructionLevel
                : null;
            string installation = InstallationId > 0
                ? "Installation: " + ScenarioPlaceables.InstallationName(InstallationId)
                : null;
            string text = "";
            if (mark != null)
                text = mark;
            if (construction != null)
                text = text.Length == 0 ? construction : text + "    " + construction;
            if (installation != null)
                text = text.Length == 0 ? installation : text + "    " + installation;
            return text;
        }
    }

    public sealed class ScenarioMap
    {
        public int Width;
        public int Height;
        public LandClimate Climate;
        public string Name;
        public HexCell[] Cells;

        public ScenarioMap(int width, int height, LandClimate climate, string name)
        {
            Width = width;
            Height = height;
            Climate = climate;
            Name = string.IsNullOrEmpty(name) ? "generated" : name;
            Cells = new HexCell[width * height];
        }

        public HexCell Get(int x, int y)
        {
            return Cells[y * Width + x];
        }

        public void Set(int x, int y, HexCell cell)
        {
            Cells[y * Width + x] = cell;
        }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public static int TilesetId(LandClimate climate)
        {
            switch (climate)
            {
                case LandClimate.Sand:
                    return 2;
                case LandClimate.Snow:
                    return 7;
                case LandClimate.Water:
                    return 1;
                default:
                    return 1;
            }
        }

        public static string LandPrefix(LandClimate climate)
        {
            switch (climate)
            {
                case LandClimate.Sand:
                    return "sand";
                case LandClimate.Snow:
                    return "snow";
                default:
                    return "land";
            }
        }

        public static string LandSheet(LandClimate climate)
        {
            switch (climate)
            {
                case LandClimate.Sand:
                    return "tiles_sand_hd";
                case LandClimate.Snow:
                    return "tiles_snow_hd";
                default:
                    return "tiles_land_hd";
            }
        }

        public static string CoastSheet(LandClimate climate)
        {
            switch (climate)
            {
                case LandClimate.Sand:
                    return "coast_sand_hd";
                case LandClimate.Snow:
                    return "coast_snow_hd";
                default:
                    return "coast_land_hd";
            }
        }
    }
}
