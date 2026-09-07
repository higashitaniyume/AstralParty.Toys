using System.Reflection;
using System.Runtime.Loader;

namespace AstralParty.Toys.Services;

public sealed class GameProtocolContext
{
    private readonly Assembly _gameAssembly;
    private readonly Dictionary<string, object> _parsers = new(StringComparer.Ordinal);

    public static readonly IReadOnlyDictionary<int, string> MessageTypes = new Dictionary<int, string>
    {
        [1002] = "party.protocol.PredictActionS2C",
        [1003] = "party.protocol.RunningGameS2C",
        [1016] = "party.protocol.GameFinishS2C",
        [1040] = "party.protocol.UpdateHeroAttrS2C",
        [1096] = "party.protocol.HeroSkillMoveEffectS2C",
        [1098] = "party.protocol.SayPhraseNotifyS2C",
        [1112] = "party.protocol.SyncRelicsS2C",
        [1113] = "party.protocol.ReplaySnapshotS2C",
        [1115] = "party.protocol.ReplayDieS2C",
        [5212] = "party.protocol.SelectRelicS2C",
        [5214] = "party.protocol.MonsterPursuitS2C",
        [5216] = "party.protocol.PVEShopBuyS2C",
        [5250] = "party.protocol.BuyRelicS2C"
    };

    public GameProtocolContext(string protocolDirectory)
    {
        var gamePath = Path.Combine(protocolDirectory, "AstralParty.Runtime.dll");
        if (!File.Exists(gamePath))
            throw new FileNotFoundException("找不到游戏协议程序集 AstralParty.Runtime.dll。", gamePath);

        Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
        {
            if (name.Name is null || name.Name is "System.Runtime" or "netstandard" or "mscorlib" || name.Name.StartsWith("System."))
                return null;
            var candidate = Path.Combine(protocolDirectory, name.Name + ".dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        }

        AssemblyLoadContext.Default.Resolving += Resolve;
        _gameAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(gamePath);
    }

    public object Decode(string typeName, byte[] payload)
    {
        if (!_parsers.TryGetValue(typeName, out var parser))
        {
            var type = _gameAssembly.GetType(typeName, throwOnError: true)!;
            parser = type.GetProperty("Parser", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                     ?? throw new InvalidOperationException($"{typeName} 没有 protobuf Parser。");
            _parsers[typeName] = parser;
        }

        return parser.GetType().GetMethod("ParseFrom", new[] { typeof(byte[]) })?.Invoke(parser, new object[] { payload })
               ?? throw new InvalidDataException($"无法解码 {typeName}。");
    }

    public object? TryDecode(int cmdId, byte[] payload)
    {
        if (!MessageTypes.TryGetValue(cmdId, out var typeName) || payload.Length == 0) return null;
        try { return Decode(typeName, payload); }
        catch { return null; }
    }

    public string MessageName(int cmdId)
        => MessageTypes.TryGetValue(cmdId, out var typeName) ? typeName.Split('.').Last() : $"CMD {cmdId}";

    public string FormatFrame(ProtocolFrame frame)
    {
        if (frame.Payload.Length == 0) return "（空载荷）";
        var decoded = TryDecode(frame.CmdId, frame.Payload);
        if (decoded is not null) return decoded.ToString() ?? "（已解码，无可显示字段）";

        var shown = frame.Payload.Take(4096).ToArray();
        var hex = Convert.ToHexString(shown);
        var grouped = string.Join(' ', Enumerable.Range(0, (hex.Length + 1) / 2)
            .Select(i => hex.Substring(i * 2, Math.Min(2, hex.Length - i * 2))));
        return frame.Payload.Length > shown.Length
            ? $"未注册的消息类型。原始十六进制（前 {shown.Length} 字节）：\n{grouped}\n\n已省略 {frame.Payload.Length - shown.Length:N0} 字节。"
            : $"未注册的消息类型。原始十六进制：\n{grouped}";
    }
}
