namespace Brooklyneir.Controls
{
    /// <summary>
    /// Turns the mouse button state of one frame into pointer phases (editor/desktop only). A
    /// quick click can be pressed and released within the same frame: both phases must be sent,
    /// in that order, or the press is never released and the mouse stays stuck to the player
    /// that owned it. A pointer still tracked while the button is up is also released. Pure,
    /// unit-tested.
    /// </summary>
    public static class MousePointer
    {
        public struct Phases { public bool Began, Moved, Ended; }

        /// <param name="tracked">The router currently owns the mouse pointer.</param>
        public static Phases FromButton(bool pressedThisFrame, bool releasedThisFrame, bool isPressed, bool tracked)
        {
            Phases p = new Phases();
            if (pressedThisFrame) p.Began = true;
            if (releasedThisFrame && (pressedThisFrame || tracked)) p.Ended = true;
            if (!pressedThisFrame && !releasedThisFrame)
            {
                if (isPressed && tracked) p.Moved = true;
                else if (!isPressed && tracked) p.Ended = true; // lost release: never stay stuck
            }
            return p;
        }
    }
}
