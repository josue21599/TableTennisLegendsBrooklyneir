using Brooklyneir.Gameplay;
using UnityEngine;

namespace Brooklyneir.CameraSystem
{
    /// <summary>Which side of the table the match camera stands on.</summary>
    public enum ViewSide
    {
        /// <summary>Camera on +x looking at −x: player 1 on the left, player 2 on the right (default).</summary>
        PlusX = 0,
        /// <summary>Camera on −x looking at +x (turned 180°): player 2 on the left, player 1 on the right.</summary>
        MinusX = 1
    }

    /// <summary>
    /// The one place that knows how the screen's axes map onto the table for each camera side.
    /// The camera, the editor's keyboard controls and the tests use it, so they cannot disagree.
    /// Touch drags do not need it: they are projected through the real camera, so they follow
    /// whatever side it stands on. Pure, unit-tested.
    /// </summary>
    public static class ViewMapping
    {
        /// <summary>The camera turns 180° when the only player on this phone is player 2 (LAN guest).</summary>
        public static ViewSide SideFor(PlayerControl[] controls)
        {
            bool l0 = controls[0] == PlayerControl.Local, l1 = controls[1] == PlayerControl.Local;
            return l1 && !l0 ? ViewSide.MinusX : ViewSide.PlusX;
        }

        /// <summary>Sign of the camera's x position.</summary>
        public static float CameraXSign(ViewSide s) { return s == ViewSide.PlusX ? 1f : -1f; }

        /// <summary>World direction of "right" on the screen (along the table's length).</summary>
        public static Vector3 ScreenRight(ViewSide s) { return s == ViewSide.PlusX ? new Vector3(0f, 0f, 1f) : new Vector3(0f, 0f, -1f); }

        /// <summary>World direction of "up" on the screen (away from the camera, across the table).</summary>
        public static Vector3 ScreenUp(ViewSide s) { return s == ViewSide.PlusX ? new Vector3(-1f, 0f, 0f) : new Vector3(1f, 0f, 0f); }

        /// <summary>A movement on the screen (x = right, y = up) as a movement on the table plane.</summary>
        public static Vector3 ScreenToWorld(Vector2 screen, ViewSide s)
        {
            return ScreenRight(s) * screen.x + ScreenUp(s) * screen.y;
        }

        /// <summary>A direction on the table plane as seen on the screen (x = right, y = up).</summary>
        public static Vector2 WorldToScreen(Vector3 world, ViewSide s)
        {
            return new Vector2(Vector3.Dot(world, ScreenRight(s)), Vector3.Dot(world, ScreenUp(s)));
        }
    }
}
