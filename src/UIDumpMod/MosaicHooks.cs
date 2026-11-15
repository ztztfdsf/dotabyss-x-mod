using System;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Il2CppProject.Novel;

namespace UIDumpMod
{
    /// <summary>
    /// 动态 CG（Live2D 剧情）程序马赛克消除层。
    ///
    /// 游戏机制（Il2CppProject.Novel.NovelLive2DController，interop 元数据实证）：
    ///   CacheMosaics()  扫 Live2D 模型里名字含 Mosaic 的 drawable，缓存其网格
    ///   _mosaicCamera   专用相机把模型拍到低分辨率 render target 上（MosaicPixelDivX/Y 降采样）
    ///   _mosaicMeshes   用降采样结果当贴图的网格叠加层 —— 这就是屏幕上那层码
    ///   每帧 Update/LateUpdate 会刷新，OnBeginCameraRendering 回调驱动相机渲染
    ///
    /// AbyssMod 的 DisableMosaic 只 SetActive(false) 了名字 Mosaic 开头的 drawable，
    /// 罩不住这套相机-网格管线（其贴图是相机实拍的，跟 drawable 无关）。
    ///
    /// 修法（两层）：
    ///   1) CacheMosaics prefix 返回 false —— 缓存列表永远是空的，网格无从生成
    ///   2) Setup postfix 兜底 —— 万一别处已初始化，把 _mosaicCamera 禁用 +
    ///      _isMosaicCacheInit 置 true（防后续再补缓存）
    /// </summary>
    public static class MosaicHooks
    {
        internal static bool Enabled = true; // 待挂 cfg 开关（当前跟随 AbyssMod DynamicMosaic=false 语义：默认全关）

        public static void Apply(HarmonyLib.Harmony harmony, MelonLogger.Instance log)
        {
            int ok = 0, fail = 0;
            try
            {
                // ① 主修：CacheMosaics 是私有实例方法，跳过它
                var t = typeof(NovelLive2DController);
                var cm = AccessTools.Method(t, "CacheMosaics");
                if (cm != null)
                {
                    harmony.Patch(cm, prefix: new HarmonyMethod(typeof(MosaicHooks), nameof(SkipCache)));
                    ok++;
                }
                else { fail++; log.Warning("[Mosaic] CacheMosaics not found"); }

                // ② 兜底：Setup 完成后掐相机
                var setup = AccessTools.Method(t, "Setup");
                if (setup != null)
                {
                    harmony.Patch(setup, postfix: new HarmonyMethod(typeof(MosaicHooks), nameof(KillMosaicCamera)));
                    ok++;
                }
                else { fail++; log.Warning("[Mosaic] Setup not found"); }
            }
            catch (Exception e)
            {
                fail++;
                log.Warning("[Mosaic] hook failed: " + e.Message);
            }
            log.Msg($"[Mosaic] dynamic-CG mosaic hooks: {ok} ok, {fail} failed");
        }

        /// <summary>CacheMosaics prefix：直接跳过（返回 false 不执行原方法）。</summary>
        private static void SkipCache() { }

        /// <summary>Setup postfix：禁用马赛克相机 + 标记已初始化（防补缓存）。</summary>
        private static void KillMosaicCamera(NovelLive2DController __instance)
        {
            if (!Enabled) return;
            try
            {
                var cam = __instance._mosaicCamera;
                if (cam != null && cam.gameObject != null)
                    cam.gameObject.SetActive(false);
                __instance._isMosaicCacheInit = true; // 骗过「未初始化」检查，别再触发缓存
                MelonLogger.Msg("[Mosaic] mosaic camera disabled on " + __instance.gameObject.name);
            }
            catch (Exception e) { MelonLogger.Warning("[Mosaic] kill camera err: " + e.Message); }
        }
    }
}
