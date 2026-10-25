using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using Brooklyneir.Config;
public static class GenBalance {
  static void Write(object o, StringBuilder sb, int indent) {
    Type t = o.GetType();
    sb.Append("{\n");
    FieldInfo[] fs = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
    for (int i = 0; i < fs.Length; i++) {
      object v = fs[i].GetValue(o);
      sb.Append(new string(' ', indent + 2)).Append('"').Append(fs[i].Name).Append("\": ");
      if (v is float) sb.Append(((float)v).ToString("0.######", CultureInfo.InvariantCulture));
      else if (v is int) sb.Append(v.ToString());
      else if (v is bool) sb.Append(((bool)v) ? "true" : "false");
      else Write(v, sb, indent + 2);
      if (i < fs.Length - 1) sb.Append(',');
      sb.Append('\n');
    }
    sb.Append(new string(' ', indent)).Append('}');
  }
  public static void Main(string[] a) {
    BalanceConfig c = new BalanceConfig(); c.Validate();
    StringBuilder sb = new StringBuilder(); Write(c, sb, 0); Console.WriteLine(sb.ToString());
  }
}
