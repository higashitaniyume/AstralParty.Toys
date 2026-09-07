using System.Collections;
using System.Reflection;

namespace AstralParty.ReplayTool.Services;

internal static class ReflectionValue
{
    public static object? Get(object? target, string name)
    {
        if (target is null) return null;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        return target.GetType().GetProperty(name, flags)?.GetValue(target)
               ?? target.GetType().GetField(name, flags)?.GetValue(target);
    }

    public static string Text(object? target, string name) => Get(target, name)?.ToString() ?? "";

    public static int Int(object? target, string name)
        => int.TryParse(Text(target, name), out var value) ? value : 0;

    public static long Long(object? target, string name)
        => long.TryParse(Text(target, name), out var value) ? value : 0L;

    public static bool Bool(object? target, string name)
        => bool.TryParse(Text(target, name), out var value) && value;

    public static IEnumerable<object> Items(object? value)
        => value is IEnumerable enumerable && value is not string
            ? enumerable.Cast<object>()
            : [];

    public static byte[] Bytes(object? byteString)
        => byteString?.GetType().GetMethod("ToByteArray", Type.EmptyTypes)?.Invoke(byteString, null) as byte[] ?? [];
}
