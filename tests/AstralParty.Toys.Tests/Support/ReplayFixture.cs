using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.Loader;
using AstralParty.Toys.Services;

namespace AstralParty.Toys.Tests.Support;

/// <summary>
/// 合成回放的内容参数。ID 一律用 0 / 空数组表示「自动取配表里的真实 ID」，
/// 这样测试既贴合真实数据，又不会因为配表版本变化而失效。
/// </summary>
internal sealed record ReplayFixtureOptions
{
    public string ReplayId { get; init; } = "11111111111111111111111111111111";
    public long RoomId { get; init; } = 9_000_000_000_000_001;
    public int RoomServerId { get; init; } = 42;
    public int MapId { get; init; }
    public int MapType { get; init; } = 1;
    public int Difficulty { get; init; } = 1;
    public int MonsterId { get; init; }
    public int ItemId { get; init; }
    public int[] RelicIds { get; init; } = [];
    public int[] CardIds { get; init; } = [];
    public string GameVersion { get; init; } = "0.0.0-test";
    public int RoundCount { get; init; } = 3;
    public int GameProgress { get; init; } = 3;
    public int GameMaxProgress { get; init; } = 9;
    public int PlayerDeaths { get; init; } = 2;
    public int[] HeroIds { get; init; } = [];
    public long[] PlayerIds { get; init; } = [11, 22, 33, 44];
    public string[] Nicks { get; init; } = ["测试一号", "测试二号", "测试三号", "测试四号"];
    public long WinnerId { get; init; } = 11;
    public long StartTime { get; init; } = 1_760_000_000;
    public long FinishTime { get; init; } = 1_760_003_600;
    public int AwardCount { get; init; } = 5;
}

/// <summary>
/// 合成回放工厂：用**游戏自己的 protobuf 生成类**拼报文再序列化，因此不需要手工维护字段号或
/// wire type（这套 schema 的数值字段全是 sfixed，手写 varint 会被静默跳过）。
/// 产物既能让 <c>ReplayAnalyzer</c> 解析，也能让游戏自己的
/// <c>ReplayLoader.TryParseReplaySettlementOnly</c> 认出结算帧——所以测试离线、可重复，不需要真实录像。
/// </summary>
internal static class ReplayFixture
{
    private const int CmdRunningGame = 1003;
    private const int CmdGameFinish = 1016;
    private const int CmdReplaySnapshot = 1113;
    private const int CmdReplayDie = 1115;
    private const int CmdSelectRelic = 5212;
    private const int CmdPredictAction = 1002;
    private const int CmdPVEShopBuy = 5216;
    private const int CmdShopBuy = 5030;

    private const int RoomStateRunning = 25;   // Room.Types.State.Running
    private const int MonsterTypeBoss = 2;     // Hero.Types.MonsterType.Boss

    private static readonly Lazy<GameProtocolTypes> Game = new(() => new GameProtocolTypes());

    /// <summary>造一局回放，未指定的 ID 自动取配表里的真实值。</summary>
    public static byte[] Create(ReplayFixtureOptions? options = null) => Build(Resolve(options ?? new ReplayFixtureOptions()));

