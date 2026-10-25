namespace Brooklyneir.Controls
{
    /// <summary>
    /// Assigns touches to players by finger id. Pure logic (unit-tested).
    ///
    /// Screen coordinates use Unity's convention (origin bottom-left). In two-player mode the left
    /// half of the screen belongs to player 1 and the right half to player 2 (the table runs along
    /// the screen's long axis). Each player owns at most one finger at
    /// a time; extra fingers in a zone are ignored, so one player can never steal or block the
    /// other's control. A finger keeps its owner even if it slides across the middle line.
    /// Touches that begin on a UI element are never assigned (buttons keep priority).
    /// </summary>
    public class TouchZoneRouter
    {
        public const int NoFinger = int.MinValue;
        readonly int[] _owners = { NoFinger, NoFinger };
        readonly bool[] _humanControlled = { true, true };

        /// <summary>When only one player is human, that player may use the whole screen.</summary>
        public void SetHumanControlled(int player, bool human)
        {
            _humanControlled[player] = human;
            if (!human) _owners[player] = NoFinger;
        }

        public bool IsHuman(int player) { return _humanControlled[player]; }

        public int FingerOf(int player) { return _owners[player]; }

        public void Reset()
        {
            _owners[0] = NoFinger;
            _owners[1] = NoFinger;
        }

        /// <summary>Returns the player that now owns this finger, or -1.</summary>
        public int Began(int fingerId, float screenX, float screenWidth, bool overUI)
        {
            if (overUI) return -1;
            if (OwnerOf(fingerId) >= 0) return OwnerOf(fingerId);
            int zone = ZoneFor(screenX, screenWidth);
            if (zone < 0) return -1;
            if (_owners[zone] != NoFinger) return -1;
            _owners[zone] = fingerId;
            return zone;
        }

        public int OwnerOf(int fingerId)
        {
            if (_owners[0] == fingerId) return 0;
            if (_owners[1] == fingerId) return 1;
            return -1;
        }

        /// <summary>Releases the finger; returns the player that owned it, or -1.</summary>
        public int Ended(int fingerId)
        {
            int owner = OwnerOf(fingerId);
            if (owner >= 0) _owners[owner] = NoFinger;
            return owner;
        }

        int ZoneFor(float x, float w)
        {
            bool h0 = _humanControlled[0], h1 = _humanControlled[1];
            if (h0 && !h1) return 0;
            if (h1 && !h0) return 1;
            if (!h0 && !h1) return -1;
            return x < w * 0.5f ? 0 : 1;
        }
    }
}
