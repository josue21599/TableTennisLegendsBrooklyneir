// Reflection runner for the NUnit shim. NOT shipped.
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public static class TestRunner
{
    public static int Main(string[] args)
    {
        int passed = 0, failed = 0;
        Assembly asm = typeof(TestRunner).Assembly;
        foreach (Type t in asm.GetTypes().Where(t => t.GetCustomAttributes(typeof(TestFixtureAttribute), false).Length > 0).OrderBy(t => t.Name))
        {
            foreach (MethodInfo m in t.GetMethods().Where(m => m.GetCustomAttributes(typeof(TestAttribute), false).Length > 0).OrderBy(m => m.Name))
            {
                object inst = Activator.CreateInstance(t);
                MethodInfo setup = t.GetMethods().FirstOrDefault(x => x.GetCustomAttributes(typeof(SetUpAttribute), false).Length > 0);
                try
                {
                    if (setup != null) setup.Invoke(inst, null);
                    m.Invoke(inst, null);
                    passed++;
                    Console.WriteLine("PASS  " + t.Name + "." + m.Name);
                }
                catch (TargetInvocationException e)
                {
                    failed++;
                    Console.WriteLine("FAIL  " + t.Name + "." + m.Name + " -> " + e.InnerException.Message);
                }
            }
        }
        Console.WriteLine();
        Console.WriteLine("Total: " + (passed + failed) + "  Passed: " + passed + "  Failed: " + failed);
        return failed == 0 ? 0 : 1;
    }
}
