// CesiumLoader iOS 侧注入器（Mono.Cecil）。
//
//   probe <in.dll> <out.dll> [--name <文件名>] [--text <内容>] [--refs <目录>]
//
// 设计原则：**贴着安卓已验证的形态**。
// 安卓成功的标记注入只有一条 File::WriteAllText 加一个写死的绝对路径，内联在
// <Module>.cctor 里。iOS 唯一多出来的要求是容器路径带 UUID、只能在运行时解析，
// 所以这里把路径换成 Application::get_persistentDataPath() + "/<name>"。
//
// 因此本命令注入的调用面**只有三个**：
//   Application::get_persistentDataPath()   （游戏自己就在用，Addressables 依赖它）
//   System.String::Concat(string, string)
//   System.IO.File::WriteAllText(string, string)
// 并且：
//   - 不新增任何方法（只建/前插 <Module>.cctor），避免动 <Module> 的类型形状；
//   - 整段一个 try/catch，异常只吞掉 —— cctor 抛出会让 AstralParty.Runtime 的
//     所有类型都无法初始化，表现是启动即卡死且没有可用堆栈。
//
// 之前那版探针用了 7 个调用 + 在 <Module> 上新增方法，明显越界，已废弃。

using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

// 输出固定用 UTF-8：调用方（Python）按 UTF-8 读；接了控制台时顺带把控制台代码页切成
// UTF-8，中文提示在管道里和控制台上都是对的（Windows 默认 GBK，管道里按 UTF-8 解会乱码）。
try
{
    Console.OutputEncoding = new UTF8Encoding(false);
}
catch (IOException)
{
    // 没有控制台句柄时保持默认，不影响注入
}

if (args.Length < 3)
{
    Usage();
    return 2;
}

try
{
    return args[0] switch
    {
        "probe" => Probe(args[1], args[2],
                         Option(args, "--name") ?? "cesium_probe.txt",
                         Option(args, "--text") ?? "cesium ios probe ok",
                         Refs(args)),
        "bootstrap" => Bootstrap(args[1], args[2],
                         Option(args, "--name") ?? "cesium_probe.txt",
                         Option(args, "--text") ?? "cesium ios probe ok",
                         Option(args, "--dir") ?? "CesiumLoader",
                         Option(args, "--boot-dll") ?? "CesiumLoader.Mobile.Bootstrap.dll",
                         Option(args, "--boot-type") ?? "CesiumLoader.Mobile.Bootstrap.Boot",
                         Option(args, "--boot-method") ?? "LoadForGame",
                         Option(args, "--boot-arg") ?? "root",
                         Refs(args)),
        _ => Unknown(args[0]),
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"失败: {ex.Message}");
    return 1;
}

static void Usage()
{
    Console.WriteLine("""
        cesium-ios-patcher —— iOS 侧注入器

          probe <in.dll> <out.dll> [--name <file>] [--text <内容>]

        所有命令都接受 --refs <目录>（可重复），用于解析被引用程序集。
        """);
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"未知命令: {command}");
    Usage();
    return 2;
}

static string? Option(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (args[i] == name) return args[i + 1];
    return null;
}

static string[] Refs(string[] args)
{
    var dirs = new List<string>();
    for (var i = 0; i < args.Length - 1; i++)
        if (args[i] == "--refs") dirs.Add(args[i + 1]);
    return dirs.ToArray();
}

static AssemblyDefinition Read(string path, string[] refDirs)
{
    var resolver = new DefaultAssemblyResolver();
    resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    foreach (var d in refDirs) resolver.AddSearchDirectory(d);
    return AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver });
}

static string Sha256(string path)
{
    using var sha = System.Security.Cryptography.SHA256.Create();
    using var fs = File.OpenRead(path);
    return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
}

static int CountTypes(ModuleDefinition m) => AllTypes(m).Count();
static int CountMethods(ModuleDefinition m) => AllTypes(m).Sum(t => t.Methods.Count);

