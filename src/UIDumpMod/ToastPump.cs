using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using Il2CppTMPro;
using Il2CppInterop.Runtime;
using Object = UnityEngine.Object;

namespace UIDumpMod
{
    /// <summary>
    /// 原版视觉 Toast 的自绘实现（1.15.0）。
    /// 背景：Utility.dll（按旧 interop 编译）自己的渲染链在新 interop 方法表下部分槽位错位 ——
    /// 实测 Activator.CreateInstance(ToastTheme) 直接 coreclr abort（step6a 铁证）；
    /// 但我们新写的代码 Vector2/3/Color ctor、GameObject ctor 槽位全绿（p0 系列铁证）。
    /// 结论：里子全部自己写，永远不执行 Utility 的渲染代码；
    /// 泛型 AddComponent&lt;T&gt; 的 MethodInfoStoreGeneric 静态初始化会炸 → 一律走 Il2CppType.Of 非泛型重载。
    /// 面子参数照抄反编译的原版 ToastTheme：425×105 近黑 94% 卡、右下角 20 边距、白标题/灰正文、彩色 accent、7 秒、最多 4 张。
    /// 锚点：不枚举 Canvas（FindObjectsOfType 泛型也是雷），直接用翻译层每帧都摸到的活 TMP 的 canvas。
    /// </summary>
    internal static class ToastPump
    {
        private static MelonLogger.Instance _log;
        private static bool _probed, _uguiOk;

        internal static void Attach(MelonLogger.Instance log)
        {
            _log = log;
            _log.Msg("[TP] pump attached (self-drawn UGUI toast)");
        }

        // ---------- 非泛型 AddComponent（绕开 MethodInfoStoreGeneric 雷） ----------
        private static T AddC<T>(GameObject go) where T : Component
            => go.AddComponent(Il2CppType.Of<T>()).Cast<T>();

        // ---------- 锚点（翻译层喂进来的活 TMP） ----------
        private static Transform _canvasT;
        private static TMP_FontAsset _font;
        internal static void OnAnchor(TextMeshProUGUI t)
        {
            if (_canvasT != null || t == null) return;
            try
            {
                var c = t.canvas;
                if (c != null)
                {
                    _canvasT = c.transform;
                    if (_font == null) _font = t.font;
                    _log?.Msg("[TP] anchor canvas ok, font=" + (_font != null ? _font.name : "(null)"));
                }
            }
            catch { }
        }

        // ---------- 队列 ----------
        internal struct Item { internal string Title, Msg; internal float Ttl, Age; internal int Kind; }
        private static readonly List<Item> _queue = new List<Item>();
        private static readonly List<GameObject> _cards = new List<GameObject>();
        private static readonly List<float> _cardAge = new List<float>();
        private static float _yCursor;

        internal static void Show(string title, string msg, int kind, float duration)
        {
            if (_queue.Count < 8) _queue.Add(new Item { Title = title, Msg = msg, Ttl = duration > 0 ? duration : 7f, Kind = kind });
        }

