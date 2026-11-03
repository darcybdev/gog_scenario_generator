using UnityEngine;

namespace GoG
{
    /// <summary>
    /// Flat-top hex metrics matching Glory of Generals HD tiles (186x162)
    /// and even-q offset coordinates (odd columns sit half a tile higher).
    /// </summary>
    public static class GoGHexGrid
    {
        public const float TileWidth = 186f;
        public const float TileHeight = 162f;
        public const float HorizStep = TileWidth * 0.75f;
        public const float OddColumnOffset = TileHeight * 0.5f;

        static readonly int[,] EvenColNeighbors =
        {
            { 1, 1 }, { 1, 0 }, { 0, -1 }, { -1, 0 }, { -1, 1 }, { 0, 1 }
        };

        static readonly int[,] OddColNeighbors =
        {
            { 1, 0 }, { 1, -1 }, { 0, -1 }, { -1, -1 }, { -1, 0 }, { 0, 1 }
        };

        static readonly int[,] EvenColCoastBits =
        {
            { 0, -1 }, { 1, 0 }, { 1, 1 }, { 0, 1 }, { -1, 1 }, { -1, 0 }
        };

        static readonly int[,] OddColCoastBits =
        {
            { 0, -1 }, { 1, -1 }, { 1, 0 }, { 0, 1 }, { -1, 0 }, { -1, -1 }
        };

        public static void CellOrigin(int x, int y, out float px, out float py)
        {
            px = x * HorizStep;
            py = y * TileHeight + ((x + 1) & 1) * OddColumnOffset;
        }

        public static void CellCenter(int x, int y, out float cx, out float cy)
        {
            CellOrigin(x, y, out float px, out float py);
            cx = px + TileWidth * 0.5f;
            cy = py + TileHeight * 0.5f;
        }

        public static Vector3 ToUnity(float screenX, float screenY, float pixelsPerUnit)
        {
            return new Vector3(screenX / pixelsPerUnit, -screenY / pixelsPerUnit, 0f);
        }

        public static void FromUnity(Vector3 world, float pixelsPerUnit, out float screenX, out float screenY)
        {
            screenX = world.x * pixelsPerUnit;
            screenY = -world.y * pixelsPerUnit;
        }

        public static float WorldWidth(int columns)
        {
            return HorizStep * (columns - 1) + TileWidth;
        }

        public static float WorldHeight(int rows)
        {
            return TileHeight * rows + OddColumnOffset;
        }

        public static void Neighbor(int x, int y, int dir, out int nx, out int ny)
        {
            var table = (x & 1) == 0 ? EvenColNeighbors : OddColNeighbors;
            nx = x + table[dir, 0];
            ny = y + table[dir, 1];
        }

        public static void CoastBitNeighbor(int x, int y, int bit, out int nx, out int ny)
        {
            var table = (x & 1) == 0 ? EvenColCoastBits : OddColCoastBits;
            nx = x + table[bit, 0];
            ny = y + table[bit, 1];
        }

        public static bool TryWorldToCell(
            Vector3 world, float pixelsPerUnit, int width, int height, out int x, out int y)
        {
            FromUnity(world, pixelsPerUnit, out float sx, out float sy);
            int guessX = Mathf.RoundToInt(sx / HorizStep);
            int guessY = Mathf.RoundToInt((sy - ((guessX + 1) & 1) * OddColumnOffset) / TileHeight);

            float best = float.MaxValue;
            int bx = -1;
            int by = -1;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int cx = guessX + dx;
                    int cy = guessY + dy;
                    if (cx < 0 || cy < 0 || cx >= width || cy >= height)
                        continue;
                    CellCenter(cx, cy, out float mx, out float my);
                    if (!ContainsPoint(sx - mx, sy - my))
                        continue;
                    float dist = (sx - mx) * (sx - mx) + (sy - my) * (sy - my);
                    if (dist < best)
                    {
                        best = dist;
                        bx = cx;
                        by = cy;
                    }
                }
            }

            x = bx;
            y = by;
            return bx >= 0;
        }

        public static bool ContainsPoint(float dx, float dy)
        {
            return ContainsPoint(dx, dy, TileWidth * 0.5f, TileHeight * 0.5f);
        }

        public static bool ContainsPoint(float dx, float dy, float halfW, float halfH)
        {
            float ax = Mathf.Abs(dx);
            float ay = Mathf.Abs(dy);
            float quarterW = halfW * 0.5f;
            if (ax > halfW || ay > halfH)
                return false;
            if (ax <= quarterW)
                return true;
            float t = (ax - quarterW) / quarterW;
            return ay <= halfH * (1f - t);
        }
    }
}
