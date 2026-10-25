using UnityEngine;

namespace Brooklyneir.Art
{
    /// <summary>
    /// Stylised gloved hand in a shakehand grip plus forearm sleeve, built to wrap the handle of
    /// a racket from <see cref="Brooklyneir.Rackets.RacketModelBuilder"/> (same local space:
    /// handle along -Y, blade facing Z). The player's colour is used for the sleeve and wristband.
    /// </summary>
    public static class HandModelBuilder
    {
        public static GameObject Build(Transform racketRoot, Color playerColor, Color skin, int layer = 0)
        {
            GameObject hand = new GameObject("Hand");
            hand.layer = layer;
            hand.transform.SetParent(racketRoot, false);

            Material glove = MaterialLibrary.Surface(SurfaceKind.Fabric, skin);
            Material sleeve = MaterialLibrary.Surface(SurfaceKind.Fabric, playerColor);
            Material band = MaterialLibrary.Surface(SurfaceKind.Fabric, Color.Lerp(playerColor, Color.white, 0.75f));

            // Palm wraps the handle (slightly bigger than it), offset to the back side.
            Part(hand, "Palm", MeshFactory.RoundedBox(new Vector3(0.042f, 0.06f, 0.034f), 0.014f, 3), glove, new Vector3(0.004f, -0.055f, 0.004f), Vector3.zero, layer);

            // Four fingers curl around the front of the handle.
            for (int i = 0; i < 4; i++)
            {
                float y = -0.032f - i * 0.0155f;
                Part(hand, "Finger" + i, MeshFactory.Capsule(0.0075f, 0.04f - i * 0.003f, 12, 4), glove,
                    new Vector3(-0.006f, y, -0.016f), new Vector3(0f, 0f, 90f), layer);
            }
            // Index finger extended along the backhand side of the blade.
            Part(hand, "Index", MeshFactory.Capsule(0.0072f, 0.05f, 12, 4), glove, new Vector3(0.016f, 0.024f, 0.0115f), new Vector3(0f, 0f, -35f), layer);
            // Thumb on the forehand side.
            Part(hand, "Thumb", MeshFactory.Capsule(0.0085f, 0.045f, 12, 4), glove, new Vector3(-0.012f, 0.004f, -0.0125f), new Vector3(0f, 0f, 28f), layer);

            // Wristband + sleeve (forearm), continuing past the handle end and dipping away from the
            // forehand face (local +Z), i.e. down and back towards the player in the game pose.
            Vector3 dir = new Vector3(0f, -Mathf.Cos(30f * Mathf.Deg2Rad), Mathf.Sin(30f * Mathf.Deg2Rad));
            Vector3 wrist = new Vector3(0.006f, -0.098f, 0.006f);
            Part(hand, "Wristband", MeshFactory.Cylinder(0.024f, 0.022f, 20), band, wrist + dir * 0.006f, new Vector3(-30f, 0f, 0f), layer);
            Part(hand, "Sleeve", MeshFactory.Frustum(0.031f, 0.025f, 0.16f, 20), sleeve, wrist + dir * 0.09f, new Vector3(-30f, 0f, 0f), layer);
            return hand;
        }

        static void Part(GameObject root, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 euler, int layer)
        {
            GameObject g = new GameObject(name);
            g.layer = layer;
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = pos;
            g.transform.localEulerAngles = euler;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
        }
    }
}