    /// <summary>按给定内容造一局回放（不做任何 ID 兜底）。</summary>
    public static byte[] Build(ReplayFixtureOptions options)
    {
        var game = Game.Value;
        var running = game.New("party.protocol.RunningGameS2C");
        game.SetMessage(running, "Room", BuildRoom(options, round: 1, final: false));
        var frames = new List<(int CmdId, byte[] Payload)>
        {
            (CmdRunningGame, game.Serialize(running))
        };

        for (var round = 1; round <= options.RoundCount; round++)
        {
            var playerId = options.PlayerIds[(round - 1) % options.PlayerIds.Length];
            var relicId = options.RelicIds[(round - 1) % options.RelicIds.Length];

            var snapshot = Game.Value.New("party.protocol.ReplaySnapshotS2C");
            Game.Value.SetNumber(snapshot, "PlayerId", playerId);
            Game.Value.SetMessage(snapshot, "Room", BuildRoom(options, round, final: round == options.RoundCount));
            frames.Add((CmdReplaySnapshot, Game.Value.Serialize(snapshot)));

            var die = Game.Value.New("party.protocol.ReplayDieS2C");
            Game.Value.SetNumber(die, "PlayerId", options.MonsterId * 1000L);   // 被击败的单位 ID
            Game.Value.SetNumber(die, "HeroId", options.MonsterId);            // 怪物配置 ID
            Game.Value.SetNumber(die, "KillerId", playerId);                   // 击杀者
            frames.Add((CmdReplayDie, Game.Value.Serialize(die)));

            var relic = Game.Value.New("party.protocol.SelectRelicS2C");
            Game.Value.SetNumber(relic, "PlayerId", playerId);
            Game.Value.SetNumber(relic, "RelicId", relicId);
            Game.Value.SetFlag(relic, "IsReroll", round == 2);                 // 中间那一回合走「请求刷新」分支
            frames.Add((CmdSelectRelic, Game.Value.Serialize(relic)));

            AddShopFrames(frames, options, playerId, round);
        }

        var finish = Game.Value.New("party.protocol.GameFinishS2C");
        Game.Value.SetNumber(finish, "RoomId", options.RoomId);
        Game.Value.SetNumber(finish, "Winer", options.WinnerId);
        Game.Value.SetNumber(finish, "FinishTime", options.FinishTime);
        Game.Value.SetEntry(finish, "Awards", options.ItemId, options.AwardCount);
        Game.Value.SetText(finish, "ReplayId", options.ReplayId);
        Game.Value.SetText(finish, "Version", options.GameVersion);
        Game.Value.SetNumber(finish, "MapType", options.MapType);
        frames.Add((CmdGameFinish, Game.Value.Serialize(finish)));

        return FrameStream(frames);
    }

    /// <summary>
    /// 造商店帧：进店 Action（1002 内嵌 5215 PVE / 5029 PVP）+ 购买回执（5216 / 5030）。
    /// 第一回合：PVE 商店，买了第 2 张卡；之后回合：PVP 商店，未购买直接关闭。
    /// 同一进店候选重复广播一次（同 Sn），验证去重合并。
    /// </summary>
    private static void AddShopFrames(List<(int CmdId, byte[] Payload)> frames, ReplayFixtureOptions options, long playerId, int round)
    {
        var game = Game.Value;
        var cards = options.CardIds.Length >= 3 ? options.CardIds : [.. options.CardIds];
        if (cards.Length == 0) return;

        var isPve = round == 1;
        var actionId = isPve ? 5215 : 5029;
        var sn = 10_000L + round;

        var message = game.New(isPve ? "party.protocol.PVEShopBuyC2S" : "party.protocol.ShopBuyC2S");
        var info = game.New("party.model.ActionInfo");
        game.SetNumber(info, "Sn", sn);
        game.SetMessage(message, "Info", info);
        foreach (var card in cards) game.Add(message, "Cards", card);
        game.SetNumber(message, "Gold", 3);
        if (isPve) game.SetNumber(message, "DisCountGold", round == 1 ? 1 : 0);
        foreach (var _ in cards) game.Add(message, "Alreadys", false);
        frames.Add((CmdPredictAction, BuildActionFrame(actionId, sn, playerId, message)));

        if (isPve)
        {
            // 买第 2 张（下标 1）
            var receipt = game.New("party.protocol.PVEShopBuyS2C");
            game.SetNumber(receipt, "PlayerId", playerId);
            game.Add(receipt, "BuyCards", 1);
            game.SetFlag(receipt, "IsClose", false);
            frames.Add((CmdPVEShopBuy, game.Serialize(receipt)));
            frames.Add((CmdPVEShopBuy, game.Serialize(CloseReceipt(receipt))));
        }
        else
        {
            var receipt = game.New("party.protocol.ShopBuyS2C");
            game.SetNumber(receipt, "PlayerId", playerId);
            frames.Add((CmdShopBuy, game.Serialize(receipt)));
        }
    }

    private static object CloseReceipt(object receipt)
    {
        var game = Game.Value;
        game.SetFlag(receipt, "IsClose", true);
        return receipt;
    }

    /// <summary>包一个 1002 帧：Actions 里塞一条带 Data 的 Action。</summary>
    private static byte[] BuildActionFrame(int actionId, long sn, long playerId, object data)
    {
        var game = Game.Value;
        var predict = game.New("party.protocol.PredictActionS2C");
        var action = game.New("party.model.Action");
        game.SetNumber(action, "Id", actionId);
        game.SetNumber(action, "Sn", sn);
        game.SetNumber(action, "PlayerId", playerId);
        game.SetBytes(action, "Data", game.Serialize(data));
        game.Add(predict, "Actions", action);
        return game.Serialize(predict);
    }

