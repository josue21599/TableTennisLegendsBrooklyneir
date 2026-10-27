// Minimal NUnit-compatible shim so the project's EditMode tests can be compiled and run with
// Mono outside Unity. Same attribute/assert names and signatures as NUnit 3. NOT shipped.
using System;
using System.Collections;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Class)] public class TestFixtureAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class TestAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class SetUpAttribute : Attribute { }

    public class AssertionException : Exception { public AssertionException(string m) : base(m) { } }

    public class TearDownAttribute : System.Attribute { }
    public delegate void TestDelegate();
    public static class Assert
    {
        static void Fail(string what, string msg) { throw new AssertionException(what + (string.IsNullOrEmpty(msg) ? "" : " :: " + msg)); }
        public static void Fail(string msg) { Fail("Fail", msg); }
        public static void IsTrue(bool c, string msg = null) { if (!c) Fail("Expected true", msg); }
        public static void IsFalse(bool c, string msg = null) { if (c) Fail("Expected false", msg); }
        public static void IsNull(object o, string msg = null) { if (o != null) Fail("Expected null", msg); }
        public static void DoesNotThrow(TestDelegate code, string msg = null) { try { code(); } catch (System.Exception e) { Fail("Unexpected exception " + e.Message, msg); } }
        public static void IsNotNull(object o, string msg = null) { if (o == null) Fail("Expected not null", msg); }
        public static void AreEqual(object e, object a, string msg = null) { if (!Equals(e, a)) Fail("Expected " + e + " but was " + a, msg); }
        public static void AreEqual(int e, int a, string msg = null) { if (e != a) Fail("Expected " + e + " but was " + a, msg); }
        public static void AreEqual(float e, float a, float delta, string msg = null) { if (Math.Abs(e - a) > delta) Fail("Expected " + e + " +- " + delta + " but was " + a, msg); }
        public static void AreNotEqual(object e, object a, string msg = null) { if (Equals(e, a)) Fail("Expected different from " + e, msg); }
        public static void Greater(float a, float b, string msg = null) { if (!(a > b)) Fail("Expected " + a + " > " + b, msg); }
        public static void GreaterOrEqual(float a, float b, string msg = null) { if (!(a >= b)) Fail("Expected " + a + " >= " + b, msg); }
        public static void Less(float a, float b, string msg = null) { if (!(a < b)) Fail("Expected " + a + " < " + b, msg); }
        public static void LessOrEqual(float a, float b, string msg = null) { if (!(a <= b)) Fail("Expected " + a + " <= " + b, msg); }
        public static void Contains(object expected, ICollection actual, string msg = null)
        {
            foreach (object o in actual) if (Equals(o, expected)) return;
            Fail("Collection does not contain " + expected, msg);
        }
    }
}
