using Netimobiledevice.Plist;

namespace AstralParty.Toys.Mobile.Channels;

internal static class PlistNodeExtensions
{
    public static PropertyNode? Node(this DictionaryNode dict, string key) =>
        dict.TryGetValue(key, out var value) ? value : null;

    public static string? Text(this DictionaryNode dict, string key) =>
        dict.Node(key) switch
        {
            StringNode s => s.Value,
            IntegerNode i => i.Value.ToString(),
            BooleanNode b => b.Value.ToString(),
            _ => null,
        };

    public static long Number(this DictionaryNode dict, string key) =>
        dict.Node(key) switch
        {
            IntegerNode i => (long)i.Value,
            RealNode r => (long)r.Value,
            StringNode s when long.TryParse(s.Value, out var parsed) => parsed,
            _ => -1,
        };
}