    /// <summary>把帧列表拼成文件：int16 大端 cmdId + int32 大端长度 + 载荷。</summary>
    public static byte[] FrameStream(IEnumerable<(int CmdId, byte[] Payload)> frames)
    {
        using var stream = new MemoryStream();
        Span<byte> header = stackalloc byte[6];
        foreach (var (cmdId, payload) in frames)
        {
            BinaryPrimitives.WriteInt16BigEndian(header, (short)cmdId);
            BinaryPrimitives.WriteInt32BigEndian(header[2..], payload.Length);
            stream.Write(header);
            stream.Write(payload);
        }
        return stream.ToArray();
    }

    /// <summary>造一个「看起来像回放、其实解析不出结算」的坏文件。</summary>
    public static byte[] CreateUnparseable(int length = 4096) =>
        [.. Enumerable.Range(0, length).Select(i => (byte)(i * 7 % 251))];

    /// <summary>把未指定（0 / 空）的 ID 换成配表里的真实 ID。</summary>
    public static ReplayFixtureOptions Resolve(ReplayFixtureOptions options)
    {
        var config = TestPaths.CreateAnalyzer().Config;
        return options with
        {
            MapId = options.MapId != 0 ? options.MapId : config.MapIds.First(),
            MonsterId = options.MonsterId != 0 ? options.MonsterId : config.MonsterIds.First(),
            ItemId = options.ItemId != 0 ? options.ItemId : config.ItemIds.First(),
            RelicIds = options.RelicIds.Length > 0 ? options.RelicIds : [.. config.RelicIds.Take(3)],
            CardIds = options.CardIds.Length > 0 ? options.CardIds : [.. config.CardIds.Take(3)],
            HeroIds = options.HeroIds.Length > 0 ? options.HeroIds : [.. config.CharacterIds.Take(4)]
        };
    }

    private static object BuildRoom(ReplayFixtureOptions options, int round, bool final)
    {
        var game = Game.Value;
        var room = game.New("party.model.Room");
        game.SetNumber(room, "Id", options.RoomId);
        game.SetText(room, "Name", "合成对局");
        game.SetNumber(room, "MapId", options.MapId);
        game.SetNumber(room, "MasterId", options.PlayerIds[0]);
        game.SetNumber(room, "CreateTime", options.StartTime);
        game.SetNumber(room, "UpdateTime", options.StartTime + round * 60);
        game.SetNumber(room, "State", RoomStateRunning);
        game.SetNumber(room, "Round", round);
        game.SetNumber(room, "StartTime", options.StartTime);
        game.SetNumber(room, "MapType", options.MapType);
        game.SetNumber(room, "Difficulty", options.Difficulty);
        game.SetNumber(room, "GameProgress", options.GameProgress);
        game.SetNumber(room, "GameMaxProgress", options.GameMaxProgress);
        game.SetNumber(room, "PlayerTotalDie", options.PlayerDeaths);
        game.SetNumber(room, "RoomServerId", options.RoomServerId);

        for (var index = 0; index < options.PlayerIds.Length; index++)
        {
            var player = game.New("party.model.Player");
            game.SetNumber(player, "Id", options.PlayerIds[index]);
            game.SetText(player, "Nick", options.Nicks[index % options.Nicks.Length]);
            game.SetNumber(player, "Slot", index);
            game.SetNumber(player, "Level", 30 + index);
            game.SetFlag(player, "IsBot", false);

            var hero = game.New("party.model.Hero");
            game.SetNumber(hero, "PlayerId", options.PlayerIds[index]);
            game.SetNumber(hero, "HeroId", options.HeroIds[index % options.HeroIds.Length]);
            game.SetNumber(hero, "Gold", 100 + index * 10 + round);
            game.SetNumber(hero, "MaxHp", 20);
            game.SetNumber(hero, "Hp", final && index == 1 ? 0 : 20 - index);   // 2 号玩家末局阵亡
            game.SetNumber(hero, "Attack", 5 + index);
            game.SetNumber(hero, "Defense", 3 + index);
            game.SetNumber(hero, "BuyRelicNum", index + round - 1);
            game.SetMessage(player, "Hero", hero);

            game.Add(room, "Players", player);
        }

