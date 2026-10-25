using Brooklyneir.Art;
using Brooklyneir.Audio;
using Brooklyneir.CameraSystem;
using Brooklyneir.Config;
using Brooklyneir.Gameplay;
using Brooklyneir.Progression;
using Brooklyneir.SaveSystem;
using Brooklyneir.Tables;
using Brooklyneir.UI;

namespace Brooklyneir.Core
{
    /// <summary>
    /// Access point to the game's services, assigned once by <see cref="GameRoot"/> at start-up.
    /// Kept deliberately small: systems receive what they need, and this is only used where
    /// passing references around would add noise (sounds, haptics, settings).
    /// </summary>
    public static class Game
    {
        public static BalanceConfig Balance { get; internal set; }
        public static SaveService Save { get; internal set; }
        public static Economy Economy { get; internal set; }
        public static AudioManager Audio { get; internal set; }
        public static Haptics Haptics { get; internal set; }
        public static ArenaManager Arena { get; internal set; }
        public static CameraDirector Camera { get; internal set; }
        public static VfxSystem Vfx { get; internal set; }
        public static MatchController Match { get; internal set; }
        public static UIManager UI { get; internal set; }
        public static AppFlow Flow { get; internal set; }
        public static PreviewStage Preview { get; internal set; }

        public static bool Ready { get { return Flow != null; } }
    }
}
