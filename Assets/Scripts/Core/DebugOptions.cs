namespace Brooklyneir.Core
{
    /// <summary>
    /// Development switches set from the editor's Brooklyneir → Depuración menu. They have no
    /// effect in a normal build.
    /// </summary>
    public static class DebugOptions
    {
        /// <summary>Against the CPU, play as player 2 with the camera turned 180° (the LAN guest's view).</summary>
        public static bool ViewAsGuest;
    }
}
