using UnityEngine;

namespace Brooklyneir.Physics
{
    /// <summary>
    /// Regulation table dimensions (metres). The table is centred on the origin, its length runs
    /// along Z. Player 1 (index 0) defends z &lt; 0, player 2 (index 1) defends z &gt; 0.
    /// Every environment uses exactly these numbers, so changing scenery never changes physics.
    /// </summary>
    public static class TableGeometry
    {
        public const float Length = 2.74f;
        public const float Width = 1.525f;
        public const float HalfLength = Length * 0.5f;
        public const float HalfWidth = Width * 0.5f;
        public const float TopY = 0.76f;
        public const float BoardThickness = 0.03f;
        public const float NetHeight = 0.1525f;
        public const float NetOverhang = 0.1525f;
        public const float NetTopY = TopY + NetHeight;
        public const float NetHalfSpan = HalfWidth + NetOverhang;
        public const float LineWidth = 0.02f;

        /// <summary>Which half of the table a z coordinate belongs to: 0 for player 1, 1 for player 2.</summary>
        public static int SideOf(float z) { return z < 0f ? 0 : 1; }

        /// <summary>+1 for player 1 (attacks towards +z), -1 for player 2.</summary>
        public static float ForwardSign(int player) { return player == 0 ? 1f : -1f; }

        public static bool IsOverTable(float x, float z)
        {
            return Mathf.Abs(x) <= HalfWidth && Mathf.Abs(z) <= HalfLength;
        }

        /// <summary>Converts a "distance past the net" on the opponent half into a world z.</summary>
        public static float OpponentDepthToZ(int hitter, float depthPastNet)
        {
            return ForwardSign(hitter) * depthPastNet;
        }
    }
}