static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition m)
{
    foreach (var t in m.Types)
    {
        yield return t;
        foreach (var n in Nested(t)) yield return n;
    }
}

static IEnumerable<TypeDefinition> Nested(TypeDefinition t)
{
    foreach (var n in t.NestedTypes)
    {
        yield return n;
        foreach (var x in Nested(n)) yield return x;
    }
}

static int Probe(string input, string output, string fileName, string text, string[] refDirs)
{
    using var assembly = Read(input, refDirs);
    var module = assembly.MainModule;
    var before = (Types: CountTypes(module), Methods: CountMethods(module), Refs: module.AssemblyReferences.Count);

    var moduleType = module.Types.FirstOrDefault(t => t.Name == "<Module>")
                     ?? throw new InvalidOperationException("找不到 <Module> 类型");
    var corlib = module.AssemblyReferences.FirstOrDefault(r => r.Name == "mscorlib")
                 ?? throw new InvalidOperationException("找不到 mscorlib 引用");
    var unity = module.AssemblyReferences.FirstOrDefault(r => r.Name == "UnityEngine.CoreModule")
                ?? throw new InvalidOperationException("找不到 UnityEngine.CoreModule 引用");

    var str = module.TypeSystem.String;

    var stringType = new TypeReference("System", "String", module, corlib);
    var concat2 = new MethodReference("Concat", str, stringType) { HasThis = false };
    concat2.Parameters.Add(new ParameterDefinition(str));
    concat2.Parameters.Add(new ParameterDefinition(str));

    var fileType = new TypeReference("System.IO", "File", module, corlib);
    var writeAllText = new MethodReference("WriteAllText", module.TypeSystem.Void, fileType) { HasThis = false };
    writeAllText.Parameters.Add(new ParameterDefinition(str));
    writeAllText.Parameters.Add(new ParameterDefinition(str));

    var appType = new TypeReference("UnityEngine", "Application", module, unity);
    var getPp = new MethodReference("get_persistentDataPath", str, appType) { HasThis = false };

    var exceptionType = new TypeReference("System", "Exception", module, corlib);

    var cctor = moduleType.Methods.FirstOrDefault(m => m.Name == ".cctor" && m.IsStatic);
    var createdCctor = cctor == null;
    if (cctor == null)
    {
        cctor = new MethodDefinition(
            ".cctor",
            MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig |
            MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            module.TypeSystem.Void);
        cctor.Body = new MethodBody(cctor);
        moduleType.Methods.Add(cctor);
    }
    else if (!cctor.HasBody)
    {
        throw new InvalidOperationException("<Module>.cctor 没有方法体，无法前插");
    }

    var body = cctor.Body;
    var il = body.GetILProcessor();

    // 已存在 cctor 时把注入段前插到原有第一条指令之前；否则新建一个 Ret 作为落点。
    var end = createdCctor ? il.Create(OpCodes.Ret) : body.Instructions[0];

    var tryStart = il.Create(OpCodes.Call, getPp);
    var handlerStart = il.Create(OpCodes.Pop);

    var block = new List<Instruction>
    {
        tryStart,                                          // Application.persistentDataPath
        il.Create(OpCodes.Ldstr, "/" + fileName),
        il.Create(OpCodes.Call, concat2),                  // -> "<pp>/<file>"
        il.Create(OpCodes.Ldstr, text),
        il.Create(OpCodes.Call, writeAllText),             // File.WriteAllText(path, text)
        il.Create(OpCodes.Leave, end),
        handlerStart,                                      // catch { }
        il.Create(OpCodes.Leave, end),
    };

    if (createdCctor)
    {
        foreach (var ins in block) il.Append(ins);
        il.Append(end);
    }
    else
    {
        var anchor = body.Instructions[0];
        foreach (var ins in block) il.InsertBefore(anchor, ins);
    }

    body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Catch)
    {
        TryStart = tryStart,
        TryEnd = handlerStart,
        HandlerStart = handlerStart,
        HandlerEnd = end,
        CatchType = exceptionType,
    });

    assembly.Write(output, new WriterParameters { DeterministicMvid = false });

    // ---- 回读校验 ----
    using var check = Read(output, refDirs);
    var checkModule = check.MainModule.Types.First(t => t.Name == "<Module>");
    var checkCctor = checkModule.Methods.FirstOrDefault(m => m.Name == ".cctor" && m.IsStatic);
    var refs = checkCctor?.Body?.Instructions
        .Select(i => (i.Operand as MethodReference)?.Name)
        .Where(n => n != null).ToList() ?? new List<string?>();
    var hasPp = refs.Contains("get_persistentDataPath");
    var hasWrite = refs.Contains("WriteAllText");
    var hasConcat = refs.Contains("Concat");
    var extraMethods = checkModule.Methods.Count(m => m.Name != ".cctor");
    var handlers = checkCctor?.Body?.ExceptionHandlers.Count ?? 0;

    var after = (Types: CountTypes(check.MainModule), Methods: CountMethods(check.MainModule), Refs: check.MainModule.AssemblyReferences.Count);

    Console.WriteLine($"注入 <Module>.cctor -> File::WriteAllText(Application.persistentDataPath + \"/{fileName}\")");
    Console.WriteLine($"cctor         : {(createdCctor ? "新建（该程序集原本没有）" : "已存在，注入段前插，原有指令保留")}");
    Console.WriteLine($"调用面        : get_persistentDataPath / String::Concat / File::WriteAllText（共 3 个）");
    Console.WriteLine($"输入          : {Path.GetFileName(input)}  {new FileInfo(input).Length} B  {Sha256(input)}");
    Console.WriteLine($"输出          : {Path.GetFileName(output)}  {new FileInfo(output).Length} B  {Sha256(output)}");
    Console.WriteLine($"结构          : 类型 {before.Types} -> {after.Types}，方法 {before.Methods} -> {after.Methods}，引用 {before.Refs} -> {after.Refs}");
    Console.WriteLine($"校验 : 三个调用 {hasPp}/{hasConcat}/{hasWrite}，异常护栏 {handlers} 段");
    Console.WriteLine($"校验 : <Module> 上除 cctor 外的方法 {extraMethods} 个（应为 0）");
    return hasPp && hasWrite && hasConcat && handlers == 1 && extraMethods == 0 ? 0 : 1;
}

