namespace FoodIsekaiZ.Display
{
    /// <summary>
    /// Centralizes the Unity display indices used by the FoodIsekaiZ installation.
    /// Unity exposes the first physical display as index 0 and the second as index 1.
    /// </summary>
    public static class DisplayOutput
    {
        /// <summary>Unity index for the horizontal LED floor display (Display 2 on this installation).</summary>
        public const int FloorDisplayIndex = 1;

        /// <summary>Unity index for the vertical wall display (Display 1 on this installation).</summary>
        public const int WallDisplayIndex = 0;
    }
}
