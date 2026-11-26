using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CesiumLoader.Mobile.Patcher;

/// <summary>注入器执行结果（成功状态与校验摘要/错误信息）。</summary>
public readonly record struct PatchResult(bool Ok, string Message);

/// <summary>
/// CesiumLoader 移动端程序集注入器（Mono.Cecil）。
/// 在 &lt;Module&gt;.cctor 首部内联单段 try/catch 引导链，不新增方法也不改变类型形状。
/// </summary>
public static class MobileAssemblyPatcher
{
    /// <summary>
    /// 探针注入：在 &lt;Module&gt;.cctor 写入 File::WriteAllText(Application.persistentDataPath + "/&lt;fileName&gt;", text)。
    /// </summary>
    public static PatchResult Probe(
        string inputPath,
        string outputPath,
        string fileName = "cesium_probe.txt",
        string text = "cesium ios probe ok",
        params string[] refDirs)
    {
        try
        {
            var (before, createdCctor, hasPp, hasConcat, hasWrite, handlers, extraMethods, after) =
                ProbeCore(inputPath, outputPath, fileName, text, refDirs);

            var log = new StringBuilder();
            log.AppendLine($"注入 <Module>.cctor -> File::WriteAllText(Application.persistentDataPath + \"/{fileName}\")");
            log.AppendLine($"cctor         : {(createdCctor ? "新建（该程序集原本没有）" : "已存在，注入段前插，原有指令保留")}");
            log.AppendLine("调用面        : get_persistentDataPath / String::Concat / File::WriteAllText（共 3 个）");
            log.AppendLine($"输入          : {Path.GetFileName(inputPath)}  {new FileInfo(inputPath).Length} B  {Sha256(inputPath)}");
            log.AppendLine($"输出          : {Path.GetFileName(outputPath)}  {new FileInfo(outputPath).Length} B  {Sha256(outputPath)}");
            log.AppendLine($"结构          : 类型 {before.Types} -> {after.Types}，方法 {before.Methods} -> {after.Methods}，引用 {before.Refs} -> {after.Refs}");
            log.AppendLine($"校验 : 三个调用 {hasPp}/{hasConcat}/{hasWrite}，异常护栏 {handlers} 段");
            log.Append($"校验 : <Module> 上除 cctor 外的方法 {extraMethods} 个（应为 0）");

            var ok = hasPp && hasWrite && hasConcat && handlers == 1 && extraMethods == 0;
            return new PatchResult(ok, log.ToString());
        }
        catch (Exception ex)
        {
            return new PatchResult(false, $"失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 引导加载注入，单段 try/catch 内联在 &lt;Module&gt;.cctor 里（不写入诊断标记文件）。
    /// 加载链与安卓 inject-bootstrap 同形：
    /// Assembly.Load(File.ReadAllBytes(dll)) -&gt; GetType(type) -&gt; GetMethod(name) -&gt; Invoke。
    /// </summary>
    public static PatchResult Bootstrap(
        string inputPath,
        string outputPath,
        string loaderDir = "CesiumLoader",
        string bootDll = "CesiumLoader.Mobile.Bootstrap.dll",
        string bootType = "CesiumLoader.Mobile.Bootstrap.Boot",
        string bootMethod = "LoadForGame",
        string bootArg = "root",
        params string[] refDirs)
    {
        try
        {
            var passRoot = !string.Equals(bootArg, "none", StringComparison.OrdinalIgnoreCase);

            using (var resolver = CreateResolver(inputPath, refDirs))
            using (var assembly = Read(inputPath, resolver))
            {
                var module = assembly.MainModule;
                var before = GetCounts(module);
                var (moduleType, corlib, unity) = GetTargetModuleParts(module);
                var str = module.TypeSystem.String;
                var obj = module.TypeSystem.Object;
                var bytes = new ArrayType(module.TypeSystem.Byte);
                var objArray = new ArrayType(obj);

                var concat2 = CreateStaticMethod(module, corlib, "System", "String", "Concat", str, str, str);
                var readAllBytes = CreateStaticMethod(module, corlib, "System.IO", "File", "ReadAllBytes", bytes, str);
                var getPp = CreateStaticMethod(module, unity, "UnityEngine", "Application", "get_persistentDataPath", str);

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

                var (cctor, createdCctor) = GetOrCreateCctor(moduleType, module);
                var body = cctor.Body;
                body.InitLocals = true;
                var il = body.GetILProcessor();
                var ppLocal = new VariableDefinition(str);
                var rootLocal = new VariableDefinition(str);
                var dllLocal = new VariableDefinition(str);
                body.Variables.Add(ppLocal);
                body.Variables.Add(rootLocal);
                body.Variables.Add(dllLocal);

                var tail = createdCctor ? il.Create(OpCodes.Ret) : body.Instructions[0];
                var tryStart = il.Create(OpCodes.Call, getPp);
                var handlerStart = il.Create(OpCodes.Pop);

                var block = new List<Instruction>();

                void ConcatInto(VariableDefinition target, VariableDefinition left, string right)
                {
                    block.Add(Instruction.Create(OpCodes.Ldloc, left));
                    block.Add(Instruction.Create(OpCodes.Ldstr, right));
                    block.Add(Instruction.Create(OpCodes.Call, concat2));
                    block.Add(Instruction.Create(OpCodes.Stloc, target));
                }

                block.Add(tryStart);
                block.Add(Instruction.Create(OpCodes.Stloc, ppLocal));
                ConcatInto(rootLocal, ppLocal, "/" + loaderDir);
                ConcatInto(dllLocal, rootLocal, "/" + bootDll);
                block.Add(Instruction.Create(OpCodes.Ldloc, dllLocal));
                block.Add(Instruction.Create(OpCodes.Call, readAllBytes));
                block.Add(Instruction.Create(OpCodes.Call, assemblyLoad));
                block.Add(Instruction.Create(OpCodes.Ldstr, bootType));
                block.Add(Instruction.Create(OpCodes.Callvirt, getType));
                block.Add(Instruction.Create(OpCodes.Ldstr, bootMethod));
                block.Add(Instruction.Create(OpCodes.Callvirt, getMethod));
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
                block.Add(Instruction.Create(OpCodes.Leave, tail));
                block.Add(handlerStart);
                block.Add(Instruction.Create(OpCodes.Leave, tail));

                InsertInstructionsAndCatch(body, il, createdCctor, block, tail, tryStart, handlerStart, exceptionType);
                assembly.Write(outputPath, new WriterParameters { DeterministicMvid = false });

                using var checkResolver = CreateResolver(outputPath, refDirs);
                using var check = Read(outputPath, checkResolver);
                var checkModule = check.MainModule.Types.First(t => t.Name == "<Module>");
                var checkCctor = checkModule.Methods.FirstOrDefault(m => m.Name == ".cctor" && m.IsStatic);
                var refs = checkCctor?.Body?.Instructions
                    .Select(i => (i.Operand as MethodReference)?.Name)
                    .Where(n => n != null).ToList() ?? [];
                var need = new[] { "get_persistentDataPath", "Concat", "ReadAllBytes", "Load", "GetType", "GetMethod", "Invoke" };
                var missing = need.Where(n => !refs.Contains(n)).ToList();
                var handlers = checkCctor?.Body?.ExceptionHandlers.Count ?? 0;
                var extraMethods = checkModule.Methods.Count(m => m.Name != ".cctor");
                var literals = checkCctor?.Body?.Instructions.Where(i => i.OpCode == OpCodes.Ldstr)
                    .Select(i => (string?)i.Operand).ToList() ?? [];
                var hasType = literals.Contains(bootType);
                var after = GetCounts(check.MainModule);

                var log = new StringBuilder();
                log.AppendLine($"注入 <Module>.cctor -> 加载 {bootType}::{bootMethod}({(passRoot ? "root" : "无参")})");
                log.AppendLine($"引导路径      : {{Application.persistentDataPath}}/{loaderDir}/{bootDll}");
                log.AppendLine($"cctor         : {(createdCctor ? "新建（该程序集原本没有）" : "已存在，注入段前插，原有指令保留")}");
                log.AppendLine($"输入          : {Path.GetFileName(inputPath)}  {new FileInfo(inputPath).Length} B  {Sha256(inputPath)}");
                log.AppendLine($"输出          : {Path.GetFileName(outputPath)}  {new FileInfo(outputPath).Length} B  {Sha256(outputPath)}");
                log.AppendLine($"结构          : 类型 {before.Types} -> {after.Types}，方法 {before.Methods} -> {after.Methods}，引用 {before.Refs} -> {after.Refs}");
                log.AppendLine(missing.Count == 0 ? "校验 : 加载链调用齐全" : $"校验 : 缺少调用 {string.Join(", ", missing)}（失败）");
                log.Append($"校验 : 异常护栏 {handlers} 段（应为 1）；引导类型字面量 {hasType}；<Module> 额外方法 {extraMethods} 个");

                var ok = missing.Count == 0 && handlers == 1 && hasType && extraMethods == 0;
                return new PatchResult(ok, log.ToString());
            }
        }
        catch (Exception ex)
        {
            return new PatchResult(false, $"失败: {ex.Message}");
        }
    }

    private static (
        (int Types, int Methods, int Refs) Before,
        bool CreatedCctor,
        bool HasPp,
        bool HasConcat,
        bool HasWrite,
        int Handlers,
        int ExtraMethods,
        (int Types, int Methods, int Refs) After)
        ProbeCore(string inputPath, string outputPath, string fileName, string text, string[] refDirs)
    {
        using var resolver = CreateResolver(inputPath, refDirs);
        using var assembly = Read(inputPath, resolver);
        var module = assembly.MainModule;
        var before = GetCounts(module);

        var (moduleType, corlib, unity) = GetTargetModuleParts(module);
        var str = module.TypeSystem.String;
        var concat2 = CreateStaticMethod(module, corlib, "System", "String", "Concat", str, str, str);
        var writeAllText = CreateStaticMethod(module, corlib, "System.IO", "File", "WriteAllText", module.TypeSystem.Void, str, str);
        var getPp = CreateStaticMethod(module, unity, "UnityEngine", "Application", "get_persistentDataPath", str);
        var exceptionType = new TypeReference("System", "Exception", module, corlib);

        var (cctor, createdCctor) = GetOrCreateCctor(moduleType, module);
        var body = cctor.Body;
        var il = body.GetILProcessor();
        var end = createdCctor ? il.Create(OpCodes.Ret) : body.Instructions[0];
        var tryStart = il.Create(OpCodes.Call, getPp);
        var handlerStart = il.Create(OpCodes.Pop);

        var block = new List<Instruction>
        {
            tryStart,
            il.Create(OpCodes.Ldstr, "/" + fileName),
            il.Create(OpCodes.Call, concat2),
            il.Create(OpCodes.Ldstr, text),
            il.Create(OpCodes.Call, writeAllText),
            il.Create(OpCodes.Leave, end),
            handlerStart,
            il.Create(OpCodes.Leave, end),
        };

        InsertInstructionsAndCatch(body, il, createdCctor, block, end, tryStart, handlerStart, exceptionType);
        assembly.Write(outputPath, new WriterParameters { DeterministicMvid = false });

        using var checkResolver = CreateResolver(outputPath, refDirs);
        using var check = Read(outputPath, checkResolver);
        var checkModule = check.MainModule.Types.First(t => t.Name == "<Module>");
        var checkCctor = checkModule.Methods.FirstOrDefault(m => m.Name == ".cctor" && m.IsStatic);
        var refs = checkCctor?.Body?.Instructions
            .Select(i => (i.Operand as MethodReference)?.Name)
            .Where(n => n != null).ToList() ?? [];
        var hasPp = refs.Contains("get_persistentDataPath");
        var hasWrite = refs.Contains("WriteAllText");
        var hasConcat = refs.Contains("Concat");
        var extraMethods = checkModule.Methods.Count(m => m.Name != ".cctor");
        var handlers = checkCctor?.Body?.ExceptionHandlers.Count ?? 0;
        var after = GetCounts(check.MainModule);

        return (before, createdCctor, hasPp, hasConcat, hasWrite, handlers, extraMethods, after);
    }

    private static DefaultAssemblyResolver CreateResolver(string path, string[] refDirs)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        foreach (var d in refDirs)
            resolver.AddSearchDirectory(d);
        return resolver;
    }

    private static AssemblyDefinition Read(string path, IAssemblyResolver resolver) =>
        AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver });

    private static (TypeDefinition ModuleType, AssemblyNameReference Corlib, AssemblyNameReference Unity)
        GetTargetModuleParts(ModuleDefinition module)
    {
        var moduleType = module.Types.FirstOrDefault(t => t.Name == "<Module>")
                         ?? throw new InvalidOperationException("找不到 <Module> 类型");
        var corlib = module.AssemblyReferences.FirstOrDefault(r => r.Name == "mscorlib")
                     ?? throw new InvalidOperationException("找不到 mscorlib 引用");
        var unity = module.AssemblyReferences.FirstOrDefault(r => r.Name == "UnityEngine.CoreModule")
                    ?? throw new InvalidOperationException("找不到 UnityEngine.CoreModule 引用");
        return (moduleType, corlib, unity);
    }

    private static MethodReference CreateStaticMethod(
        ModuleDefinition module,
        IMetadataScope scope,
        string ns,
        string typeName,
        string methodName,
        TypeReference returnType,
        params TypeReference[] paramTypes)
    {
        var type = new TypeReference(ns, typeName, module, scope);
        var method = new MethodReference(methodName, returnType, type) { HasThis = false };
        foreach (var paramType in paramTypes)
            method.Parameters.Add(new ParameterDefinition(paramType));
        return method;
    }

    private static (MethodDefinition Cctor, bool Created) GetOrCreateCctor(TypeDefinition moduleType, ModuleDefinition module)
    {
        var cctor = moduleType.Methods.FirstOrDefault(m => m.Name == ".cctor" && m.IsStatic);
        if (cctor is not null)
        {
            if (!cctor.HasBody)
                throw new InvalidOperationException("<Module>.cctor 没有方法体，无法前插");
            return (cctor, false);
        }

        cctor = new MethodDefinition(
            ".cctor",
            MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig |
            MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            module.TypeSystem.Void);
        cctor.Body = new MethodBody(cctor);
        moduleType.Methods.Add(cctor);
        return (cctor, true);
    }