/// <summary>
/// 探针 + 引导加载，两段互相独立的 try/catch 内联在 &lt;Module&gt;.cctor 里。
///
/// 加载链与安卓 inject-bootstrap 同形：
///   Assembly.Load(File.ReadAllBytes(dll)) -&gt; GetType(type) -&gt; GetMethod(name) -&gt; Invoke
/// 两处 iOS 侧的必要差异：
///   1. 路径运行时算（iOS 容器带 UUID）：Application.persistentDataPath；
///   2. 调 <c>Boot.LoadForGame(root)</c> 而不是 <c>Boot.Load()</c>，把根目录显式传进去。
///      安卓那份 Boot.ResolveRoot() 靠 TryPersistentDataRoot() 兜底，而后者用的是
///      双参 Assembly.GetType(string, bool) —— 该重载在 HybridCLR 上被裁掉（AGENTS 点名、
///      MobileTouch.cs 也记过）。让它抛出来只会退到 /sdcard 那几个安卓专属候选，
///      iOS 上必然全失败。LoadForGame(root) 里的 TryPrepare(root) 一成功就不会再走
///      ResolveRoot()，所以这条路完全绕开那个裁剪坑，也不用改安卓产物。
///
/// 每一步都写一个 &lt;persistentDataPath&gt;/cesium_ios_&lt;step&gt;.txt 标记：标记是纯
/// WriteAllText，是最不可能出问题的一步，用文件在不在就能把失败点钉死。
/// </summary>
static int Bootstrap(string input, string output, string markerName, string markerText,
                     string loaderDir, string bootDll, string bootType, string bootMethod,
                     string bootArg, string[] refDirs)
{
    using var assembly = Read(input, refDirs);
    var module = assembly.MainModule;
    var before = (Types: CountTypes(module), Methods: CountMethods(module), Refs: module.AssemblyReferences.Count);

    var moduleType = module.Types.FirstOrDefault(t => t.Name == "<Module>")
                     ?? throw new InvalidOperationException("找不到 <Module> 类型");
    var corlib = module.AssemblyReferences.FirstOrDefault(r => r.Name == "mscorlib")
                 ?? throw new InvalidOperationException("找不到 mscorlib 引用");
    var unity = module.AssemblyReferences.FirstOrDefault(r => r.Name == "UnityEngine.CoreModule")
                ?? throw new InvalidOperationException("找不到 UnityEngine.CoreModule 引用");

    var str = module.TypeSystem.String;
    var obj = module.TypeSystem.Object;
    var bytes = new ArrayType(module.TypeSystem.Byte);
    var objArray = new ArrayType(obj);
    var passRoot = !string.Equals(bootArg, "none", StringComparison.OrdinalIgnoreCase);

    var stringType = new TypeReference("System", "String", module, corlib);
    var concat2 = new MethodReference("Concat", str, stringType) { HasThis = false };
    concat2.Parameters.Add(new ParameterDefinition(str));
    concat2.Parameters.Add(new ParameterDefinition(str));

    var fileType = new TypeReference("System.IO", "File", module, corlib);
    var writeAllText = new MethodReference("WriteAllText", module.TypeSystem.Void, fileType) { HasThis = false };
    writeAllText.Parameters.Add(new ParameterDefinition(str));
    writeAllText.Parameters.Add(new ParameterDefinition(str));
    var readAllBytes = new MethodReference("ReadAllBytes", bytes, fileType) { HasThis = false };
    readAllBytes.Parameters.Add(new ParameterDefinition(str));

    var appType = new TypeReference("UnityEngine", "Application", module, unity);
    var getPp = new MethodReference("get_persistentDataPath", str, appType) { HasThis = false };

    var assemblyType = new TypeReference("System.Reflection", "Assembly", module, corlib);
    var assemblyLoad = new MethodReference("Load", assemblyType, assemblyType) { HasThis = false };
    assemblyLoad.Parameters.Add(new ParameterDefinition(bytes));
    var typeType = new TypeReference("System", "Type", module, corlib);
    var getType = new MethodReference("GetType", typeType, assemblyType) { HasThis = true };
    getType.Parameters.Add(new ParameterDefinition(str));
    var methodInfoType = new TypeReference("System.Reflection", "MethodInfo", module, corlib);
    var getMethod = new MethodReference("GetMethod", methodInfoType, typeType) { HasThis = true };
    getMethod.Parameters.Add(new ParameterDefinition(str));
    var methodBaseType = new TypeReference("System.Reflection", "MethodBase", module, corlib);
    var invoke = new MethodReference("Invoke", obj, methodBaseType) { HasThis = true };
    invoke.Parameters.Add(new ParameterDefinition(obj));
    invoke.Parameters.Add(new ParameterDefinition(objArray));

    var exceptionType = new TypeReference("System", "Exception", module, corlib);

    var cctor = moduleType.Methods.FirstOrDefault(m => m.Name == ".cctor" && m.IsStatic);
    var createdCctor = cctor == null;
    if (cctor == null)
    {
        cctor = new MethodDefinition(
            ".cctor",
            MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig |
            MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            module.TypeSystem.Void);
        cctor.Body = new MethodBody(cctor);
        moduleType.Methods.Add(cctor);
    }
    else if (!cctor.HasBody)
    {
        throw new InvalidOperationException("<Module>.cctor 没有方法体，无法前插");
    }

    var body = cctor.Body;
    body.InitLocals = true;
    var il = body.GetILProcessor();
    var ppLocal = new VariableDefinition(str); body.Variables.Add(ppLocal);
    var rootLocal = new VariableDefinition(str); body.Variables.Add(rootLocal);
    var dllLocal = new VariableDefinition(str); body.Variables.Add(dllLocal);

    var tail = createdCctor ? il.Create(OpCodes.Ret) : body.Instructions[0];

    var after1 = il.Create(OpCodes.Nop);
    var try1 = il.Create(OpCodes.Call, getPp);
    var handler1 = il.Create(OpCodes.Pop);

    var try2 = il.Create(OpCodes.Call, getPp);
    var handler2 = il.Create(OpCodes.Pop);

    var block = new List<Instruction>();

    // 标记写入：path = pp + "/" + name; File.WriteAllText(path, text)
    void Mark(string name, string text)
    {
        block.Add(Instruction.Create(OpCodes.Ldloc, ppLocal));
        block.Add(Instruction.Create(OpCodes.Ldstr, "/" + name));
        block.Add(Instruction.Create(OpCodes.Call, concat2));
        block.Add(Instruction.Create(OpCodes.Ldstr, text));
        block.Add(Instruction.Create(OpCodes.Call, writeAllText));
    }

    void ConcatInto(VariableDefinition target, VariableDefinition left, string right)
    {
        block.Add(Instruction.Create(OpCodes.Ldloc, left));
        block.Add(Instruction.Create(OpCodes.Ldstr, right));
        block.Add(Instruction.Create(OpCodes.Call, concat2));
        block.Add(Instruction.Create(OpCodes.Stloc, target));
    }

    // ---- 第 1 段：pp 与标记（证明 cctor 跑了 + persistentDataPath 可用） ----
    block.Add(try1);
    block.Add(Instruction.Create(OpCodes.Stloc, ppLocal));
    Mark(markerName, markerText);
    block.Add(Instruction.Create(OpCodes.Leave, after1));
    block.Add(handler1);
    block.Add(Instruction.Create(OpCodes.Leave, after1));
    block.Add(after1);

    // ---- 第 2 段：加载引导，逐步留标记 ----
    block.Add(try2);
    block.Add(Instruction.Create(OpCodes.Stloc, ppLocal));
    Mark("cesium_ios_s1_enter.txt", "region2 entered");
    ConcatInto(rootLocal, ppLocal, "/" + loaderDir);
    ConcatInto(dllLocal, rootLocal, "/" + bootDll);
    Mark("cesium_ios_s2_paths.txt", "paths computed");
    block.Add(Instruction.Create(OpCodes.Ldloc, dllLocal));
    block.Add(Instruction.Create(OpCodes.Call, readAllBytes));
    Mark("cesium_ios_s3_read.txt", "bytes read");
    block.Add(Instruction.Create(OpCodes.Call, assemblyLoad));
    Mark("cesium_ios_s4_load.txt", "assembly loaded");
    block.Add(Instruction.Create(OpCodes.Ldstr, bootType));
    block.Add(Instruction.Create(OpCodes.Callvirt, getType));
    Mark("cesium_ios_s5_type.txt", "type resolved");
    block.Add(Instruction.Create(OpCodes.Ldstr, bootMethod));
    block.Add(Instruction.Create(OpCodes.Callvirt, getMethod));
    Mark("cesium_ios_s6_method.txt", "method resolved");
    block.Add(Instruction.Create(OpCodes.Ldnull));
    if (passRoot)
    {
        block.Add(Instruction.Create(OpCodes.Ldc_I4_1));
        block.Add(Instruction.Create(OpCodes.Newarr, obj));
        block.Add(Instruction.Create(OpCodes.Dup));
        block.Add(Instruction.Create(OpCodes.Ldc_I4_0));
        block.Add(Instruction.Create(OpCodes.Ldloc, rootLocal));
        block.Add(Instruction.Create(OpCodes.Stelem_Ref));
    }
    else
    {
        block.Add(Instruction.Create(OpCodes.Ldnull));
    }
    block.Add(Instruction.Create(OpCodes.Callvirt, invoke));
    block.Add(Instruction.Create(OpCodes.Pop));
    Mark("cesium_ios_s7_invoked.txt", "bootstrap invoked");
    block.Add(Instruction.Create(OpCodes.Leave, tail));

    block.Add(handler2);
    // handler2 的首条指令就是 Pop（丢掉异常对象）；ppLocal 由第 1 段或第 2 段开头赋值过。
    Mark("cesium_ios_s9_caught.txt", "region2 threw");
    block.Add(Instruction.Create(OpCodes.Leave, tail));

    if (createdCctor)
    {
        foreach (var ins in block) il.Append(ins);
        il.Append(tail);
    }
    else
    {
        foreach (var ins in block) il.InsertBefore(tail, ins);
    }

    body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Catch)
    {
        TryStart = try1, TryEnd = handler1, HandlerStart = handler1, HandlerEnd = after1,
        CatchType = exceptionType,
    });
    body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Catch)
    {
        TryStart = try2, TryEnd = handler2, HandlerStart = handler2, HandlerEnd = tail,
        CatchType = exceptionType,
    });

    assembly.Write(output, new WriterParameters { DeterministicMvid = false });

    using var check = Read(output, refDirs);
    var checkModule = check.MainModule.Types.First(t => t.Name == "<Module>");
    var checkCctor = checkModule.Methods.FirstOrDefault(m => m.Name == ".cctor" && m.IsStatic);
    var refs = checkCctor?.Body?.Instructions
        .Select(i => (i.Operand as MethodReference)?.Name)
        .Where(n => n != null).ToList() ?? new List<string?>();
    var need = new[] { "get_persistentDataPath", "Concat", "WriteAllText", "ReadAllBytes", "Load", "GetType", "GetMethod", "Invoke" };
    var missing = need.Where(n => !refs.Contains(n)).ToList();
    var handlers = checkCctor?.Body?.ExceptionHandlers.Count ?? 0;
    var extraMethods = checkModule.Methods.Count(m => m.Name != ".cctor");
    var literals = checkCctor?.Body?.Instructions.Where(i => i.OpCode == OpCodes.Ldstr)
        .Select(i => (string?)i.Operand).ToList() ?? new List<string?>();
    var hasType = literals.Contains(bootType);
    var markerCount = literals.Count(s => s != null && s.StartsWith("/cesium_ios_s", StringComparison.Ordinal));

    var after = (Types: CountTypes(check.MainModule), Methods: CountMethods(check.MainModule), Refs: check.MainModule.AssemblyReferences.Count);

    Console.WriteLine($"注入 <Module>.cctor -> 标记文件 + 加载 {bootType}::{bootMethod}({(passRoot ? "root" : "无参")})");
    Console.WriteLine($"引导路径      : {{Application.persistentDataPath}}/{loaderDir}/{bootDll}");
    Console.WriteLine($"cctor         : {(createdCctor ? "新建（该程序集原本没有）" : "已存在，注入段前插，原有指令保留")}");
    Console.WriteLine($"输入          : {Path.GetFileName(input)}  {new FileInfo(input).Length} B  {Sha256(input)}");
    Console.WriteLine($"输出          : {Path.GetFileName(output)}  {new FileInfo(output).Length} B  {Sha256(output)}");
    Console.WriteLine($"结构          : 类型 {before.Types} -> {after.Types}，方法 {before.Methods} -> {after.Methods}，引用 {before.Refs} -> {after.Refs}");
    Console.WriteLine(missing.Count == 0 ? "校验 : 加载链调用齐全" : $"校验 : 缺少调用 {string.Join(", ", missing)}（失败）");
    Console.WriteLine($"校验 : 异常护栏 {handlers} 段（应为 2）；引导类型字面量 {hasType}；分段标记 {markerCount} 个；<Module> 额外方法 {extraMethods} 个");
    return missing.Count == 0 && handlers == 2 && hasType && markerCount == 8 && extraMethods == 0 ? 0 : 1;
}
