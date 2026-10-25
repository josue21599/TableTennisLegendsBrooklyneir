using System; using UnityEngine; using Brooklyneir.Art;
public static class MeshCheck {
  static int fails = 0;
  static void Check(string name, Mesh m, bool expectInward = false) {
    int bad = 0, degenerate = 0, n = m.triangles.Length / 3;
    for (int i = 0; i < m.triangles.Length; i += 3) {
      int a = m.triangles[i], b = m.triangles[i+1], c = m.triangles[i+2];
      Vector3 f = Vector3.Cross(m.vertices[b]-m.vertices[a], m.vertices[c]-m.vertices[a]);
      if (f.sqrMagnitude < 1e-14f) { degenerate++; continue; }
      Vector3 avg = m.normals[a] + m.normals[b] + m.normals[c];
      float d = Vector3.Dot(f.normalized, avg.normalized);
      if (d < 0) bad++;
      if (expectInward && Vector3.Dot(avg, m.vertices[a] + m.vertices[b] + m.vertices[c]) > 0) bad++;
    }
    bool ok = bad == 0;
    if (!ok) fails++;
    Console.WriteLine((ok ? "OK   " : "FAIL ") + name.PadRight(28) + " tris=" + n + " wrongWinding=" + bad + " degenerate=" + degenerate);
  }
  public static int Main() {
    Check("Sphere", MeshFactory.Sphere(0.03f, 24, 16));
    Check("SkyDome (inward)", MeshFactory.SkyDome(60f), true);
    Check("Box", MeshFactory.Box(new Vector3(1f, 0.5f, 2f)));
    Check("RoundedBox", MeshFactory.RoundedBox(new Vector3(1.525f, 0.03f, 1.37f), 0.006f, 2));
    Check("RoundedBox2", MeshFactory.RoundedBox(new Vector3(0.36f, 0.22f, 0.3f), 0.05f, 3));
    Check("Cylinder", MeshFactory.Cylinder(0.05f, 0.3f, 16));
    Check("Frustum", MeshFactory.Frustum(0.2f, 0.05f, 0.4f, 16));
    Check("Capsule", MeshFactory.Capsule(0.1f, 0.6f, 12, 4));
    Check("Disc", MeshFactory.Disc(0.5f, 24));
    Check("Ring", MeshFactory.Ring(0.4f, 0.5f, 24));
    Check("Quad", MeshFactory.Quad(1f, 1f));
    Check("Plane", MeshFactory.Plane(2f, 3f, 1f));
    Check("Torus", MeshFactory.Torus(0.5f, 0.05f, 24, 8));
    Check("DoubleSidedQuad", MeshFactory.DoubleSidedQuad(2f, 0.15f, 10f, 2f));
    Vector2[] blade = new Vector2[48];
    for (int i = 0; i < 48; i++) { float t = 2f*Mathf.PI*i/48; blade[i] = new Vector2(0.076f*Mathf.Cos(t), 0.079f*Mathf.Sin(t)); }
    Check("Extrude (blade)", MeshFactory.Extrude(blade, 0.0065f));
    Check("OutlineBand", MeshFactory.OutlineBand(blade, 0.01f, 0.001f));
    Check("SmoothLathe", MeshFactory.SmoothLathe(new[]{ new Vector2(0f,-0.1f), new Vector2(0.016f,-0.1f), new Vector2(0.012f,-0.02f), new Vector2(0f,-0.002f)}, 20));
    // Rounded box surface check: every vertex lies on the bevelled surface.
    Mesh rb = MeshFactory.RoundedBox(new Vector3(1f, 0.4f, 0.6f), 0.05f, 3);
    Vector3 inner = new Vector3(0.45f, 0.15f, 0.25f); float maxErr = 0;
    foreach (var v in rb.vertices) { Vector3 c = new Vector3(Mathf.Clamp(v.x,-inner.x,inner.x),Mathf.Clamp(v.y,-inner.y,inner.y),Mathf.Clamp(v.z,-inner.z,inner.z)); maxErr = Mathf.Max(maxErr, Mathf.Abs((v-c).magnitude-0.05f)); }
    Console.WriteLine((maxErr < 1e-4f ? "OK   " : "FAIL ") + "RoundedBox surface error " + maxErr);
    if (maxErr >= 1e-4f) fails++;
    Console.WriteLine(fails == 0 ? "ALL MESHES OK" : fails + " MESH CHECKS FAILED");
    return fails;
  }
}
