using System; using System.IO; using System.Globalization; using System.Collections.Generic; using UnityEngine;
using Brooklyneir.Art; using Brooklyneir.Rackets; using Brooklyneir.Tables; using Brooklyneir.Physics;
public static class Export {
  static int Base = "_BaseColor".GetHashCode();
  static Color Sample(Texture2D t, Vector2 uv) {
    if (t == null || t.Px == null) return Color.white;
    float u = uv.x - Mathf.Floor(uv.x), v = uv.y - Mathf.Floor(uv.y);
    int x = Mathf.Clamp((int)(u * t.W), 0, t.W - 1), y = Mathf.Clamp((int)(v * t.H), 0, t.H - 1);
    Color32 c = t.Px[y * t.W + x]; return new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
  }
  static void Dump(string path) {
    var inv = CultureInfo.InvariantCulture; int n = 0;
    using (var w = new StreamWriter(path)) {
      foreach (var g in GameObject.All) {
        if (!g.ActiveInHierarchy) continue;
        var mf = g.GetComponent<MeshFilter>(); var mr = g.GetComponent<MeshRenderer>();
        if (mf == null || mr == null || mf.sharedMesh == null || mr.sharedMaterial == null) continue;
        Mesh m = mf.sharedMesh; Material mat = mr.sharedMaterial; Color bc; if (!mat.Colors.TryGetValue(Base, out bc)) bc = Color.white;
        Vector3[] wv = new Vector3[m.vertices.Length];
        for (int i = 0; i < wv.Length; i++) wv[i] = g.transform.TransformPoint(m.vertices[i]);
        for (int i = 0; i < m.triangles.Length; i += 3) {
          int a = m.triangles[i], b = m.triangles[i + 1], c = m.triangles[i + 2];
          Vector2 uv = (m.uv[a] + m.uv[b] + m.uv[c]) / 3f;
          Color col = bc * Sample(mat.Tex as Texture2D, uv);
          if (col.a < 0.05f) continue;
          w.WriteLine(string.Join(" ", new[] { wv[a].x, wv[a].y, wv[a].z, wv[b].x, wv[b].y, wv[b].z, wv[c].x, wv[c].y, wv[c].z, col.r, col.g, col.b, col.a, mat.Emissive ? 1f : 0f }.Length > 0 ?
            Array.ConvertAll(new[] { wv[a].x, wv[a].y, wv[a].z, wv[b].x, wv[b].y, wv[b].z, wv[c].x, wv[c].y, wv[c].z, col.r, col.g, col.b, col.a, mat.Emissive ? 1f : 0f }, f => f.ToString("0.####", inv)) : new string[0]));
          n++;
        }
      }
    }
    Console.WriteLine(path + ": " + n + " triangles");
  }
  static void PlaceRacket(RacketDefinition def, int player, float x, float z) {
    GameObject holder = new GameObject("RacketHolder");
    GameObject racket = RacketModelBuilder.Build(def, holder.transform);
    HandModelBuilder.Build(racket.transform, player == 0 ? new Color(1f, 0.48f, 0.1f) : new Color(0.13f, 0.76f, 1f), new Color(0.93f, 0.92f, 0.9f));
    racket.transform.localScale = Vector3.one * 2.2f;
    float fwd = player == 0 ? 1f : -1f;
    Quaternion rot = RacketModelBuilder.Pose(fwd, 28f, 12f, 0f);
    holder.transform.position = new Vector3(x, 0.95f, z);
    racket.transform.localRotation = rot;
    racket.transform.localPosition = -(rot * new Vector3(0f, RacketModelBuilder.BladeCentreY * 2.2f, 0f));
  }
  public static void Main(string[] args) {
    MaterialLibrary.Init();
    string outDir = args[0];
    // 1) Each table with its environment and two rackets.
    foreach (TableDefinition t in TableCatalog.All) {
      GameObject.All.Clear();
      GameObject root = new GameObject("Arena");
      EnvironmentBuilder.Build(t, root.transform);
      TableModelBuilder.Build(t, root.transform);
      PlaceRacket(RacketCatalog.Get("classic_oak"), 0, 0.25f, -1.7f);
      PlaceRacket(RacketCatalog.Get("neon_pulse"), 1, -0.2f, 1.7f);
      // ball
      GameObject ball = new GameObject("Ball"); ball.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Sphere(0.032f, 20, 14);
      ball.AddComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Surface(SurfaceKind.Ball, new Color(1f, 0.97f, 0.92f)); ball.transform.position = new Vector3(0.1f, 1.05f, -0.55f);
      Dump(Path.Combine(outDir, "scene_" + t.Id + ".tri"));
    }
    // 2) Racket showcase.
    GameObject.All.Clear();
    int i = 0;
    foreach (RacketDefinition r in RacketCatalog.All) {
      GameObject holder = new GameObject("Show");
      GameObject racket = RacketModelBuilder.Build(r, holder.transform);
      holder.transform.position = new Vector3((i % 4) * 0.22f - 0.33f, (i / 4) * -0.33f, 0f);
      holder.transform.localRotation = Quaternion.Euler(0f, 160f, 0f);
      i++;
    }
    Dump(Path.Combine(outDir, "rackets.tri"));
  }
}
