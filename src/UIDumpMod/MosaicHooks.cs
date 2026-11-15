using System;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Il2CppLive2D.Cubism.Rendering;

namespace UIDumpMod
{
    /// <summary>
    /// 去码 v10（五路专家大会 · grok 方案，interop 类面已核对）：
    ///
    /// 【机制定案】码 = 模型自带的 Mosaic_XX / MosaicInsted_XX drawable，
    /// 由 CubismRenderController.OnLateUpdate → OnDynamicDrawableData →
    /// CubismRenderer.OnDrawableVisiblityDidChange(true) 每帧重新点亮，
    /// 用 renderer 自己的 Abyss/Live2DMosaicV2 材质实例 grab 屏幕像素化画出。
    ///
    /// v8 失败根因：Update prefix 里关 renderer，Cubism 在 LateUpdate（绘制前）
    /// 把 enabled 写回去；且模型异步加载后不一定在 _canvasRoot 下。
    ///
    /// 【v10 打法】不扫场景、不动相机、不动材质参数：
    ///   ① prefix 拦 OnDrawableVisiblityDidChange：码层 return false（跳过原方法，
    ///      BecomeVisible 不执行，renderer 不被重新点亮）
    ///   ② prefix 拦 OnDrawableVertexPositionsDidChange：码层 return false
    ///      （网格顶点不再更新 = 就算残留一次也画不出正确位置）
    ///   ③ CubismRenderController.OnLateUpdate postfix 兜底：把 Renderers 数组里
    ///      码层的 MeshRenderer.enabled=false + GameObject.SetActive(false)
    ///   ④ 首次命中打点日志 + mosaic_vis.txt 探针（grok 的探针一并带上）
    ///
    /// 判定（不靠场景树）：GameObject 名以 "Mosaic" 开头（覆盖 MosaicInsted）
    /// 或 renderer 材质 shader 名含 "Live2DMosaic"。
    /// </summary>
    public static class MosaicHooks
    {
        internal static bool Enabled = true;

        private static bool _logNovel, _logTitle, _visLogged;
        private static int _neutralized;
        private static readonly int GrabId = Shader.PropertyToID("_grabEffectValue");

        // 判定缓存：instanceID → 是否码层（CubismRenderer 生命周期内不变）
        private static readonly System.Collections.Generic.Dictionary<int, bool> _isMosaicCache = new();

        public static void Apply(HarmonyLib.Harmony harmony, MelonLogger.Instance log)
        {
            int ok = 0, fail = 0;

            void TryPatch(string name, System.Reflection.MethodBase target, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null)
            {
                if (target == null) { fail++; log.Warning($"[Mosaic] {name}: target not found"); return; }
                try { harmony.Patch(target, prefix, postfix); ok++; }
                catch (Exception e) { fail++; log.Warning($"[Mosaic] {name}: {e.Message}"); }
            }

            // ★ 核心：可见性回写拦截
            TryPatch("CubismRenderer.OnDrawableVisiblityDidChange",
                AccessTools.Method(typeof(CubismRenderer), "OnDrawableVisiblityDidChange"),
                prefix: new HarmonyMethod(typeof(MosaicHooks), nameof(BlockMosaicVis)));

            // ★ 顶点更新拦截（双保险）
            TryPatch("CubismRenderer.OnDrawableVertexPositionsDidChange",
                AccessTools.Method(typeof(CubismRenderer), "OnDrawableVertexPositionsDidChange"),
                prefix: new HarmonyMethod(typeof(MosaicHooks), nameof(BlockMosaicVertex)));

            // ★ 兜底：控制器 LateUpdate 后把码层 renderer 全关
            TryPatch("CubismRenderController.OnLateUpdate",
                AccessTools.Method(typeof(CubismRenderController), "OnLateUpdate"),
                postfix: new HarmonyMethod(typeof(MosaicHooks), nameof(KillAfterLateUpdate)));

            log.Msg($"[Mosaic] v10 hooks: {ok} ok, {fail} failed");
        }

        // ---------- 判定 ----------

        private static bool IsMosaic(CubismRenderer r)
        {
            if (r == null) return false;
            try
            {
                int id = r.GetInstanceID();
                if (_isMosaicCache.TryGetValue(id, out var cached)) return cached;
                bool result = false;
                try
                {
                    var go = r.gameObject;
                    if (go != null && go.name.StartsWith("Mosaic", StringComparison.Ordinal)) result = true;
                    if (!result)
                    {
                        var mat = r.Material; // renderer 自己的材质实例（非泛型 getter）
                        if (mat != null && mat.shader != null && mat.shader.name.Contains("Live2DMosaic")) result = true;
                    }
                }
                catch { }
                _isMosaicCache[id] = result;
                return result;
            }
            catch { return false; }
        }

        // ---------- ① 可见性拦截 ----------

        private static bool BlockMosaicVis(CubismRenderer __instance, bool isVisible)
        {
            if (!Enabled) return true;
            bool isM = IsMosaic(__instance);
            if (isM && isVisible)
            {
                Off(__instance, "vis");
                return false; // 跳过原方法：BecomeVisible 不执行
            }
            return true;
        }

        // ---------- ② 顶点更新拦截 ----------

        private static bool BlockMosaicVertex(CubismRenderer __instance)
        {
            if (!Enabled) return true;
            if (IsMosaic(__instance)) return false; // 顶点不更新
            return true;
        }

        // ---------- ③ LateUpdate 后兜底 ----------

        private static void KillAfterLateUpdate(CubismRenderController __instance)
        {
            if (!Enabled || __instance == null) return;
            try
            {
                var rs = __instance.Renderers; // Il2CppReferenceArray<CubismRenderer>
                if (rs == null) return;
                int n = rs.Length;
                for (int i = 0; i < n; i++)
                {
                    var r = rs[i];
                    if (r == null) continue;
                    if (IsMosaic(r)) Off(r, "late");
                }
            }
            catch (Exception e)
            {
                if (!_visLogged) { _visLogged = true; MelonLogger.Warning("[Mosaic] late err: " + e.Message); }
            }
        }

        private static void Off(CubismRenderer r, string src)
        {
            try
            {
                bool did = false;
                var mr = r.MeshRenderer;
                if (mr != null && mr.enabled) { mr.enabled = false; did = true; }
                var go = r.gameObject;
                if (go != null && go.activeSelf) { go.SetActive(false); did = true; }
                if (did)
                {
                    _neutralized++;
                    if (_neutralized <= 10 || _neutralized % 100 == 0)
                        MelonLogger.Msg($"[Mosaic] killed #{_neutralized} [{src}] {r.name}");
                }
            }
            catch { }
        }

    }
}
