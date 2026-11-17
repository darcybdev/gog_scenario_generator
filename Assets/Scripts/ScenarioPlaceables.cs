using GoG;

namespace ScenarioEditor
{
    public enum PlaceKind
    {
        Land,
        Sea,
        Mark,
        Construction,
        Installation
    }

    public sealed class PlaceableItem
    {
        public PlaceKind Kind;
        public string Label;
        public string[] ImageNames;
        public int Id;
        public int Index;
        public int Level;

        public bool IsClear
        {
            get { return Kind == PlaceKind.Construction || Kind == PlaceKind.Installation ? Id == 0 : false; }
        }
    }

    public sealed class PlaceableGroup
    {
        public string Title;
        public PlaceableItem[] Items;
    }

    public static class ScenarioPlaceables
    {
        public const int MarkTypeMin = 4;
        public const int MarkTypeMax = 14;

        public static readonly string[] ConstructionNames =
        {
            null, "city", "factory", "airport", "port", "oil"
        };

        public static readonly string[] InstallationNames =
        {
            null, "fort", "bunker", "radar", "entrenchment", "antiaircraft"
        };

        static readonly string[][] ConstructionImages =
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

        public static PlaceableGroup[] Groups(LandClimate climate)
        {
            var catalog = GoGTerrainCatalog.Load();
            var groups = new PlaceableGroup[1 + (MarkTypeMax - MarkTypeMin + 1) + 2];
            int n = 0;
            groups[n++] = LandGroup(climate);
            for (int mark = MarkTypeMin; mark <= MarkTypeMax; mark++)
                groups[n++] = MarkGroup(mark, catalog);
            groups[n++] = InstallationGroup();
            groups[n++] = ConstructionGroup();
            return groups;
        }

        public static string MarkName(int markType)
        {
            int catalog = GoGTerrainCatalog.FromGameType(markType);
            if (catalog >= 0 && catalog < GoGTerrainCatalog.TypeNames.Length)
                return GoGTerrainCatalog.TypeNames[catalog];
            return "mark " + markType;
        }

        public static string ConstructionName(int id)
        {
            return id > 0 && id < ConstructionNames.Length ? ConstructionNames[id] : null;
        }

        public static string InstallationName(int id)
        {
            return id > 0 && id < InstallationNames.Length ? InstallationNames[id] : null;
        }

        public static string[] ConstructionImageSet(int id)
        {
            return id > 0 && id < ConstructionImages.Length ? ConstructionImages[id] : null;
        }

        public static string[] InstallationImageSet(int id)
        {
            return id > 0 && id < InstallationImages.Length ? InstallationImages[id] : null;
        }

        static PlaceableGroup LandGroup(LandClimate climate)
        {
            string prefix = ScenarioMap.LandPrefix(climate);
            var items = new PlaceableItem[15 + 7];
            for (int i = 0; i < 15; i++)
            {
                string image = prefix + (i + 1) + ".png";
                items[i] = new PlaceableItem
                {
                    Kind = PlaceKind.Land,
                    Label = "Land " + (i + 1),
                    ImageNames = new[] { image, "land" + (i + 1) + ".png" },
                    Index = i
                };
            }

            for (int i = 0; i < 7; i++)
            {
                items[15 + i] = new PlaceableItem
                {
                    Kind = PlaceKind.Sea,
                    Label = "Sea " + (i + 1),
                    ImageNames = new[] { "sea" + (i + 1) + ".png" },
                    Index = i
                };
            }

            return new PlaceableGroup { Title = "Land", Items = items };
        }

        static PlaceableGroup MarkGroup(int markType, GoGTerrainCatalog catalog)
        {
            string[] images = catalog != null
                ? catalog.Images(GoGTerrainCatalog.FromGameType(markType))
                : new string[0];
            var items = new PlaceableItem[images.Length];
            for (int i = 0; i < images.Length; i++)
            {
                items[i] = new PlaceableItem
                {
                    Kind = PlaceKind.Mark,
                    Label = ShortMarkLabel(markType, i),
                    ImageNames = string.IsNullOrEmpty(images[i]) ? new string[0] : new[] { images[i] },
                    Id = markType,
                    Index = i
                };
            }

            return new PlaceableGroup
            {
                Title = "Mark Type " + markType,
                Items = items
            };
        }

        static PlaceableGroup InstallationGroup()
        {
            var items = new PlaceableItem[6];
            items[0] = new PlaceableItem
            {
                Kind = PlaceKind.Installation,
                Label = "None",
                ImageNames = new string[0],
                Id = 0
            };
            for (int id = 1; id <= 5; id++)
            {
                items[id] = new PlaceableItem
                {
                    Kind = PlaceKind.Installation,
                    Label = TitleCase(InstallationNames[id]),
                    ImageNames = InstallationImages[id],
                    Id = id
                };
            }

            return new PlaceableGroup { Title = "Installation", Items = items };
        }

        static PlaceableGroup ConstructionGroup()
        {
            int count = 1;
            for (int id = 1; id <= 5; id++)
                count += ConstructionImages[id].Length;

            var items = new PlaceableItem[count];
            int n = 0;
            items[n++] = new PlaceableItem
            {
                Kind = PlaceKind.Construction,
                Label = "None",
                ImageNames = new string[0],
                Id = 0
            };
            for (int id = 1; id <= 5; id++)
            {
                var images = ConstructionImages[id];
                for (int i = 0; i < images.Length; i++)
                {
                    items[n++] = new PlaceableItem
                    {
                        Kind = PlaceKind.Construction,
                        Label = TitleCase(ConstructionNames[id]) + " " + (i + 1),
                        ImageNames = new[] { images[i] },
                        Id = id,
                        Index = i,
                        Level = i + 1
                    };
                }
            }

            return new PlaceableGroup { Title = "Construction", Items = items };
        }

        static string ShortMarkLabel(int markType, int index)
        {
            switch (markType)
            {
                case 4: return "Boreal " + (index + 1);
                case 5: return "Temp. " + (index + 1);
                case 6: return "Trop. " + (index + 1);
                case 7: return "Desert " + (index + 1);
                case 8: return "G.Hill " + (index + 1);
                case 9: return "G.Mtn " + (index + 1);
                case 10: return "P.Hill " + (index + 1);
                case 11: return "P.Mtn " + (index + 1);
                case 12: return "S.Mtn " + (index + 1);
                case 13: return "Swamp " + (index + 1);
                case 14: return "Farm " + (index + 1);
                default: return "Tile " + (index + 1);
            }
        }

        static string TitleCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }
    }
}
