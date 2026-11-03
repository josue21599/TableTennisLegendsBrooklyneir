using Brooklyneir.Core;
using UnityEditor;

namespace Brooklyneir.EditorTools
{
    /// <summary>
    /// Brooklyneir → Depuración: development switches, remembered between editor sessions.
    /// "Ver como invitado": against the CPU you play as player 2 with the camera turned 180°,
    /// exactly as the LAN guest will see the match, without a network.
    /// </summary>
    [InitializeOnLoad]
    public static class DebugMenu
    {
        const string GuestKey = "Brooklyneir.Debug.ViewAsGuest";
        const string GuestItem = "Brooklyneir/Depuración/Ver como invitado (contra la CPU)";

        static DebugMenu()
        {
            DebugOptions.ViewAsGuest = EditorPrefs.GetBool(GuestKey, false);
        }

        [MenuItem(GuestItem, false, 100)]
        static void ToggleGuest()
        {
            DebugOptions.ViewAsGuest = !DebugOptions.ViewAsGuest;
            EditorPrefs.SetBool(GuestKey, DebugOptions.ViewAsGuest);
        }

        [MenuItem(GuestItem, true)]
        static bool ToggleGuestCheck()
        {
            Menu.SetChecked(GuestItem, DebugOptions.ViewAsGuest);
            return true;
        }
    }
}
