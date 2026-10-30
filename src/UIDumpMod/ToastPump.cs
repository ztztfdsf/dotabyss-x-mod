using System;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace UIDumpMod
{
    /// <summary>
    /// AbyssMod 原版 Toast 的每帧驱动（替代 1.15.0 下注入必崩的 ToastBehaviour）。
    /// 只做三件事：AttachRenderer(null) 一次、OnUpdate 调 ProcessFrame、OnGUI 调 Render。
    /// 渲染器/主题/动画/排队逻辑全部是 Utility.dll 里的原版代码，这里只是「心脏起搏器」。
    /// </summary>
    internal static class ToastPump
    {
        private const string RuntimeTypeName = "Utility.Notifications.Internal.ToastRuntime";
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static MelonLogger.Instance _log;
        private static object _runtime;
        private static Action<float> _processFrame;
        private static Action _render;
        private static bool _dead;

        internal static void Attach(MelonLogger.Instance log)
        {
            _log = log;
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Utility");
                var type = asm?.GetType(RuntimeTypeName);
                if (type == null) { Fail("ToastRuntime type not found (AbyssMod/Utility.dll not loaded?)"); return; }

                _runtime = type.GetProperty("Shared", Any)?.GetValue(null);
                var attach = type.GetMethod("AttachRenderer", Any);
                var process = type.GetMethod("ProcessFrame", Any);
                var render = type.GetMethod("Render", Any);
                if (_runtime == null || attach == null || process == null || render == null) { Fail("ToastRuntime members not found"); return; }

                _processFrame = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), _runtime, process);
                _render = (Action)Delegate.CreateDelegate(typeof(Action), _runtime, render);
                attach.Invoke(_runtime, new object[] { null });
                _log.Msg("toast pump attached (original AbyssMod Toast renderer)");
            }
            catch (Exception e)
            {
                Fail("attach failed: " + (e.InnerException ?? e).Message);
            }
        }

        internal static void Tick()
        {
            if (_dead || _processFrame == null) return;
            try { _processFrame(Time.unscaledDeltaTime); }
            catch (Exception e) { Fail("ProcessFrame failed: " + e.Message); }
        }

        internal static void Gui()
        {
            if (_dead || _render == null) return;
            try { _render(); }
            catch (Exception e) { Fail("Render failed: " + e.Message); }
        }

        private static void Fail(string msg)
        {
            _dead = true;
            _log?.Warning("[ToastPump] " + msg + " — toast disabled, translation unaffected");
        }
    }
}