    private static void InsertInstructionsAndCatch(
        MethodBody body,
        ILProcessor il,
        bool createdCctor,
        List<Instruction> block,
        Instruction tail,
        Instruction tryStart,
        Instruction handlerStart,
        TypeReference exceptionType)
    {
        if (createdCctor)
        {
            foreach (var ins in block)
                il.Append(ins);
            il.Append(tail);
        }
        else
        {
            var anchor = body.Instructions[0];
            foreach (var ins in block)
                il.InsertBefore(anchor, ins);
        }

        body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Catch)
        {
            TryStart = tryStart,
            TryEnd = handlerStart,
            HandlerStart = handlerStart,
            HandlerEnd = tail,
            CatchType = exceptionType,
        });
    }

    private static string Sha256(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
    }

    private static (int Types, int Methods, int Refs) GetCounts(ModuleDefinition module) =>
        (AllTypes(module).Count(), AllTypes(module).Sum(t => t.Methods.Count), module.AssemblyReferences.Count);

    private static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition module)
    {
        foreach (var t in module.Types)
        {
            yield return t;
            foreach (var n in Nested(t))
                yield return n;
        }
    }

    private static IEnumerable<TypeDefinition> Nested(TypeDefinition type)
    {
        foreach (var n in type.NestedTypes)
        {
            yield return n;
            foreach (var x in Nested(n))
                yield return x;
        }
    }
}
