// UIDumpMod —— AbyssMod 的运行时 UI 兜底翻译层
//
// 一句话：AbyssMod 翻完界面文本后，如果还剩日文，我们按「路径后缀 + 去 (Clone)」规则
// 用同一份 ui_texts 表再翻一次；仍翻不出的打 [LEAK] 日志，供 tools/leak2table.py 滚动补译。
//
// 文件分工（Translation/ 下全是纯 C#，可在 PC 上用 tests/UIDumpMod.SelfTest 自测）：
//   Core.cs                         入口：加载翻译表、挂 Harmony、定时全场景扫描
//   TextHooks.cs                    Harmony 补丁 + 处理管道（取路径 → 翻译 → 回写 → 日志）
//   Translation/UiTextTable.cs      ui_texts 表与路径匹配规则（exact / suffix / {n} 模板）
//   Translation/FragmentTranslator  color 标签拼接文本的碎片级翻译（深渊事件选项等）
//   Translation/JapaneseText.cs     「是否还含日文」判定
//   Translation/TemplatePattern.cs  {n} 占位符模板
//
// 名字由来：最初是一个「dump 漏网日文」的调试工具，后演进成翻译层，名字沿用以免改动部署路径。

using System;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using Il2CppTMPro;
using UIDumpMod.Translation;

[assembly: MelonInfo(typeof(UIDumpMod.Core), "UIDumpMod", "1.11.0", "ztztfdsf")]
[assembly: MelonGame("EXNOA LLC.", "ドットアビスX for Android")]

namespace UIDumpMod
{
    public class Core : MelonMod
    {
        /// <summary>全场景兜底扫描间隔（帧）。120 帧 ≈ 2 秒。</summary>
        private const int SweepIntervalFrames = 120;

        /// <summary>与 AbyssMod 共用的翻译表；加载失败时为 null（mod 仍会报 [LEAK]）。</summary>
        internal static UiTextTable Table;

        private int _frame;

        public override void OnInitializeMelon()
        {
            Table = LoadTable();
            TextHooks.Apply(new HarmonyLib.Harmony("com.ztztfdsf.uidump"), LoggerInstance);
            if (Table != null)
                LoggerInstance.Msg($"UIDumpMod table: exact={Table.ExactPathCount} suffix={Table.SuffixPathCount} patterns={Table.PatternPathCount} global={Table.GlobalCount}");
        }

        /// <summary>
        /// 有些文本不经过 set_text/OnEnable（如动画换字、预制体初始值），hook 抓不到；
        /// 每 2 秒把场景里所有 TMP 组件过一遍兜底。
        /// </summary>
        public override void OnLateUpdate()
        {
            if (++_frame % SweepIntervalFrames != 0) return;
            try
            {
                foreach (var tmp in UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>())
                    if (tmp != null) TextHooks.Handle(tmp, "Sweep");
            }
            catch { }
        }

        private UiTextTable LoadTable()
        {
            var path = Path.Combine(MelonEnvironment.UserDataDirectory,
                "AbyssMod", "translations", "ui_texts", "zh_Hans.json");
            try
            {
                if (File.Exists(path)) return UiTextTable.FromJson(File.ReadAllText(path));
                LoggerInstance.Warning("ui_texts json not found: " + path);
            }
            catch (Exception e)
            {
                LoggerInstance.Warning("LoadTranslations failed: " + e.Message);
            }
            return null;
        }
    }
}
