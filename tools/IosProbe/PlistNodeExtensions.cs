// Netimobiledevice 的 plist 节点只给了 IDictionary<string, PropertyNode>，
// 拿值要自己转型；探针里统一用这几个小工具，免得散落一堆 cast。
using Netimobiledevice.Plist;

namespace IosProbe;

internal static class PlistNodeExtensions
{
    public static PropertyNode? Node(this DictionaryNode dict, string key) =>
        dict.TryGetValue(key, out var value) ? value : null;

    public static string? Text(this DictionaryNode dict, string key) =>
        dict.Node(key) switch
        {
            StringNode s => s.Value,
            DataNode d => Convert.ToHexString(d.Value)[..16] + "…",
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

    public static bool Flag(this DictionaryNode dict, string key) =>
        dict.Node(key) switch
        {
            BooleanNode b => b.Value,
            IntegerNode i => i.Value != 0,
            _ => false,
        };
}