        // 场上的 BOSS：Monsters 也是 RepeatedField<Player>，工具按 Hero.MonsterType 文本含 "BOSS" 判定
        var monster = game.New("party.model.Player");
        game.SetNumber(monster, "Id", options.MonsterId * 1000L);
        var monsterHero = game.New("party.model.Hero");
        game.SetNumber(monsterHero, "HeroId", options.MonsterId);
        game.SetNumber(monsterHero, "Hp", 60);
        game.SetNumber(monsterHero, "MaxHp", 60);
        game.SetNumber(monsterHero, "MonsterType", MonsterTypeBoss);
        game.SetMessage(monster, "Hero", monsterHero);
        game.Add(room, "Monsters", monster);

        return room;
    }
}

/// <summary>
/// 游戏协议生成类的反射包装：造消息、设字段、序列化。
/// 全部走反射，测试项目因此不需要引用游戏 DLL 里的 protobuf 运行时。
/// </summary>
internal sealed class GameProtocolTypes
{
    private readonly Assembly _game;
    private readonly MethodInfo _toByteArray;

    public GameProtocolTypes()
    {
        // GameProtocolContext 会注册依赖解析（Google.Protobuf.Runtime.dll 等），必须先建
        _ = new GameProtocolContext(Path.Combine(TestPaths.AppDirectory, "Protocol"));
        _game = AssemblyLoadContext.Default.LoadFromAssemblyPath(TestPaths.ProtocolAssembly);
        var protobuf = AssemblyLoadContext.Default.LoadFromAssemblyPath(
            Path.Combine(TestPaths.AppDirectory, "Protocol", "Google.Protobuf.Runtime.dll"));
        _toByteArray = protobuf.GetType("Google.Protobuf.MessageExtensions")!
                           .GetMethod("ToByteArray", [protobuf.GetType("Google.Protobuf.IMessage")!])
                       ?? throw new InvalidOperationException("Google.Protobuf 里找不到 MessageExtensions.ToByteArray。");
    }

    public object New(string typeName) => Activator.CreateInstance(Require(typeName))!;

    public void SetNumber(object target, string name, long value) =>
        SetValue(target, name, ConvertNumber(value, Property(target, name).PropertyType));

    public void SetFlag(object target, string name, bool value) => SetValue(target, name, value);

    public void SetText(object target, string name, string value) => SetValue(target, name, value);

    public void SetMessage(object target, string name, object value) => SetValue(target, name, value);

    /// <summary>给 bytes 字段（ByteString）赋值。</summary>
    public void SetBytes(object target, string name, byte[] value)
    {
        var property = Property(target, name);
        var byteString = property.PropertyType.Assembly.GetType("Google.Protobuf.ByteString")!;
        var fromArray = byteString.GetMethod("CopyFrom", [typeof(byte[])])!;
        SetValue(target, name, fromArray.Invoke(null, [value])!);
    }

    /// <summary>map&lt;int,int&gt; 的一条记录。</summary>
    public void SetEntry(object target, string name, int key, int value)
    {
        var map = Property(target, name).GetValue(target)!;
        map.GetType().GetMethod("Add", [typeof(int), typeof(int)])!.Invoke(map, [key, value]);
    }

    /// <summary>repeated 字段追加一项。</summary>
    public void Add(object target, string name, object item)
    {
        var collection = Property(target, name).GetValue(target)!;
        var add = collection.GetType().GetMethods()
            .FirstOrDefault(method => method.Name == "Add"
                                      && method.GetParameters().Length == 1
                                      && method.GetParameters()[0].ParameterType.IsInstanceOfType(item))
            ?? throw new InvalidOperationException($"{target.GetType().Name}.{name} 没有可用的 Add。");
        add.Invoke(collection, [item]);
    }

    public byte[] Serialize(object message) => (byte[])_toByteArray.Invoke(null, [message])!;

    private Type Require(string typeName) => _game.GetType(typeName, throwOnError: false)
        ?? throw new InvalidOperationException($"游戏协议里没有类型 {typeName}。");

    private static PropertyInfo Property(object target, string name) =>
        target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException($"{target.GetType().FullName} 没有属性 {name}。");

    private static void SetValue(object target, string name, object value)
    {
        var property = Property(target, name);
        try { property.SetValue(target, value); }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"{target.GetType().Name}.{name} 期望 {property.PropertyType.Name}，实际给了 {value.GetType().Name}。", exception);
        }
    }

    private static object ConvertNumber(long value, Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsEnum ? Enum.ToObject(underlying, value) : Convert.ChangeType(value, underlying);
    }
}
