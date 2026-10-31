using System;
using System.IO;
using System.Linq;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

// abysspatch：修 1.15.0 进 H 剧情 CG 黑屏（主人报障 2026-10-31，对照实验定罪 AbyssMod）。
//
// 定罪证据链：
//   ① spine 资产在设备缓存且与 CDN 逐字节一致（排除下载/损坏）
//   ② 移除 AbyssMod 只留 UIDumpMod → 同一话 CG 正常显示（主人实弹验证「继续 正常了」）
// 根因：AbyssMod.Patches.EnhancePatch 的三个渲染 hook 是按 1.14 interop 编译的旧程序集，
//   在 1.15.0 重生成的 interop 方法表（部分槽位错位）下：
//   - DisableMosaic: 遍历 GetDrawables() 读 name → 错位数据 → SetActive(false) 错杀 spine 单位 → CG 黑屏
//   - Begin/EndNovelLive2DScale: 写 canvasRoot.localScale（同族毒槽位）
// 手术：三个方法体全部 no-op（保留签名，Harmony patch 挂空方法零危害）。
//   翻译功能完全不经过这三个 hook，零损失。
class Program
{
    static int Main(string[] args)
    {
        string src = args.Length > 0 ? args[0] : @"G:\tmp\dotabyss-mod\official-dl.bin.out\assets\LemonLoader\deployment\Mods\AbyssMod\AbyssMod.dll";
        string dst = args.Length > 1 ? args[1] : @"G:\tmp\dotabyss-mod\dl-1150.out\assets\LemonLoader\deployment\Mods\AbyssMod\AbyssMod.dll";

        var module = ModuleDefinition.FromFile(src);
        var enhance = module.GetAllTypes().FirstOrDefault(t => t.FullName == "AbyssMod.Patches.EnhancePatch");
        if (enhance == null) { Console.WriteLine("ERROR: EnhancePatch not found"); return 1; }

        string[] kill = { "DisableMosaic", "BeginNovelLive2DScale", "EndNovelLive2DScale" };
        foreach (var name in kill)
        {
            foreach (var m in enhance.Methods.Where(m => m.Name == name).ToList())
            {
                var fresh = new CilMethodBody();
                // 签名统一是 void(NovelLive2DObject/Controller __instance) → 直接 ret
                fresh.Instructions.Add(new CilInstruction(CilOpCodes.Ret));
                m.CilMethodBody = fresh;
                Console.WriteLine($"no-op {m.FullName}");
            }
        }
        // Harmony attribute 留着：挂空方法 = 恒等 postfix，无行为。
        module.Write(dst);
        Console.WriteLine("written: " + dst);
        return 0;
    }
}
