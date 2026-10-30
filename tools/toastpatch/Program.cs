using System;
using System.IO;
using System.Linq;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code;
using AsmResolver.PE.DotNet.Cil;

class Program
{
    // 目标：Utility.dll（AbyssMod 作者的辅助程序集）
    // 只动一处：Toast.Initialize(string) 开头插 ret
    //   —— 跳过 ClassInjector.RegisterTypeInIl2Cpp + AddComponent<ToastBehaviour>（1.15.0 下必崩的注入路径）。
    // Toast.Show / ToastRuntime / AndroidToastRenderer 全部保持原样；
    // 原本由 ToastBehaviour.Update/OnGUI 做的每帧驱动，改由 UIDumpMod 的 OnUpdate/OnGUI 反射调用 ToastRuntime 接管。
    static int Main(string[] args)
    {
        string src = args.Length > 0 ? args[0] : @"G:\tmp\dotabyss-mod\official-dl.bin.out\assets\LemonLoader\deployment\Mods\AbyssMod\Utility.dll";
        string dst = args.Length > 1 ? args[1] : @"G:\tmp\dotabyss-mod\dl-1150.out\assets\LemonLoader\deployment\Mods\AbyssMod\Utility.dll";

        var module = ModuleDefinition.FromFile(src);
        var toast = module.GetAllTypes().FirstOrDefault(t => t.FullName == "Utility.Notifications.Toast");
        if (toast == null) { Console.WriteLine("ERROR: Toast type not found"); return 1; }

        int patched = 0;
        foreach (var m in toast.Methods)
        {
            if (!m.HasMethodBody || m.Name != "Initialize") continue;
            m.CilMethodBody.Instructions.Insert(0, new CilInstruction(CilOpCodes.Ret));
            patched++; Console.WriteLine($"patched Initialize (ret) sig={m.Signature}");
        }
        if (patched != 1) { Console.WriteLine("ERROR: expected exactly 1 Initialize"); return 1; }
        module.Write(dst);
        Console.WriteLine($"OK -> {dst}");
        return 0;
    }
}
