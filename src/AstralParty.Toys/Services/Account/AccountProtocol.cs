using System.Reflection;
using System.Runtime.Loader;

namespace AstralParty.Toys.Services.Account;

/// <summary>Isolated schema context: does not start Unity or share mutable replay parsers.</summary>
public sealed class AccountProtocol : IDisposable
{
    private sealed class SchemaContext(string directory) : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName name)
        {
            var simple = name.Name;
            if (simple == null || simple is "mscorlib" or "netstandard" or "System" ||
                simple.StartsWith("System.", StringComparison.Ordinal) || simple.StartsWith("Unity", StringComparison.Ordinal)) return null;
            var path = Path.Combine(directory, simple + ".dll");
            return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
        }
    }

    private readonly SchemaContext _context;
    private readonly Assembly _schema;
    private readonly MethodInfo _serialize;

    public AccountProtocol(string directory)
    {
        directory = Path.GetFullPath(directory);
        _context = new SchemaContext(directory);
        try
        {
            var protobuf = _context.LoadFromAssemblyPath(Path.Combine(directory, "Google.Protobuf.Runtime.dll"));
            _schema = _context.LoadFromAssemblyPath(Path.Combine(directory, "AstralParty.Runtime.dll"));
            _serialize = protobuf.GetType("Google.Protobuf.MessageExtensions", true)!.GetMethod("ToByteArray",
                [protobuf.GetType("Google.Protobuf.IMessage", true)!])!;
        }
        catch { _context.Unload(); throw; }
    }

    public object Parse(string name, byte[] bytes)
    {
        var parser = _schema.GetType("party.protocol." + name, true)!.GetProperty("Parser")!.GetValue(null)!;
        return parser.GetType().GetMethod("ParseFrom", [typeof(byte[])])!.Invoke(parser, [bytes])!;
    }

    public byte[] Create(string name, params (string Key, object Value)[] values)
    {
        var type = _schema.GetType("party.protocol." + name, true)!;
        var message = Activator.CreateInstance(type)!;
        foreach (var (key, value) in values) type.GetProperty(key)!.SetValue(message, value);
        return (byte[])_serialize.Invoke(null, [message])!;
    }

    public static object? Get(object? value, string name) => value?.GetType().GetProperty(name)?.GetValue(value);
    public static long Number(object? value, string name) => Convert.ToInt64(Get(value, name) ?? 0, System.Globalization.CultureInfo.InvariantCulture);
    public static string Text(object? value, string name) => Get(value, name)?.ToString() ?? "";
    public static bool Flag(object? value, string name) => Get(value, name) is true;
    public static IEnumerable<object> Items(object? value, string name) =>
        (Get(value, name) as System.Collections.IEnumerable)?.Cast<object>() ?? [];
    public void Dispose() => _context.Unload();
}
