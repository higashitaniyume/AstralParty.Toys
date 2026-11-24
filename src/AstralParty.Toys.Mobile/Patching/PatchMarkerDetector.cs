using System.Text;

namespace AstralParty.Toys.Mobile.Patching;

/// <summary>
/// Detects the injected bootstrap symbol in an extracted Runtime DLL.
/// Measured on every pristine and injected assembly available (22b25522 pristine, 68b848c0 official
/// Windows, 213ed3ea and 8ea8562c injected), the symbol only ever appears as a UTF-16LE ldstr literal
/// in #US; the ASCII #Strings form was never observed. ASCII is still searched because it costs
/// nothing and would catch a differently-built payload, but UTF-16LE is what actually matches.
/// Both the current and legacy type names must be recognised (the rename is a destructive
/// change; a legacy-only marker means "needs re-injection", never "already patched").
/// </summary>
public static class PatchMarkerDetector
{
    public static readonly string[] TypeNames =
    [
        "CesiumLoader.Mobile.Bootstrap.Boot",
        "CesiumLoader.Android.Bootstrap.Boot",   // 旧名：兼容读，不能跟着批量替换
    ];

    public static bool ContainsMarker(ReadOnlySpan<byte> data)
    {
        foreach (var name in TypeNames)
        {
            if (ContainsAscii(data, name) || ContainsUtf16Le(data, name))
                return true;
        }

        return false;
    }

    public static bool ContainsCurrentMarker(ReadOnlySpan<byte> data) =>
        ContainsAscii(data, TypeNames[0]) || ContainsUtf16Le(data, TypeNames[0]);

    public static bool ContainsLegacyMarker(ReadOnlySpan<byte> data) =>
        ContainsAscii(data, TypeNames[1]) || ContainsUtf16Le(data, TypeNames[1]);

    private static bool ContainsAscii(ReadOnlySpan<byte> data, string text)
    {
        var needle = Encoding.ASCII.GetBytes(text);
        return data.IndexOf(needle) >= 0;
    }

    private static bool ContainsUtf16Le(ReadOnlySpan<byte> data, string text)
    {
        var needle = Encoding.Unicode.GetBytes(text);
        return data.IndexOf(needle) >= 0;
    }
}
