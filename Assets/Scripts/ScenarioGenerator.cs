using UnityEngine;

namespace ScenarioEditor
{
    public static class ScenarioGenerator
    {
        const int LandVariants = 15;
        const int SeaVariants = 7;

        public static ScenarioMap Generate(
            int width, int height, LandClimate climate, float waterPercent, string name, int seed)
        {
            width = Mathf.Clamp(width, 4, 80);
            height = Mathf.Clamp(height, 4, 80);

            var map = new ScenarioMap(width, height, climate, name);
            int total = width * height;
            int targetSea = Mathf.RoundToInt(total * Mathf.Clamp01(waterPercent / 100f));
            targetSea = Mathf.Clamp(targetSea, 0, total);

            var scores = new float[total];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (x + 0.5f) / width;
                    float ny = (y + 0.5f) / height;
                    float n =
                        0.50f * ValueNoise(nx * 3.1f, ny * 3.1f, seed) +
                        0.32f * ValueNoise(nx * 6.7f, ny * 6.7f, seed + 19) +
                        0.18f * ValueNoise(nx * 13.4f, ny * 13.4f, seed + 41);
                    float dx = (nx - 0.5f) * 2f;
                    float dy = (ny - 0.5f) * 2f;
                    float edge = Mathf.Sqrt(dx * dx + dy * dy);
                    scores[y * width + x] = n - edge * 0.30f;
                }
            }

            var sorted = (float[])scores.Clone();
            System.Array.Sort(sorted);
            float threshold = targetSea <= 0
                ? sorted[0] - 1f
                : targetSea >= total
                    ? sorted[total - 1] + 1f
                    : sorted[targetSea - 1];

            for (int i = 0; i < total; i++)
            {
                int x = i % width;
                int y = i / width;
                bool sea = scores[i] <= threshold;
                map.Cells[i] = new HexCell
                {
                    IsSea = sea,
                    Variant = Variant(x, y, sea),
                    MarkType = sea ? 1 : 0,
                    MarkIdx = Variant(x, y, sea)
                };
            }

            CleanIsolated(map);
            return map;
        }

        static void CleanIsolated(ScenarioMap map)
        {
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = map.Get(x, y);
                    int same = 0;
                    for (int dir = 0; dir < 6; dir++)
                    {
                        GoG.GoGHexGrid.Neighbor(x, y, dir, out int nx, out int ny);
                        bool neighborSea = !map.InBounds(nx, ny) || map.Get(nx, ny).IsSea;
                        if (neighborSea == cell.IsSea)
                            same++;
                    }

                    if (same == 0)
                    {
                        cell.IsSea = !cell.IsSea;
                        cell.Variant = Variant(x, y, cell.IsSea);
                        cell.MarkType = cell.IsSea ? 1 : 0;
                        cell.MarkIdx = cell.Variant;
                        map.Set(x, y, cell);
                    }
                }
            }
        }

        static int Variant(int x, int y, bool sea)
        {
            int mix = Mathf.Abs(x * 7 + y * 13 + x * y * 3);
            return sea ? mix % SeaVariants : mix % LandVariants;
        }

        static float ValueNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float tx = x - x0;
            float ty = y - y0;
            float sx = tx * tx * (3f - 2f * tx);
            float sy = ty * ty * (3f - 2f * ty);
            float a = Hash(x0, y0, seed);
            float b = Hash(x0 + 1, y0, seed);
            float c = Hash(x0, y0 + 1, seed);
            float d = Hash(x0 + 1, y0 + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        static float Hash(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0x00FFFFFF) / 16777215f;
        }
    }
}
