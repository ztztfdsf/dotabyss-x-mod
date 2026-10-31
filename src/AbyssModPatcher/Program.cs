// AbyssModPatcher —— 对原作者二进制（AbyssMod.dll / Utility.dll）做 IL 手术，修复它们在新游戏版本上的崩溃点。
//
// 背景：AbyssMod（原作者 Jitsu v1.1.4，已停更、无源码）按 1.14 时代的 interop 编译。1.15.0 起游戏
// metadata 变化，interop 代理必须重新生成，而旧程序集调用新代理时部分方法槽位错位——某些代码路径
// 一执行就 coreclr abort 或行为错乱。我们不改它的翻译逻辑，只把出事的代码路径「挖空」：
//
//   utility   Utility.dll：Toast 弹窗渲染链（new ToastTheme 即 abort）
//             ・保留 Toast 门面方法签名（AbyssMod 会调用 Toast.Info/Success/...），方法体改为空
//             ・删除 Utility.Notifications 下除 Toast/ToastKind 外的全部类型（渲染器/主题/运行时）
//             ・删除内嵌的 Toast 原生资源（arm64 .so / classes.dex）
//
//   abyssmod  AbyssMod.dll：H 剧情 CG 黑屏
//             ・EnhancePatch.DisableMosaic 读错 drawable 名 → SetActive(false) 错杀 spine 模型
//             ・EnhancePatch.Begin/EndNovelLive2DScale 写 localScale（同类槽位，默认缩放 1.0 本就恒等）
//             三个方法体改为直接 return；Harmony 仍会挂上，但什么都不做。翻译功能不经过这些方法。
//
// 用法（输入永远是原作者的原版 dll，不要拿已手术过的再手术）：
//   dotnet run -c Release -- utility  <原版 Utility.dll>  <输出 Utility.dll>
//   dotnet run -c Release -- abyssmod <原版 AbyssMod.dll> <输出 AbyssMod.dll>
//   dotnet run -c Release -- all      <原版 Mods/AbyssMod 目录> <输出 Mods/AbyssMod 目录>

using System;
using System.IO;
using System.Linq;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

static class Program
{
    static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.WriteLine("usage: AbyssModPatcher <utility|abyssmod|all> <input> <output>");
            return 2;
        }
        var (mode, input, output) = (args[0], args[1], args[2]);
        switch (mode)
        {
            case "utility":  return PatchUtility(input, output);
            case "abyssmod": return PatchAbyssMod(input, output);
            case "all":
                Directory.CreateDirectory(output);
                return PatchUtility(Path.Combine(input, "Utility.dll"), Path.Combine(output, "Utility.dll"))
                     | PatchAbyssMod(Path.Combine(input, "AbyssMod.dll"), Path.Combine(output, "AbyssMod.dll"));
            default:
                Console.WriteLine("unknown mode: " + mode);
                return 2;
        }
    }

    // ---------------- Utility.dll：拆除 Toast 渲染链 ----------------

    const string ToastType = "Utility.Notifications.Toast";
    const string ToastKindType = "Utility.Notifications.ToastKind";

    static int PatchUtility(string input, string output)
    {
        var module = ModuleDefinition.FromFile(input);
        var toast = module.GetAllTypes().FirstOrDefault(t => t.FullName == ToastType);
        if (toast == null) return Fail("Toast type not found in " + input);

        // ① 门面瘦身：这几个成员引用了要删除的类型，留着会让 dll 写不出来
        foreach (var name in new[] { "IsInitialized", "LastRenderError", "Renderer", "Count", "Configure" })
        {
            foreach (var p in toast.Properties.Where(p => p.Name == name).ToList()) toast.Properties.Remove(p);
            foreach (var m in toast.Methods.Where(m => m.Name == name || m.Name == "get_" + name || m.Name == "set_" + name).ToList())
                toast.Methods.Remove(m);
        }

        // ② 剩下的门面方法全部换成空方法体（bool 返回 true，其余 void）
        //    必须新建方法体而不是清空旧的：旧方法体里残留的元数据引用会卡住 PE 写出
        foreach (var m in toast.Methods.Where(m => m.Name != ".ctor" && m.Name != ".cctor"))
        {
            bool returnsBool = m.Signature?.ReturnType?.FullName == "System.Boolean";
            m.CilMethodBody = EmptyBody(returnsBool);
        }

        // ③ 删除渲染链全部类型（前缀匹配，覆盖 Utility.Notifications.Internal）
        int removedTypes = 0;
        foreach (var t in module.GetAllTypes().ToList())
        {
            var ns = t.Namespace?.ToString() ?? "";
            if (!ns.StartsWith("Utility.Notifications", StringComparison.Ordinal)) continue;
            if (t.FullName == ToastType || t.FullName == ToastKindType) continue;
            if (t.DeclaringType == null) { module.TopLevelTypes.Remove(t); removedTypes++; continue; }

            // 嵌套类型：外层是被删的类型 → 随外层一起消失，不用单独删；外层是保留的 Toast → 单独删
            var root = t.DeclaringType;
            while (root.DeclaringType != null) root = root.DeclaringType;
            if (root.FullName == ToastType || root.FullName == ToastKindType)
            {
                t.DeclaringType.NestedTypes.Remove(t);
                removedTypes++;
            }
        }

        // ④ 删除 Toast 原生资源
        int removedResources = 0;
        foreach (var r in module.Resources.Where(r => r.Name.Contains("Toast")).ToList())
        {
            module.Resources.Remove(r);
            removedResources++;
        }

        module.Write(output);
        Console.WriteLine($"utility  -> {output} ({new FileInfo(output).Length} bytes; types removed {removedTypes}, resources removed {removedResources})");
        return 0;
    }

    // ---------------- AbyssMod.dll：挖空渲染 hook ----------------

    static readonly string[] DeadHooks = { "DisableMosaic", "BeginNovelLive2DScale", "EndNovelLive2DScale" };

    static int PatchAbyssMod(string input, string output)
    {
        var module = ModuleDefinition.FromFile(input);
        var enhance = module.GetAllTypes().FirstOrDefault(t => t.FullName == "AbyssMod.Patches.EnhancePatch");
        if (enhance == null) return Fail("EnhancePatch not found in " + input);

        int patched = 0;
        foreach (var m in enhance.Methods.Where(m => DeadHooks.Contains(m.Name.ToString())))
        {
            m.CilMethodBody = EmptyBody(returnsBool: false); // 三个方法都是 void
            patched++;
        }
        if (patched != DeadHooks.Length) return Fail($"expected {DeadHooks.Length} hooks, found {patched}");

        module.Write(output);
        Console.WriteLine($"abyssmod -> {output} ({new FileInfo(output).Length} bytes; hooks emptied {patched})");
        return 0;
    }

    // ---------------- 工具 ----------------

    static CilMethodBody EmptyBody(bool returnsBool)
    {
        var body = new CilMethodBody();
        if (returnsBool) body.Instructions.Add(new CilInstruction(CilOpCodes.Ldc_I4_1));
        body.Instructions.Add(new CilInstruction(CilOpCodes.Ret));
        return body;
    }

    static int Fail(string msg)
    {
        Console.WriteLine("ERROR: " + msg);
        return 1;
    }
}
