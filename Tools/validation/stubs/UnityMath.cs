// Functional re-implementation of the small subset of UnityEngine math used by the
// pure-logic layer, so that layer can be compiled and unit-tested with Mono outside Unity.
// NOT shipped in the Unity project.
using System;

namespace UnityEngine
{
    [Serializable]
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; this.z = 0f; }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 down { get { return new Vector3(0, -1, 0); } }
        public static Vector3 forward { get { return new Vector3(0, 0, 1); } }
        public static Vector3 back { get { return new Vector3(0, 0, -1); } }
        public static Vector3 right { get { return new Vector3(1, 0, 0); } }
        public static Vector3 left { get { return new Vector3(-1, 0, 0); } }
        public float magnitude { get { return (float)Math.Sqrt(x * x + y * y + z * z); } }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
        public static Vector3 operator *(Vector3 a, float d) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        public static Vector3 operator *(float d, Vector3 a) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        public static Vector3 operator /(Vector3 a, float d) { return new Vector3(a.x / d, a.y / d, a.z / d); }
        public static bool operator ==(Vector3 a, Vector3 b) { return (a - b).sqrMagnitude < 1e-10f; }
        public static bool operator !=(Vector3 a, Vector3 b) { return !(a == b); }
        public override bool Equals(object o) { return o is Vector3 && this == (Vector3)o; }
        public override int GetHashCode() { return x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode(); }
        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public static Vector3 Cross(Vector3 a, Vector3 b) { return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x); }
        public static float Distance(Vector3 a, Vector3 b) { return (a - b).magnitude; }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) { return a + (b - a) * t; }
        public static Vector3 MoveTowards(Vector3 c, Vector3 t, float d)
        {
            Vector3 diff = t - c; float m = diff.magnitude;
            if (m <= d || m < 1e-6f) return t;
            return c + diff / m * d;
        }
        public static Vector3 ClampMagnitude(Vector3 v, float max) { float m = v.magnitude; return m > max ? v / m * max : v; }
        public static Vector3 Scale(Vector3 a, Vector3 b) { return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z); }
        public static Vector3 Project(Vector3 v, Vector3 n) { float s = n.sqrMagnitude; return s < 1e-9f ? zero : n * (Dot(v, n) / s); }
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            float x = currentVelocity.x, y = currentVelocity.y, z = currentVelocity.z;
            Vector3 r = new Vector3(
                Mathf.SmoothDamp(current.x, target.x, ref x, smoothTime, maxSpeed, deltaTime),
                Mathf.SmoothDamp(current.y, target.y, ref y, smoothTime, maxSpeed, deltaTime),
                Mathf.SmoothDamp(current.z, target.z, ref z, smoothTime, maxSpeed, deltaTime));
            currentVelocity = new Vector3(x, y, z);
            return r;
        }
        public override string ToString() { return string.Format("({0:F3}, {1:F3}, {2:F3})", x, y, z); }
        public static implicit operator Vector2(Vector3 v) { return new Vector2(v.x, v.y); }
    }

    [Serializable]
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero { get { return new Vector2(0, 0); } }
        public static Vector2 one { get { return new Vector2(1, 1); } }
        public float magnitude { get { return (float)Math.Sqrt(x * x + y * y); } }
        public float sqrMagnitude { get { return x * x + y * y; } }
        public Vector2 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator -(Vector2 a) { return new Vector2(-a.x, -a.y); }
        public static Vector2 operator *(Vector2 a, float d) { return new Vector2(a.x * d, a.y * d); }
        public static Vector2 operator *(float d, Vector2 a) { return new Vector2(a.x * d, a.y * d); }
        public static Vector2 operator /(Vector2 a, float d) { return new Vector2(a.x / d, a.y / d); }
        public static bool operator ==(Vector2 a, Vector2 b) { return (a - b).sqrMagnitude < 1e-10f; }
        public static bool operator !=(Vector2 a, Vector2 b) { return !(a == b); }
        public override bool Equals(object o) { return o is Vector2 && this == (Vector2)o; }
        public override int GetHashCode() { return x.GetHashCode() ^ y.GetHashCode(); }
        public static float Distance(Vector2 a, Vector2 b) { return (a - b).magnitude; }
        public static float Dot(Vector2 a, Vector2 b) { return a.x * b.x + a.y * b.y; }
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
        public static implicit operator Vector3(Vector2 v) { return new Vector3(v.x, v.y, 0); }
        public override string ToString() { return string.Format("({0:F2}, {1:F2})", x, y); }
    }

    public static class Mathf
    {
        public const float PI = 3.14159265358979f;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public const float Epsilon = 1.401298E-45f;
        public const float Infinity = float.PositiveInfinity;
        public static float Abs(float v) { return Math.Abs(v); }
        public static int Abs(int v) { return Math.Abs(v); }
        public static float Sqrt(float v) { return (float)Math.Sqrt(v); }
        public static float Sin(float v) { return (float)Math.Sin(v); }
        public static float Cos(float v) { return (float)Math.Cos(v); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float Atan(float v) { return (float)Math.Atan(v); }
        public static float Tan(float v) { return (float)Math.Tan(v); }
        public static float PerlinNoise(float x, float y) { return 0.5f; }
        public static float Pow(float a, float b) { return (float)Math.Pow(a, b); }
        public static float Exp(float a) { return (float)Math.Exp(a); }
        public static float Log(float a) { return (float)Math.Log(a); }
        public static float Sign(float v) { return v >= 0f ? 1f : -1f; }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static float Clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
        public static int Clamp(int v, int a, int b) { return v < a ? a : (v > b ? b : v); }
        public static float Clamp01(float v) { return Clamp(v, 0f, 1f); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static float LerpUnclamped(float a, float b, float t) { return a + (b - a) * t; }
        public static float InverseLerp(float a, float b, float v) { return Math.Abs(b - a) < 1e-9f ? 0f : Clamp01((v - a) / (b - a)); }
        public static float MoveTowards(float c, float t, float d) { return Math.Abs(t - c) <= d ? t : c + Sign(t - c) * d; }
        public static float Repeat(float t, float length) { return Clamp(t - (float)Math.Floor(t / length) * length, 0f, length); }
        public static float PingPong(float t, float length) { t = Repeat(t, length * 2f); return length - Math.Abs(t - length); }
        public static int RoundToInt(float v) { return (int)Math.Round(v, MidpointRounding.AwayFromZero); }
        public static int FloorToInt(float v) { return (int)Math.Floor(v); }
        public static int CeilToInt(float v) { return (int)Math.Ceiling(v); }
        public static float Round(float v) { return (float)Math.Round(v); }
        public static float Floor(float v) { return (float)Math.Floor(v); }
        public static bool Approximately(float a, float b) { return Math.Abs(a - b) < 1e-5f; }
        public static float SmoothStep(float a, float b, float t) { t = Clamp01(t); t = t * t * (3f - 2f * t); return a + (b - a) * t; }
        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            smoothTime = Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * deltaTime;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float originalTo = target;
            float maxChange = maxSpeed * smoothTime;
            change = Clamp(change, -maxChange, maxChange);
            target = current - change;
            float temp = (currentVelocity + omega * change) * deltaTime;
            currentVelocity = (currentVelocity - omega * temp) * exp;
            float output = target + (change + temp) * exp;
            if (originalTo - current > 0.0f == output > originalTo) { output = originalTo; currentVelocity = (output - originalTo) / deltaTime; }
            return output;
        }
    }
}
