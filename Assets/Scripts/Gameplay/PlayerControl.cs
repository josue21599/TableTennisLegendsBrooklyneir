namespace Brooklyneir.Gameplay
{
    /// <summary>Who moves a racket: nobody (training feeder side), a finger on this phone, the CPU or the network.</summary>
    public enum PlayerControl { None, Local, Cpu, Remote }

    /// <summary>
    /// Decides who controls each racket for a match configuration (pure, unit-tested). Reproduces
    /// exactly the rules the match used before this class existed; the only addition is the
    /// optional remote player, which is accepted only in the modes that can be played over LAN
    /// (quick match and custom) and ignored everywhere else.
    /// </summary>
    public static class ControlSetup
    {
        public static bool AllowsRemote(GameMode mode)
        {
            return mode == GameMode.QuickMatch || mode == GameMode.Custom;
        }

        /// <param name="remotePlayer">0 or 1 for the player on the other phone; -1 (default) for none.</param>
        public static PlayerControl[] For(MatchConfig config, int remotePlayer = -1)
        {
            PlayerControl[] c = new PlayerControl[2];
            bool demo = config.mode == GameMode.Demo;
            bool training = config.mode == GameMode.Training;
            c[0] = !config.player1IsAI && !demo ? PlayerControl.Local : PlayerControl.Cpu;
            if (!config.player2IsAI && !demo && !training) c[1] = PlayerControl.Local;
            else c[1] = training ? PlayerControl.None : PlayerControl.Cpu;
            if ((remotePlayer == 0 || remotePlayer == 1) && AllowsRemote(config.mode))
                c[remotePlayer] = PlayerControl.Remote;
            return c;
        }

        /// <summary>
        /// Editor debug ("ver como invitado"): a match against the CPU where the person plays player 2,
        /// like the LAN guest. Custom mode with player 1 as the CPU; the rackets swap so the person
        /// keeps theirs.
        /// </summary>
        public static MatchConfig AsSecondPlayer(MatchConfig source)
        {
            MatchConfig c = source.Clone();
            c.mode = GameMode.Custom;
            c.player1IsAI = true;
            c.player2IsAI = false;
            string r = c.racketP1; c.racketP1 = c.racketP2; c.racketP2 = r;
            return c;
        }

        /// <summary>True only for a finger on this phone (touch input, assist, magnet, hit-stop, vibration).</summary>
        public static bool IsLocal(PlayerControl c) { return c == PlayerControl.Local; }
    }
}
