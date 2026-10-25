// Functional mini-stubs so MeshFactory can run under Mono and its output can be inspected.
using System; using System.Collections.Generic;
namespace UnityEngine {
  public class Object { public string name { get; set; }
    public static T Instantiate<T>(T o) where T : Object { var m = o as Mesh; if (m != null) { var c = new Mesh(); c.vertices=(Vector3[])m.vertices.Clone(); c.normals=(Vector3[])m.normals.Clone(); c.uv=(Vector2[])m.uv.Clone(); c.triangles=(int[])m.triangles.Clone(); return c as T; } return o; } }
  public struct CombineInstance { public Mesh mesh; }
  public class Mesh : Object {
    public Vector3[] vertices { get; set; } public Vector3[] normals { get; set; } public Vector2[] uv { get; set; } public int[] triangles { get; set; }
    public UnityEngine.Rendering.IndexFormat indexFormat { get; set; }
    public void SetVertices(List<Vector3> v) { vertices = v.ToArray(); } public void SetNormals(List<Vector3> n) { normals = n.ToArray(); }
    public void SetUVs(int c, List<Vector2> u) { uv = u.ToArray(); } public void SetTriangles(List<int> t, int s) { triangles = t.ToArray(); }
    public void RecalculateBounds() { } public void RecalculateTangents() { } public void CombineMeshes(CombineInstance[] c, bool a, bool b) { } }
}
namespace UnityEngine.Rendering { public enum IndexFormat { UInt16, UInt32 } }