        // ---------- 每帧心跳 ----------
        internal static void Tick(float dt)
        {
            if (!_probed)
            {
                if (_canvasT == null) return;
                ProbeOnce();
                if (!_probed) return;
            }
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] == null) continue;
                var rt = _cards[i].GetComponent<RectTransform>();
                if (rt == null) continue;
                if (rt.localScale.x < 1f) rt.localScale = Vector3.one * Mathf.Min(1f, rt.localScale.x + dt * 6f);
                _cardAge[i] += dt;
                if (_cardAge[i] > _ttl[i]) // 到期：移出屏（Destroy 槽位有毒，只隐藏不销毁，池化复用）
                    rt.anchoredPosition = new Vector2(99999f, 99999f);
            }
            if (_cards.All(c => Offscreen(c))) _yCursor = 0;
            if (!_uguiOk || _queue.Count == 0 || _cards.Count(c => !Offscreen(c)) >= 4 || _canvasT == null) return;
            try { SpawnCard(_queue[0]); _queue.RemoveAt(0); }
            catch (Exception e) { _uguiOk = false; _log.Warning("[TP] spawn failed, toast off: " + e.Message); }
        }

        private static bool Offscreen(GameObject c)
            => c == null || c.GetComponent<RectTransform>()?.anchoredPosition.x > 90000f;
        private static readonly List<float> _ttl = new List<float>();

        // ---------- 探针：一次性验证自绘链 ----------
        internal static void ProbeOnce()
        {
            if (_probed) return;
            _probed = true;
            try
            {
                var v2 = new Vector2(1f, 2f); _log.Msg("[TP] p0a Vector2 ctor ok " + v2.x);
                var v3 = new Vector3(1f, 2f, 3f); _log.Msg("[TP] p0b Vector3 ctor ok " + v3.z);
                var c0 = new Color(0.5f, 0.5f, 0.5f, 1f); _log.Msg("[TP] p0c Color ctor ok " + c0.r);
                var cw = Color.white; _log.Msg("[TP] p0d Color.white ok " + cw.a);
                var go = new GameObject("uidump_probe");
                _log.Msg("[TP] p1 GameObject ctor ok");
                var rt = AddC<RectTransform>(go);
                _log.Msg("[TP] p2 AddComponent(RectTransform via Il2CppType) ok");
                var tmp = AddC<TextMeshProUGUI>(go);
                _log.Msg("[TP] p3 AddComponent(TMP) ok");
                tmp.text = "probe";
                _log.Msg("[TP] p4 set_text ok");
                // Image 必须在有 Canvas 祖先的地方测（裸 GO 上 OnEnable 拿不到 Canvas 会 NRE）
                if (_canvasT != null)
                {
                    var imgGo = new GameObject("pi");
                    var irt = AddC<RectTransform>(imgGo);
                    irt.SetParent(_canvasT, false);
                    var img = AddC<UnityEngine.UI.Image>(imgGo);
                    img.color = new Color(1f, 1f, 1f, 0.5f);
                    _log.Msg("[TP] p5 AddComponent(Image under canvas) ok");
                    rt.sizeDelta = new Vector2(100, 40);
                    _log.Msg("[TP] p6 RectTransform.sizeDelta ok");
                    var child = new GameObject("c");
                    var crt = AddC<RectTransform>(child);
                    crt.SetParent(irt, false);
                    _log.Msg("[TP] p7 SetParent ok —— 自绘链全绿（Destroy 槽位有毒，卡片改池化不销毁）");
                    _uguiOk = true;
                }
                else _log.Warning("[TP] no canvas anchor yet; Image untested");
            }
            catch (Exception e) { _log.Warning("[TP] probe failed: " + (e.InnerException ?? e).Message); }
        }

        // ---------- 卡片（参数照抄原版 ToastTheme） ----------
        private static void SpawnCard(Item it)
        {
            const float W = 425f, Hmin = 105f, Margin = 20f, Gap = 15f;
            var rootGo = new GameObject("uidump_toast");
            var rrt = AddC<RectTransform>(rootGo);
            rrt.SetParent(_canvasT, false);
            rrt.SetAsLastSibling();

            var bgGo = new GameObject("bg"); var brt = AddC<RectTransform>(bgGo);
            brt.SetParent(rrt, false);
            var bg = AddC<UnityEngine.UI.Image>(bgGo);
            bg.color = new Color(0.06f, 0.06f, 0.08f, 0.94f);
            bg.raycastTarget = false;

            var accentGo = new GameObject("a"); var art = AddC<RectTransform>(accentGo);
            art.SetParent(rrt, false);
            var accent = AddC<UnityEngine.UI.Image>(accentGo);
            accent.color = it.Kind switch
            {
                1 => new Color(0.26f, 0.71f, 0.51f, 1f),   // Success
                2 => new Color(0.94f, 0.63f, 0.21f, 1f),   // Warning
                3 => new Color(0.94f, 0.28f, 0.28f, 1f),   // Error
                _ => new Color(0.29f, 0.56f, 0.85f, 1f),   // Info
            };
            accent.raycastTarget = false;

            var titleGo = new GameObject("t"); var trt = AddC<RectTransform>(titleGo);
            trt.SetParent(rrt, false);
            var title = AddC<TextMeshProUGUI>(titleGo);
            title.font = _font; title.fontSize = 19; title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.95f, 0.95f, 0.97f, 1f);
            title.text = it.Title ?? ""; title.raycastTarget = false;

            var bodyGo = new GameObject("b"); var bb = AddC<RectTransform>(bodyGo);
            bb.SetParent(rrt, false);
            var body = AddC<TextMeshProUGUI>(bodyGo);
            body.font = _font; body.fontSize = 16;
            body.color = new Color(0.70f, 0.70f, 0.75f, 1f);
            body.text = it.Msg ?? ""; body.alignment = TextAlignmentOptions.TopLeft; body.raycastTarget = false;

            // 布局（多行粗估）
            float h = Mathf.Max(Hmin, 60f + (it.Msg ?? "").Length / W * 32f * 16f);
            rrt.sizeDelta = new Vector2(W, h);
            rrt.anchorMin = rrt.anchorMax = new Vector2(1f, 0f);
            rrt.pivot = new Vector2(1f, 0f);
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            art.anchorMin = new Vector2(0f, 0f); art.anchorMax = new Vector2(0f, 1f);
            art.pivot = new Vector2(0f, 0.5f); art.sizeDelta = new Vector2(6f, 0f); art.anchoredPosition = Vector2.zero;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f); trt.anchoredPosition = new Vector2(-14f, -12f); trt.sizeDelta = new Vector2(-34f, 24f);
            bb.anchorMin = new Vector2(0f, 1f); bb.anchorMax = new Vector2(1f, 1f);
            bb.pivot = new Vector2(0.5f, 1f); bb.anchoredPosition = new Vector2(-14f, -42f); bb.sizeDelta = new Vector2(-34f, h - 54f);

            rrt.anchoredPosition = new Vector2(-Margin, Margin + _yCursor);
            _yCursor += h + Gap;
            rrt.localScale = Vector3.one * 0.4f;
            _cards.Add(rootGo);
            _cardAge.Add(0f);
            _ttl.Add(it.Ttl);
        }
    }
}
