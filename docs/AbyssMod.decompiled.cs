using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AbyssMod;
using AbyssMod.Patches;
using AbyssMod.Services;
using HarmonyLib;
using Il2CppAbsf;
using Il2CppAbsf.Master;
using Il2CppAbsf.Novel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppLive2D.Cubism.Core;
using Il2CppProject;
using Il2CppProject.Master;
using Il2CppProject.Notice;
using Il2CppProject.Novel;
using Il2CppProject.Title;
using Il2CppProject.User;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using Il2CppSystem.Reflection;
using Il2CppSystem.Threading;
using Il2CppTMPro;
using MelonLoader;
using MelonLoader.Preferences;
using MelonLoader.Utils;
using UnityEngine;
using Utility.Assets;
using Utility.Caching;
using Utility.Cryptography;
using Utility.Diagnostics;
using Utility.Notifications;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(/*Could not decode attribute arguments.*/)]
[assembly: MelonInfo(typeof(Core), "AbyssMod", "1.1.4", "Jitsu", null)]
[assembly: HarmonyDontPatchAll]
[assembly: TargetFramework(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
[assembly: AssemblyCompany("AbyssMod")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyFileVersion("1.1.4.0")]
[assembly: AssemblyInformationalVersion("1.1.4+7895d8be33d4e3b7ad26bac99c491b90eed8d149")]
[assembly: AssemblyProduct("AbyssMod")]
[assembly: AssemblyTitle("AbyssMod")]
[assembly: AssemblyVersion("1.1.4.0")]
[module: System.Runtime.CompilerServices.RefSafetyRules(11)]
namespace AbyssMod
{
	public static class Config
	{
		public static readonly string FilePath = Path.Combine(MelonEnvironment.UserDataDirectory, "AbyssMod.cfg");

		public static MelonPreferences_Entry<bool> DynamicMosaic;

		public static MelonPreferences_Entry<bool> HideNovelGlasses;

		public static MelonPreferences_Entry<bool> SoundCaution;

		public static MelonPreferences_Entry<bool> VoiceInterruption;

		public static MelonPreferences_Entry<bool> TitleMovie;

		public static MelonPreferences_Entry<float> NovelLive2DScale;

		public static MelonPreferences_Entry<bool> Translation;

		public static MelonPreferences_Entry<string> TranslationCDN;

		public static MelonPreferences_Entry<string> TranslationLanguage;

		public static MelonPreferences_Entry<string> TranslationCacheDirectory;

		public static MelonPreferences_Entry<bool> TranslationPreferLocalFiles;

		public static MelonPreferences_Entry<string> TranslationCryptoTag;

		public static MelonPreferences_Entry<string> TranslationCryptoKey;

		public static MelonPreferences_Entry<string> FontBundlePath;

		private static bool _initializing;

		private static readonly List<Action> Subscriptions = new List<Action>();

		private static bool _entriesBound;

		private static MelonPreferences_Category _preferenceCategory;

		private static readonly List<MelonPreferences_Category> PreferenceCategories = new List<MelonPreferences_Category>();

		internal static string DefaultFontBundlePath => Path.Combine(MelonEnvironment.UserDataDirectory, "AbyssMod", "ttcuyuanj");

		internal static string LegacyFontBundlePath => Path.Combine(Application.persistentDataPath, "il2cpp", "ttcuyuanj");

		[field: CompilerGenerated]
		internal static bool TranslationEnabledAtStartup
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			private set;
		}

		public static void Initialize()
		{
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			Shutdown();
			_initializing = true;
			try
			{
				if (!_entriesBound)
				{
					BindAllEntries();
					_entriesBound = true;
				}
				_preferenceCategory.LoadFromFile(false);
				Enumerator<MelonPreferences_Category> enumerator = PreferenceCategories.GetEnumerator();
				try
				{
					while (enumerator.MoveNext())
					{
						enumerator.Current.SaveToFile(false);
					}
				}
				finally
				{
					((System.IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
				}
				TranslationEnabledAtStartup = Translation.Value;
			}
			finally
			{
				_initializing = false;
			}
		}

		private static void BindAllEntries()
		{
			MelonPreferences_Category category = CreateCategory("General");
			DynamicMosaic = CreateEntry(category, "DynamicMosaic", defaultValue: false, "是否启用游戏内动态马赛克");
			HideNovelGlasses = CreateEntry(category, "HideNovelGlasses", defaultValue: false, "是否隐藏寝室 CG 中角色眼镜；下次载入 Live2D 生效，与马赛克开关独立");
			SoundCaution = CreateEntry(category, "SoundCaution", defaultValue: false, "是否启用进入游戏时的音量提醒弹窗");
			VoiceInterruption = CreateEntry(category, "VoiceInterruption", defaultValue: false, "剧情中播放下一段无声文本时是否中断当前角色语音");
			TitleMovie = CreateEntry(category, "TitleMovie", defaultValue: true, "是否开启进入游戏时的标题动画");
			NovelLive2DScale = CreateEntry(category, "NovelLive2DScale", 1f, "剧情 Live2D 的缩放倍率（进剧情时应用，范围 0.1 ~ 10.0）");
			MelonPreferences_Category category2 = CreateCategory("Translation");
			Translation = CreateEntry(category2, "Enabled", defaultValue: true, "是否开启翻译；MasterData 与 UI 文本仅在启动时读取此设置，剧情翻译可在运行时切换");
			TranslationCDN = CreateEntry(category2, "CDN", "https://raw.githubusercontent.com/anosu/dotabyss-translation/refs/heads/main/translations", "翻译加载的CDN，修改后重启生效");
			TranslationLanguage = CreateEntry(category2, "Language", "zh_Hans", "翻译语言，取值范围：[zh_Hans]，修改后重启生效");
			MelonPreferences_Category category3 = CreateCategory("Translation.Cache");
			TranslationCacheDirectory = CreateEntry(category3, "Directory", "AbyssMod/translations", "翻译缓存目录，默认相对于用户数据目录，也可使用绝对路径；修改后重启生效");
			TranslationPreferLocalFiles = CreateEntry(category3, "PreferLocalFiles", defaultValue: false, "本地翻译文件存在时是否忽略清单哈希并优先使用本地文件（manifest 除外）；修改后重启生效");
			MelonPreferences_Category category4 = CreateCategory("Translation.Crypto");
			TranslationCryptoTag = CreateEntry(category4, "Tag", "ENC:", "翻译文本加密标签（可选）");
			TranslationCryptoKey = CreateEntry(category4, "Key", "woshitonghuadawang", "翻译文本解密密钥（可选）");
			FontBundlePath = CreateEntry(CreateCategory("Translation.Font"), "AssetBundlePath", DefaultFontBundlePath, "TMP字体AssetBundle的路径，默认取 MelonLoader/UserData/AbyssMod/ttcuyuanj，也可使用绝对路径；修改后重启生效");
		}

		private static MelonPreferences_Category CreateCategory(string name)
		{
			MelonPreferences_Category val = MelonPreferences.CreateCategory(name);
			val.SetFilePath(FilePath, false, false);
			if (_preferenceCategory == null)
			{
				_preferenceCategory = val;
			}
			PreferenceCategories.Add(val);
			return val;
		}

		private static MelonPreferences_Entry<T> CreateEntry<T>(MelonPreferences_Category category, string key, T defaultValue, string description)
		{
			ValueValidator val = null;
			string text = defaultValue as string;
			bool flag = text != null;
			if (flag)
			{
				bool flag2 = category.Identifier.Contains("Cache") || category.Identifier.Contains("Font");
				if (!flag2)
				{
					string text2 = key;
					bool flag3 = ((text2 == "CDN" || text2 == "Language") ? true : false);
					flag2 = flag3;
				}
				flag = flag2;
			}
			if (flag)
			{
				val = (ValueValidator)(object)new ResourceStringValidator(text, key == "CDN");
			}
			if (key == "NovelLive2DScale")
			{
				val = (ValueValidator)(object)new ValueRange<float>(0.1f, 10f);
			}
			MelonPreferences_Entry<T> entry = category.GetEntry<T>(key) ?? category.CreateEntry<T>(key, defaultValue, description, description, false, false, val, (string)null);
			Subscribe<T>(entry, delegate(T _, T newValue)
			{
				if (!_initializing)
				{
					category.SaveToFile(false);
					object obj = (((object)entry == TranslationCryptoKey) ? "***" : ((object)newValue));
					Core.Log.Msg($"[{category.Identifier}] {key} => {obj}");
					if (newValue is bool)
					{
						Toast.Info("[" + category.Identifier + "]", $"{key} => {obj}");
					}
				}
			});
			return entry;
		}

		private static void Subscribe<T>(MelonPreferences_Entry<T> entry, LemonAction<T, T> callback)
		{
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Expected O, but got Unknown
			((MelonEventBase<LemonAction<T, T>>)(object)entry.OnEntryValueChanged).Subscribe(callback, 0, false);
			Subscriptions.Add((Action)delegate
			{
				((MelonEventBase<LemonAction<T, T>>)(object)entry.OnEntryValueChanged).Unsubscribe(callback);
			});
		}

		internal static void Shutdown()
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			_initializing = true;
			Enumerator<Action> enumerator = Subscriptions.GetEnumerator();
			try
			{
				while (enumerator.MoveNext())
				{
					enumerator.Current.Invoke();
				}
			}
			finally
			{
				((System.IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
			}
			Subscriptions.Clear();
			PreferenceCategories.Clear();
			_entriesBound = false;
		}
	}
	public sealed class Core : MelonMod
	{
		[Serializable]
		[CompilerGenerated]
		private sealed class <>c
		{
			public static readonly <>c <>9 = new <>c();

			public static LogSink <>9__15_0;

			public static Func<ValueTuple<string, string>> <>9__16_0;

			public static Action <>9__17_0;

			public static Action<System.Exception> <>9__17_1;

			internal void <InitializeUtility>b__15_0(LogEntry entry)
			{
				string text = ((entry.Exception == null) ? ("[" + entry.Category + "] " + entry.Message) : $"[{entry.Category}] {entry.Message}\n{entry.Exception}");
				switch (entry.Level)
				{
				case LogLevel.Warning:
					Log.Warning(text);
					break;
				case LogLevel.Error:
					Log.Error(text);
					break;
				default:
					Log.Msg(text);
					break;
				}
			}

			internal ValueTuple<string, string> <Initialize>b__16_0()
			{
				//IL_0014: Unknown result type (might be due to invalid IL or missing references)
				return new ValueTuple<string, string>(Config.TranslationCryptoTag.Value, Config.TranslationCryptoKey.Value);
			}

			internal void <InitializeFont>b__17_0()
			{
				if (!TMP_Settings.fallbackFontAssets.Contains(_font.Asset))
				{
					TMP_Settings.fallbackFontAssets.Add(_font.Asset);
				}
				Logger.Info("Font loaded: " + ((Object)_font.Asset).name);
			}

			internal void <InitializeFont>b__17_1(System.Exception error)
			{
				Toast.Error("字体加载失败", error.Message);
			}
		}

		private const int HttpTimeoutSeconds = 10;

		private const int PooledConnectionLifetimeMinutes = 5;

		private const int PooledConnectionIdleTimeoutMinutes = 2;

		public static TranslationManager Trans;

		private static HttpClient _httpClient;

		private static AssetBundleLoader<TMP_FontAsset> _font;

		private static System.Collections.IEnumerator _fontLoad;

		private static object _fontCoroutine;

		[field: CompilerGenerated]
		public static Instance Log
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			private set;
		}

		public override void OnInitializeMelon()
		{
			Log = ((MelonBase)this).LoggerInstance;
			try
			{
				InitializeUtility();
				Config.Initialize();
				Initialize();
				Trans.Initialize();
				InitializeFont();
				PatchManager.Initialize();
				Toast.Success("AbyssMod", "Mod 加载成功，版本: 1.1.4", 7f);
			}
			catch (System.Exception ex)
			{
				Log.Error($"Initialization failed: {ex}");
				Shutdown();
				throw;
			}
		}

		public override void OnDeinitializeMelon()
		{
			Shutdown();
		}

		private static void Shutdown()
		{
			try
			{
				if (!PatchManager.Shutdown())
				{
					return;
				}
			}
			catch (System.Exception ex)
			{
				Instance log = Log;
				if (log != null)
				{
					log.Error($"Patch shutdown failed: {ex}");
				}
				return;
			}
			try
			{
				Trans?.Shutdown();
			}
			catch (System.Exception ex2)
			{
				Instance log2 = Log;
				if (log2 != null)
				{
					log2.Error($"Translation shutdown failed: {ex2}");
				}
			}
			Trans = null;
			Config.Shutdown();
			if (_fontCoroutine != null)
			{
				MelonCoroutines.Stop(_fontCoroutine);
			}
			(_fontLoad as System.IDisposable)?.Dispose();
			AssetBundleLoader<TMP_FontAsset> font = _font;
			if (font != null && font.IsLoaded)
			{
				TMP_Settings.fallbackFontAssets.Remove(_font.Asset);
			}
			_font = null;
			_fontLoad = null;
			_fontCoroutine = null;
			HttpClient httpClient = _httpClient;
			if (httpClient != null)
			{
				((HttpMessageInvoker)httpClient).Dispose();
			}
			_httpClient = null;
			try
			{
				Toast.Shutdown();
			}
			catch (System.Exception ex3)
			{
				Instance log3 = Log;
				if (log3 != null)
				{
					log3.Error($"Toast shutdown failed: {ex3}");
				}
			}
			Logging.SetSink(null);
		}

		private static void InitializeUtility()
		{
			Logging.SetSink(delegate(LogEntry entry)
			{
				string text = ((entry.Exception == null) ? ("[" + entry.Category + "] " + entry.Message) : $"[{entry.Category}] {entry.Message}\n{entry.Exception}");
				switch (entry.Level)
				{
				case LogLevel.Warning:
					Log.Warning(text);
					break;
				case LogLevel.Error:
					Log.Error(text);
					break;
				default:
					Log.Msg(text);
					break;
				}
			});
			Toast.Initialize("AbyssMod.ToastManager");
		}

		private static void Initialize()
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Expected O, but got Unknown
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Expected O, but got Unknown
			_httpClient = new HttpClient((HttpMessageHandler)(object)new TranslationCryptoHandler((HttpMessageHandler)new SocketsHttpHandler
			{
				AutomaticDecompression = (DecompressionMethods)7,
				PooledConnectionLifetime = TimeSpan.FromMinutes(5L),
				PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2L)
			}, () => new ValueTuple<string, string>(Config.TranslationCryptoTag.Value, Config.TranslationCryptoKey.Value)))
			{
				Timeout = TimeSpan.FromSeconds(10L)
			};
			_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AbyssMod/1.1.4");
			string text = ResolveUserDataPath(Config.TranslationCacheDirectory.Value);
			Log.Msg("Translation cache directory: " + text);
			Trans = new TranslationManager(new TranslationCache(Config.TranslationCDN.Value, text, Config.TranslationLanguage.Value, Config.TranslationPreferLocalFiles.Value, _httpClient));
		}

		private static void InitializeFont()
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Expected O, but got Unknown
			_font = new AssetBundleLoader<TMP_FontAsset>(ResolveFontBundlePath(Config.FontBundlePath.Value));
			AssetBundleLoader<TMP_FontAsset> font = _font;
			object obj = <>c.<>9__17_0;
			if (obj == null)
			{
				Action val = delegate
				{
					if (!TMP_Settings.fallbackFontAssets.Contains(_font.Asset))
					{
						TMP_Settings.fallbackFontAssets.Add(_font.Asset);
					}
					Logger.Info("Font loaded: " + ((Object)_font.Asset).name);
				};
				<>c.<>9__17_0 = val;
				obj = (object)val;
			}
			_fontLoad = font.Load((Action?)obj, delegate(System.Exception error)
			{
				Toast.Error("字体加载失败", error.Message);
			});
			_fontCoroutine = MelonCoroutines.Start(_fontLoad);
		}

		private static string ResolveFontBundlePath(string path)
		{
			string text = ResolveModsPath(path);
			if (File.Exists(text))
			{
				if (string.Equals(text, Config.DefaultFontBundlePath, (StringComparison)4))
				{
					Log.Msg("Using packaged UserData font bundle: " + text);
				}
				return text;
			}
			if (!string.Equals(text, Config.LegacyFontBundlePath, (StringComparison)4))
			{
				return text;
			}
			if (File.Exists(Config.DefaultFontBundlePath))
			{
				Log.Msg("Using packaged UserData font bundle: " + Config.DefaultFontBundlePath);
				return Config.DefaultFontBundlePath;
			}
			return text;
		}

		private static string ResolveModsPath(string path)
		{
			return ResolvePath(path, MelonEnvironment.ModsDirectory);
		}

		private static string ResolveUserDataPath(string path)
		{
			return ResolvePath(path, MelonEnvironment.UserDataDirectory);
		}

		private static string ResolvePath(string path, string relativeBase)
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			if (string.IsNullOrWhiteSpace(path))
			{
				throw new ArgumentException("Configured path cannot be empty", "path");
			}
			return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(relativeBase, path));
		}
	}
	public static class Logger
	{
		public static void Info(string msg)
		{
			Core.Log.Msg(msg);
		}

		public static void Warn(string msg)
		{
			Core.Log.Warning(msg);
		}

		public static void Error(string msg)
		{
			Core.Log.Error(msg);
		}
	}
	public static class ModInfo
	{
		public const string Name = "AbyssMod";

		public const string Author = "Jitsu";

		public const string Version = "1.1.4";
	}
	internal sealed class ResourceStringValidator : ValueValidator
	{
		private readonly string _defaultValue;

		private readonly bool _httpUrl;

		internal ResourceStringValidator(string defaultValue, bool httpUrl = false)
		{
			_defaultValue = defaultValue;
			_httpUrl = httpUrl;
		}

		public override bool IsValid(object value)
		{
			if (value is string text && text.Trim().Length > 0)
			{
				if (_httpUrl)
				{
					Uri val = default(Uri);
					if (Uri.TryCreate(text.Trim(), (UriKind)1, ref val))
					{
						if (!(val.Scheme == Uri.UriSchemeHttp))
						{
							return val.Scheme == Uri.UriSchemeHttps;
						}
						return true;
					}
					return false;
				}
				return true;
			}
			return false;
		}

		public override object EnsureValid(object value)
		{
			if (!((ValueValidator)this).IsValid(value))
			{
				return _defaultValue;
			}
			return ((string)value).Trim();
		}
	}
	public class Manifest
	{
		[JsonPropertyName("hash")]
		[field: CompilerGenerated]
		public string Hash
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		[JsonExtensionData]
		[field: CompilerGenerated]
		public Dictionary<string, JsonElement> Extra
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		} = new Dictionary<string, JsonElement>();

		[JsonPropertyName("novels")]
		[field: CompilerGenerated]
		public Dictionary<string, string> Novels
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			set;
		}

		public string GetFileHash(string type)
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Invalid comparison between Unknown and I4
			JsonElement val = default(JsonElement);
			if (!Extra.TryGetValue(type, ref val) || (int)((JsonElement)(ref val)).ValueKind != 3)
			{
				return null;
			}
			return ((JsonElement)(ref val)).GetString();
		}
	}
}
namespace AbyssMod.Services
{
	internal static class HttpContentReader
	{
		[StructLayout((LayoutKind)3)]
		[CompilerGenerated]
		private struct <ReadAsync>d__0 : IAsyncStateMachine
		{
			public int <>1__state;

			public AsyncTaskMethodBuilder<byte[]> <>t__builder;

			public HttpContent content;

			public long maxBytes;

			public CancellationToken token;

			private Stream <stream>5__2;

			private MemoryStream <body>5__3;

			private byte[] <buffer>5__4;

			private ConfiguredTaskAwaiter<Stream> <>u__1;

			private ConfiguredValueTaskAwaiter<int> <>u__2;

			private void MoveNext()
			{
				//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
				//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
				//IL_00db: Unknown result type (might be due to invalid IL or missing references)
				//IL_0085: Unknown result type (might be due to invalid IL or missing references)
				//IL_0090: Unknown result type (might be due to invalid IL or missing references)
				//IL_0095: Unknown result type (might be due to invalid IL or missing references)
				//IL_0099: Unknown result type (might be due to invalid IL or missing references)
				//IL_009e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0078: Unknown result type (might be due to invalid IL or missing references)
				//IL_0101: Unknown result type (might be due to invalid IL or missing references)
				//IL_010b: Expected O, but got Unknown
				//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
				//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
				//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
				//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
				//IL_019c: Unknown result type (might be due to invalid IL or missing references)
				//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
				//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
				//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
				//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
				//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
				//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
				//IL_0171: Unknown result type (might be due to invalid IL or missing references)
				int num = <>1__state;
				byte[] result3;
				try
				{
					ConfiguredTaskAwaiter<Stream> val;
					if (num != 0)
					{
						if (num == 1)
						{
							goto IL_00fc;
						}
						if (content.Headers.ContentLength > maxBytes)
						{
							throw new InvalidDataException($"download exceeds {maxBytes} bytes");
						}
						val = content.ReadAsStreamAsync(token).ConfigureAwait(false).GetAwaiter();
						if (!val.IsCompleted)
						{
							num = (<>1__state = 0);
							<>u__1 = val;
							<>t__builder.AwaitUnsafeOnCompleted<ConfiguredTaskAwaiter<Stream>, <ReadAsync>d__0>(ref val, ref this);
							return;
						}
					}
					else
					{
						val = <>u__1;
						<>u__1 = default(ConfiguredTaskAwaiter<Stream>);
						num = (<>1__state = -1);
					}
					Stream result = val.GetResult();
					<stream>5__2 = result;
					goto IL_00fc;
					IL_00fc:
					try
					{
						if (num != 1)
						{
							<body>5__3 = new MemoryStream();
						}
						try
						{
							if (num != 1)
							{
								<buffer>5__4 = new byte[81920];
								goto IL_018a;
							}
							ConfiguredValueTaskAwaiter<int> val2 = <>u__2;
							<>u__2 = default(ConfiguredValueTaskAwaiter<int>);
							num = (<>1__state = -1);
							goto IL_0205;
							IL_0205:
							int result2;
							if ((result2 = val2.GetResult()) > 0)
							{
								if (((Stream)<body>5__3).Length + result2 > maxBytes)
								{
									throw new InvalidDataException($"download exceeds {maxBytes} bytes");
								}
								((Stream)<body>5__3).Write(<buffer>5__4, 0, result2);
								goto IL_018a;
							}
							result3 = <body>5__3.ToArray();
							goto end_IL_010c;
							IL_018a:
							val2 = <stream>5__2.ReadAsync(MemoryExtensions.AsMemory<byte>(<buffer>5__4), token).ConfigureAwait(false).GetAwaiter();
							if (!val2.IsCompleted)
							{
								num = (<>1__state = 1);
								<>u__2 = val2;
								<>t__builder.AwaitUnsafeOnCompleted<ConfiguredValueTaskAwaiter<int>, <ReadAsync>d__0>(ref val2, ref this);
								return;
							}
							goto IL_0205;
							end_IL_010c:;
						}
						finally
						{
							if (num < 0 && <body>5__3 != null)
							{
								((System.IDisposable)<body>5__3).Dispose();
							}
						}
					}
					finally
					{
						if (num < 0 && <stream>5__2 != null)
						{
							((System.IDisposable)<stream>5__2).Dispose();
						}
					}
				}
				catch (System.Exception exception)
				{
					<>1__state = -2;
					<stream>5__2 = null;
					<body>5__3 = null;
					<buffer>5__4 = null;
					<>t__builder.SetException(exception);
					return;
				}
				<>1__state = -2;
				<stream>5__2 = null;
				<body>5__3 = null;
				<buffer>5__4 = null;
				<>t__builder.SetResult(result3);
			}

			[DebuggerHidden]
			private void SetStateMachine(IAsyncStateMachine stateMachine)
			{
				<>t__builder.SetStateMachine(stateMachine);
			}
		}

		[AsyncStateMachine(typeof(<ReadAsync>d__0))]
		internal static async System.Threading.Tasks.Task<byte[]> ReadAsync(HttpContent content, long maxBytes, CancellationToken token)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			if (content.Headers.ContentLength > maxBytes)
			{
				throw new InvalidDataException($"download exceeds {maxBytes} bytes");
			}
			Stream stream = await content.ReadAsStreamAsync(token).ConfigureAwait(false);
			try
			{
				MemoryStream body = new MemoryStream();
				try
				{
					byte[] buffer = new byte[81920];
					int num;
					while ((num = await stream.ReadAsync(MemoryExtensions.AsMemory<byte>(buffer), token).ConfigureAwait(false)) > 0)
					{
						if (((Stream)body).Length + num > maxBytes)
						{
							throw new InvalidDataException($"download exceeds {maxBytes} bytes");
						}
						((Stream)body).Write(buffer, 0, num);
					}
					return body.ToArray();
				}
				finally
				{
					((System.IDisposable)body)?.Dispose();
				}
			}
			finally
			{
				((System.IDisposable)stream)?.Dispose();
			}
		}
	}
	internal sealed class MasterDataTranslator
	{
		private sealed class TableTranslator
		{
			private readonly Func<IMasterLoadResult, System.Collections.IEnumerable> _getRows;

			private readonly System.Collections.Generic.IReadOnlyList<FieldBinding> _fields;

			[field: CompilerGenerated]
			public string TableName
			{
				[CompilerGenerated]
				get;
			}

			[field: CompilerGenerated]
			public string CacheTypeName
			{
				[CompilerGenerated]
				get;
			}

			public TableTranslator(string tableName, string cacheTypeName, Func<IMasterLoadResult, System.Collections.IEnumerable> getRows, System.Collections.Generic.IReadOnlyList<FieldBinding> fields)
			{
				TableName = tableName;
				CacheTypeName = cacheTypeName;
				_getRows = getRows;
				_fields = fields;
			}

			public int Translate(IMasterLoadResult result)
			{
				System.Collections.IEnumerable enumerable = _getRows.Invoke(result);
				int num = 0;
				string text2 = default(string);
				foreach (object item in enumerable)
				{
					if (item == null)
					{
						continue;
					}
					System.Collections.Generic.IEnumerator<FieldBinding> enumerator2 = ((System.Collections.Generic.IEnumerable<FieldBinding>)_fields).GetEnumerator();
					try
					{
						while (((System.Collections.IEnumerator)enumerator2).MoveNext())
						{
							FieldBinding current2 = enumerator2.Current;
							string text = current2.Get.Invoke(item);
							if (!string.IsNullOrEmpty(text) && current2.Translations.TryGetValue(text, ref text2) && !string.IsNullOrEmpty(text2) && !string.Equals(text, text2, (StringComparison)4))
							{
								current2.Set.Invoke(item, text2);
								num++;
							}
						}
					}
					finally
					{
						((System.IDisposable)enumerator2)?.Dispose();
					}
				}
				return num;
			}
		}

		private sealed class FieldBinding
		{
			[field: CompilerGenerated]
			public Func<object, string> Get
			{
				[CompilerGenerated]
				get;
			}

			[field: CompilerGenerated]
			public Action<object, string> Set
			{
				[CompilerGenerated]
				get;
			}

			[field: CompilerGenerated]
			public IReadOnlyDictionary<string, string> Translations
			{
				[CompilerGenerated]
				get;
			}

			public FieldBinding(Func<object, string> get, Action<object, string> set, IReadOnlyDictionary<string, string> translations)
			{
				Get = get;
				Set = set;
				Translations = translations;
			}
		}

		private const string ModelNamespace = "Il2CppProject.Master.NoaMessagePack.";

		private static readonly HashSet<ValueTuple<string, string>> SealFields;

		private static readonly MethodInfo CastMethod;

		private readonly IReadOnlyDictionary<string, TableTranslator> _tables;

		public int TableCount => ((System.Collections.Generic.IReadOnlyCollection<KeyValuePair<string, TableTranslator>>)_tables).Count;

		private MasterDataTranslator(IReadOnlyDictionary<string, TableTranslator> tables)
		{
			_tables = tables;
		}

		public static MasterDataTranslator Create(IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, string>>> tables)
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			Dictionary<string, TableTranslator> val = new Dictionary<string, TableTranslator>((IEqualityComparer<string>)(object)StringComparer.Ordinal);
			System.Collections.Generic.IEnumerator<KeyValuePair<string, Dictionary<string, Dictionary<string, string>>>> enumerator = ((System.Collections.Generic.IEnumerable<KeyValuePair<string, Dictionary<string, Dictionary<string, string>>>>)tables).GetEnumerator();
			try
			{
				string text = default(string);
				Dictionary<string, Dictionary<string, string>> val2 = default(Dictionary<string, Dictionary<string, string>>);
				while (((System.Collections.IEnumerator)enumerator).MoveNext())
				{
					enumerator.Current.Deconstruct(ref text, ref val2);
					string text2 = text;
					Dictionary<string, Dictionary<string, string>> fields = val2;
					try
					{
						TableTranslator tableTranslator = CreateTableTranslator(text2, fields);
						if (tableTranslator != null)
						{
							val[tableTranslator.CacheTypeName] = tableTranslator;
						}
					}
					catch (System.Exception exception)
					{
						Logger.Warn("MasterData translation plan failed [" + text2 + "]: " + Unwrap(exception).Message);
					}
				}
			}
			finally
			{
				((System.IDisposable)enumerator)?.Dispose();
			}
			Logger.Info($"MasterData translation plan created. Tables: {val.Count}");
			return new MasterDataTranslator((IReadOnlyDictionary<string, TableTranslator>)(object)val);
		}

		public bool Contains(Type cacheType)
		{
			if (cacheType != (Type)null)
			{
				return _tables.ContainsKey(((MemberInfo)cacheType).Name);
			}
			return false;
		}

		public bool TryTranslate(Type cacheType, IMasterLoadResult result, out int translatedEntries)
		{
			translatedEntries = 0;
			TableTranslator tableTranslator = default(TableTranslator);
			if (cacheType == (Type)null || result == null || !_tables.TryGetValue(((MemberInfo)cacheType).Name, ref tableTranslator))
			{
				return false;
			}
			try
			{
				translatedEntries = tableTranslator.Translate(result);
			}
			catch (System.Exception exception)
			{
				Logger.Warn("MasterData table translation failed [" + tableTranslator.TableName + "]: " + Unwrap(exception).Message);
			}
			return true;
		}

		private static TableTranslator CreateTableTranslator(string tableName, Dictionary<string, Dictionary<string, string>> fields)
		{
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_010c: Unknown result type (might be due to invalid IL or missing references)
			System.Type type = typeof(MasterDataStore).Assembly.GetType("Il2CppProject.Master.NoaMessagePack." + ToClassName(tableName), false);
			if (type == (System.Type)null)
			{
				Logger.Warn("MasterData type not found for translation table: " + tableName);
				return null;
			}
			List<FieldBinding> val = new List<FieldBinding>();
			Enumerator<string, Dictionary<string, string>> enumerator = fields.GetEnumerator();
			try
			{
				string text = default(string);
				Dictionary<string, string> val2 = default(Dictionary<string, string>);
				while (enumerator.MoveNext())
				{
					enumerator.Current.Deconstruct(ref text, ref val2);
					string text2 = text;
					Dictionary<string, string> val3 = val2;
					if (val3 == null || val3.Count == 0)
					{
						continue;
					}
					PropertyInfo property = type.GetProperty(text2, (BindingFlags)20);
					if (!(property == (PropertyInfo)null) && !(property.PropertyType != typeof(string)))
					{
						MethodInfo getMethod = property.GetMethod;
						if (getMethod != null && ((MethodBase)getMethod).IsPublic)
						{
							MethodInfo setMethod = property.SetMethod;
							if (setMethod != null && ((MethodBase)setMethod).IsPublic)
							{
								try
								{
									val.Add(CreateFieldBinding(type, property, val3, SealFields.Contains(new ValueTuple<string, string>(tableName, text2))));
								}
								catch (System.Exception exception)
								{
									Logger.Warn($"MasterData property binding failed [{((MemberInfo)type).Name}.{text2}]: {Unwrap(exception).Message}");
								}
								continue;
							}
						}
					}
					Logger.Warn("MasterData string property not found: " + ((MemberInfo)type).Name + "." + text2);
				}
			}
			finally
			{
				((System.IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
			}
			if (val.Count != 0)
			{
				return new TableTranslator(tableName, ((MemberInfo)type).Name, CreateRowsGetter(type), (System.Collections.Generic.IReadOnlyList<FieldBinding>)val);
			}
			return null;
		}

		private static Func<IMasterLoadResult, System.Collections.IEnumerable> CreateRowsGetter(System.Type rowType)
		{
			ParameterExpression val = Expression.Parameter(typeof(IMasterLoadResult), "result");
			System.Type type = typeof(MasterLoadResult<>).MakeGenericType(new System.Type[1] { rowType });
			return Expression.Lambda<Func<IMasterLoadResult, System.Collections.IEnumerable>>((Expression)(object)Expression.Convert((Expression)(object)Expression.Property((Expression)(object)Expression.Call((Expression)(object)val, CastMethod.MakeGenericMethod(new System.Type[1] { type })), "Rows"), typeof(System.Collections.IEnumerable)), (ParameterExpression[])(object)new ParameterExpression[1] { val }).Compile();
		}

		private static FieldBinding CreateFieldBinding(System.Type rowType, PropertyInfo property, Dictionary<string, string> translations, bool normalizeSealNames)
		{
			ParameterExpression val = Expression.Parameter(typeof(object), "row");
			ParameterExpression val2 = Expression.Parameter(typeof(string), "value");
			MemberExpression val3 = Expression.Property((Expression)(object)Expression.Convert((Expression)(object)val, rowType), property);
			Func<object, string> get = Expression.Lambda<Func<object, string>>((Expression)(object)val3, (ParameterExpression[])(object)new ParameterExpression[1] { val }).Compile();
			Action<object, string> set = Expression.Lambda<Action<object, string>>((Expression)(object)Expression.Assign((Expression)(object)val3, (Expression)(object)val2), (ParameterExpression[])(object)new ParameterExpression[2] { val, val2 }).Compile();
			object translations2;
			if (!normalizeSealNames)
			{
				translations2 = translations;
			}
			else
			{
				translations2 = NormalizeTranslations(translations);
			}
			return new FieldBinding(get, set, (IReadOnlyDictionary<string, string>)translations2);
		}

		private static string ToClassName(string tableName)
		{
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Expected O, but got Unknown
			string obj = (tableName.StartsWith("m_", (StringComparison)4) ? tableName.Substring(2) : tableName);
			StringBuilder val = new StringBuilder("M");
			bool flag = true;
			string text = obj;
			foreach (char c in text)
			{
				if (c == '_')
				{
					flag = true;
					continue;
				}
				val.Append(flag ? char.ToUpperInvariant(c) : c);
				flag = false;
			}
			return ((object)val).ToString();
		}

		private static string NormalizeTranslation(string text)
		{
			return text.Replace("纹章：冲击", "紋章：衝撃", (StringComparison)4).Replace("纹章：热情", "紋章：情熱", (StringComparison)4);
		}

		private static IReadOnlyDictionary<string, string> NormalizeTranslations(Dictionary<string, string> translations)
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			Dictionary<string, string> val = new Dictionary<string, string>(translations.Count, (IEqualityComparer<string>)(object)StringComparer.Ordinal);
			Enumerator<string, string> enumerator = translations.GetEnumerator();
			try
			{
				string text = default(string);
				string text2 = default(string);
				while (enumerator.MoveNext())
				{
					enumerator.Current.Deconstruct(ref text, ref text2);
					string text3 = text;
					string text4 = text2;
					val[text3] = (string.IsNullOrEmpty(text4) ? text4 : NormalizeTranslation(text4));
				}
				return (IReadOnlyDictionary<string, string>)(object)val;
			}
			finally
			{
				((System.IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
			}
		}

		private static System.Exception Unwrap(System.Exception exception)
		{
			TargetInvocationException ex = (TargetInvocationException)(object)((exception is TargetInvocationException) ? exception : null);
			if (ex == null || exception.InnerException == null)
			{
				return exception;
			}
			return ((System.Exception)(object)ex).InnerException;
		}

		static MasterDataTranslator()
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			HashSet<ValueTuple<string, string>> obj = new HashSet<ValueTuple<string, string>>();
			obj.Add(new ValueTuple<string, string>("m_ability_details", "description"));
			obj.Add(new ValueTuple<string, string>("m_ability_details", "awake_description"));
			obj.Add(new ValueTuple<string, string>("m_character_action_skills", "description"));
			SealFields = obj;
			CastMethod = Enumerable.Single<MethodInfo>((System.Collections.Generic.IEnumerable<MethodInfo>)typeof(IMasterLoadResult).GetMethods((BindingFlags)20), (Func<MethodInfo, bool>)((MethodInfo method) => ((MemberInfo)method).Name == "Cast" && ((MethodBase)method).IsGenericMethodDefinition && ((MethodBase)method).GetParameters().Length == 0));
		}
	}
	public class TranslationCache
	{
		[CompilerGenerated]
		private sealed class <EnumerateBundleEntries>d__18 : System.Collections.Generic.IEnumerable<ValueTuple<string, string>>, System.Collections.IEnumerable, System.Collections.Generic.IEnumerator<ValueTuple<string, string>>, System.Collections.IEnumerator, System.IDisposable
		{
			private int <>1__state;

			private ValueTuple<string, string> <>2__current;

			private int <>l__initialThreadId;

			private Dictionary<string, Dictionary<string, Dictionary<string, string>>> bundle;

			public Dictionary<string, Dictionary<string, Dictionary<string, string>>> <>3__bundle;

			private System.Collections.Generic.IEnumerator<string> <>7__wrap1;

			private string <type>5__3;

			private Dictionary<string, Dictionary<string, string>> <fields>5__4;

			private System.Collections.Generic.IEnumerator<string> <>7__wrap4;

			private string <field>5__6;

			private Dictionary<string, string> <dict>5__7;

			private System.Collections.Generic.IEnumerator<string> <>7__wrap7;

			ValueTuple<string, string> System.Collections.Generic.IEnumerator<ValueTuple<string, string>>.Current
			{
				[DebuggerHidden]
				get
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					return <>2__current;
				}
			}

			object System.Collections.IEnumerator.Current
			{
				[DebuggerHidden]
				get
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					return <>2__current;
				}
			}

			[DebuggerHidden]
			public <EnumerateBundleEntries>d__18(int <>1__state)
			{
				this.<>1__state = <>1__state;
				<>l__initialThreadId = Environment.CurrentManagedThreadId;
			}

			[DebuggerHidden]
			void System.IDisposable.Dispose()
			{
				int num = <>1__state;
				if ((uint)(num - -5) <= 2u || num == 1)
				{
					try
					{
						if ((uint)(num - -5) <= 1u || num == 1)
						{
							try
							{
								if (num == -5 || num == 1)
								{
									try
									{
									}
									finally
									{
										<>m__Finally3();
									}
								}
							}
							finally
							{
								<>m__Finally2();
							}
						}
					}
					finally
					{
						<>m__Finally1();
					}
				}
				<>7__wrap1 = null;
				<type>5__3 = null;
				<fields>5__4 = null;
				<>7__wrap4 = null;
				<field>5__6 = null;
				<dict>5__7 = null;
				<>7__wrap7 = null;
				<>1__state = -2;
			}

			private bool MoveNext()
			{
				//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
				//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
				try
				{
					int num = <>1__state;
					if (num != 0)
					{
						if (num != 1)
						{
							return false;
						}
						<>1__state = -5;
						goto IL_01eb;
					}
					<>1__state = -1;
					<>7__wrap1 = ((System.Collections.Generic.IEnumerable<string>)Enumerable.OrderBy<string, string>((System.Collections.Generic.IEnumerable<string>)bundle.Keys, (Func<string, string>)((string k) => k), (IComparer<string>)(object)StringComparer.Ordinal)).GetEnumerator();
					<>1__state = -3;
					goto IL_0241;
					IL_01eb:
					if (((System.Collections.IEnumerator)<>7__wrap7).MoveNext())
					{
						string current = <>7__wrap7.Current;
						<>2__current = new ValueTuple<string, string>($"{<type>5__3}\u0001{<field>5__6}\u0001{current}", <dict>5__7[current]);
						<>1__state = 1;
						return true;
					}
					<>m__Finally3();
					<>7__wrap7 = null;
					<dict>5__7 = null;
					<field>5__6 = null;
					goto IL_0216;
					IL_0241:
					do
					{
						if (((System.Collections.IEnumerator)<>7__wrap1).MoveNext())
						{
							<type>5__3 = <>7__wrap1.Current;
							<fields>5__4 = bundle[<type>5__3];
							continue;
						}
						<>m__Finally1();
						<>7__wrap1 = null;
						return false;
					}
					while (<fields>5__4 == null);
					<>7__wrap4 = ((System.Collections.Generic.IEnumerable<string>)Enumerable.OrderBy<string, string>((System.Collections.Generic.IEnumerable<string>)<fields>5__4.Keys, (Func<string, string>)((string k) => k), (IComparer<string>)(object)StringComparer.Ordinal)).GetEnumerator();
					<>1__state = -4;
					goto IL_0216;
					IL_0216:
					while (((System.Collections.IEnumerator)<>7__wrap4).MoveNext())
					{
						<field>5__6 = <>7__wrap4.Current;
						<dict>5__7 = <fields>5__4[<field>5__6];
						if (<dict>5__7 == null)
						{
							continue;
						}
						goto IL_011d;
					}
					<>m__Finally2();
					<>7__wrap4 = null;
					<fields>5__4 = null;
					<type>5__3 = null;
					goto IL_0241;
					IL_011d:
					<>7__wrap7 = ((System.Collections.Generic.IEnumerable<string>)Enumerable.OrderBy<string, string>((System.Collections.Generic.IEnumerable<string>)<dict>5__7.Keys, (Func<string, string>)((string k) => k), (IComparer<string>)(object)StringComparer.Ordinal)).GetEnumerator();
					<>1__state = -5;
					goto IL_01eb;
				}
				catch
				{
					//try-fault
					((System.IDisposable)this).Dispose();
					throw;
				}
			}

			bool System.Collections.IEnumerator.MoveNext()
			{
				//ILSpy generated this explicit interface implementation from .override directive in MoveNext
				return this.MoveNext();
			}

			private void <>m__Finally1()
			{
				<>1__state = -1;
				if (<>7__wrap1 != null)
				{
					((System.IDisposable)<>7__wrap1).Dispose();
				}
			}

			private void <>m__Finally2()
			{
				<>1__state = -3;
				if (<>7__wrap4 != null)
				{
					((System.IDisposable)<>7__wrap4).Dispose();
				}
			}

			private void <>m__Finally3()
			{
				<>1__state = -4;
				if (<>7__wrap7 != null)
				{
					((System.IDisposable)<>7__wrap7).Dispose();
				}
			}

			[DebuggerHidden]
			void System.Collections.IEnumerator.Reset()
			{
				//IL_0000: Unknown result type (might be due to invalid IL or missing references)
				throw new NotSupportedException();
			}

			[DebuggerHidden]
			System.Collections.Generic.IEnumerator<ValueTuple<string, string>> System.Collections.Generic.IEnumerable<ValueTuple<string, string>>.GetEnumerator()
			{
				<EnumerateBundleEntries>d__18 <EnumerateBundleEntries>d__;
				if (<>1__state == -2 && <>l__initialThreadId == Environment.CurrentManagedThreadId)
				{
					<>1__state = 0;
					<EnumerateBundleEntries>d__ = this;
				}
				else
				{
					<EnumerateBundleEntries>d__ = new <EnumerateBundleEntries>d__18(0);
				}
				<EnumerateBundleEntries>d__.bundle = <>3__bundle;
				return <EnumerateBundleEntries>d__;
			}

			[DebuggerHidden]
			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return (System.Collections.IEnumerator)((System.Collections.Generic.IEnumerable<ValueTuple<string, string>>)this).GetEnumerator();
			}
		}

		[CompilerGenerated]
		private sealed class <EnumerateUiTextEntries>d__17 : System.Collections.Generic.IEnumerable<ValueTuple<string, string>>, System.Collections.IEnumerable, System.Collections.Generic.IEnumerator<ValueTuple<string, string>>, System.Collections.IEnumerator, System.IDisposable
		{
			private int <>1__state;

			private ValueTuple<string, string> <>2__current;

			private int <>l__initialThreadId;

			private Dictionary<string, Dictionary<string, string>> tables;

			public Dictionary<string, Dictionary<string, string>> <>3__tables;

			private System.Collections.Generic.IEnumerator<string> <>7__wrap1;

			private string <path>5__3;

			private Dictionary<string, string> <translations>5__4;

			private System.Collections.Generic.IEnumerator<string> <>7__wrap4;

			ValueTuple<string, string> System.Collections.Generic.IEnumerator<ValueTuple<string, string>>.Current
			{
				[DebuggerHidden]
				get
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					return <>2__current;
				}
			}

			object System.Collections.IEnumerator.Current
			{
				[DebuggerHidden]
				get
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					return <>2__current;
				}
			}

			[DebuggerHidden]
			public <EnumerateUiTextEntries>d__17(int <>1__state)
			{
				this.<>1__state = <>1__state;
				<>l__initialThreadId = Environment.CurrentManagedThreadId;
			}

			[DebuggerHidden]
			void System.IDisposable.Dispose()
			{
				int num = <>1__state;
				if ((uint)(num - -4) <= 1u || num == 1)
				{
					try
					{
						if (num == -4 || num == 1)
						{
							try
							{
							}
							finally
							{
								<>m__Finally2();
							}
						}
					}
					finally
					{
						<>m__Finally1();
					}
				}
				<>7__wrap1 = null;
				<path>5__3 = null;
				<translations>5__4 = null;
				<>7__wrap4 = null;
				<>1__state = -2;
			}

			private bool MoveNext()
			{
				//IL_0111: Unknown result type (might be due to invalid IL or missing references)
				//IL_0116: Unknown result type (might be due to invalid IL or missing references)
				try
				{
					int num = <>1__state;
					if (num != 0)
					{
						if (num != 1)
						{
							return false;
						}
						<>1__state = -4;
						goto IL_012e;
					}
					<>1__state = -1;
					<>7__wrap1 = ((System.Collections.Generic.IEnumerable<string>)Enumerable.OrderBy<string, string>((System.Collections.Generic.IEnumerable<string>)tables.Keys, (Func<string, string>)((string k) => k), (IComparer<string>)(object)StringComparer.Ordinal)).GetEnumerator();
					<>1__state = -3;
					goto IL_0156;
					IL_012e:
					if (((System.Collections.IEnumerator)<>7__wrap4).MoveNext())
					{
						string current = <>7__wrap4.Current;
						<>2__current = new ValueTuple<string, string>(<path>5__3 + "\u0001" + current, <translations>5__4[current]);
						<>1__state = 1;
						return true;
					}
					<>m__Finally2();
					<>7__wrap4 = null;
					<translations>5__4 = null;
					<path>5__3 = null;
					goto IL_0156;
					IL_0156:
					do
					{
						if (((System.Collections.IEnumerator)<>7__wrap1).MoveNext())
						{
							<path>5__3 = <>7__wrap1.Current;
							<translations>5__4 = tables[<path>5__3];
							continue;
						}
						<>m__Finally1();
						<>7__wrap1 = null;
						return false;
					}
					while (<translations>5__4 == null);
					<>7__wrap4 = ((System.Collections.Generic.IEnumerable<string>)Enumerable.OrderBy<string, string>((System.Collections.Generic.IEnumerable<string>)<translations>5__4.Keys, (Func<string, string>)((string k) => k), (IComparer<string>)(object)StringComparer.Ordinal)).GetEnumerator();
					<>1__state = -4;
					goto IL_012e;
				}
				catch
				{
					//try-fault
					((System.IDisposable)this).Dispose();
					throw;
				}
			}

			bool System.Collections.IEnumerator.MoveNext()
			{
				//ILSpy generated this explicit interface implementation from .override directive in MoveNext
				return this.MoveNext();
			}

			private void <>m__Finally1()
			{
				<>1__state = -1;
				if (<>7__wrap1 != null)
				{
					((System.IDisposable)<>7__wrap1).Dispose();
				}
			}

			private void <>m__Finally2()
			{
				<>1__state = -3;
				if (<>7__wrap4 != null)
				{
					((System.IDisposable)<>7__wrap4).Dispose();
				}
			}

			[DebuggerHidden]
			void System.Collections.IEnumerator.Reset()
			{
				//IL_0000: Unknown result type (might be due to invalid IL or missing references)
				throw new NotSupportedException();
			}

			[DebuggerHidden]
			System.Collections.Generic.IEnumerator<ValueTuple<string, string>> System.Collections.Generic.IEnumerable<ValueTuple<string, string>>.GetEnumerator()
			{
				<EnumerateUiTextEntries>d__17 <EnumerateUiTextEntries>d__;
				if (<>1__state == -2 && <>l__initialThreadId == Environment.CurrentManagedThreadId)
				{
					<>1__state = 0;
					<EnumerateUiTextEntries>d__ = this;
				}
				else
				{
					<EnumerateUiTextEntries>d__ = new <EnumerateUiTextEntries>d__17(0);
				}
				<EnumerateUiTextEntries>d__.tables = <>3__tables;
				return <EnumerateUiTextEntries>d__;
			}

			[DebuggerHidden]
			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return (System.Collections.IEnumerator)((System.Collections.Generic.IEnumerable<ValueTuple<string, string>>)this).GetEnumerator();
			}
		}

		[StructLayout((LayoutKind)3)]
		[CompilerGenerated]
		private struct <FetchManifestAsync>d__7 : IAsyncStateMachine
		{
			public int <>1__state;

			public AsyncTaskMethodBuilder <>t__builder;

			public TranslationCache <>4__this;

			public CancellationToken cancellationToken;

			private string <relativePath>5__2;

			private string <url>5__3;

			private string <path>5__4;

			private Manifest <previous>5__5;

			private ConfiguredTaskAwaiter<Manifest?> <>u__1;

			private void MoveNext()
			{
				//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
				//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
				//IL_0104: Unknown result type (might be due to invalid IL or missing references)
				//IL_0109: Unknown result type (might be due to invalid IL or missing references)
				//IL_010d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0112: Unknown result type (might be due to invalid IL or missing references)
				//IL_0140: Unknown result type (might be due to invalid IL or missing references)
				//IL_0145: Unknown result type (might be due to invalid IL or missing references)
				//IL_014c: Unknown result type (might be due to invalid IL or missing references)
				//IL_006d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0078: Unknown result type (might be due to invalid IL or missing references)
				//IL_007d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0081: Unknown result type (might be due to invalid IL or missing references)
				//IL_0086: Unknown result type (might be due to invalid IL or missing references)
				//IL_0126: Unknown result type (might be due to invalid IL or missing references)
				//IL_0127: Unknown result type (might be due to invalid IL or missing references)
				//IL_009a: Unknown result type (might be due to invalid IL or missing references)
				//IL_009b: Unknown result type (might be due to invalid IL or missing references)
				int num = <>1__state;
				TranslationCache translationCache = <>4__this;
				try
				{
					ConfiguredTaskAwaiter<Manifest> val;
					if (num != 0)
					{
						if (num == 1)
						{
							val = <>u__1;
							<>u__1 = default(ConfiguredTaskAwaiter<Manifest>);
							num = (<>1__state = -1);
							goto IL_015b;
						}
						<relativePath>5__2 = TranslationPaths.BuildRelativePath("manifest", translationCache._language);
						<url>5__3 = TranslationPaths.BuildRemoteUrl(translationCache._cdn, <relativePath>5__2);
						<path>5__4 = TranslationPaths.BuildCachePath(translationCache._cacheDir, <relativePath>5__2);
						val = translationCache._resources.LoadLocalAsync<Manifest>(<path>5__4, cancellationToken).ConfigureAwait(false).GetAwaiter();
						if (!val.IsCompleted)
						{
							num = (<>1__state = 0);
							<>u__1 = val;
							((AsyncTaskMethodBuilder)(ref <>t__builder)).AwaitUnsafeOnCompleted<ConfiguredTaskAwaiter<Manifest>, <FetchManifestAsync>d__7>(ref val, ref this);
							return;
						}
					}
					else
					{
						val = <>u__1;
						<>u__1 = default(ConfiguredTaskAwaiter<Manifest>);
						num = (<>1__state = -1);
					}
					Manifest result = val.GetResult();
					<previous>5__5 = result;
					val = translationCache._resources.RefreshAsync<Manifest>(<relativePath>5__2, <path>5__4, <url>5__3, JsonCachePolicy.Refresh, null, cancellationToken).ConfigureAwait(false).GetAwaiter();
					if (!val.IsCompleted)
					{
						num = (<>1__state = 1);
						<>u__1 = val;
						((AsyncTaskMethodBuilder)(ref <>t__builder)).AwaitUnsafeOnCompleted<ConfiguredTaskAwaiter<Manifest>, <FetchManifestAsync>d__7>(ref val, ref this);
						return;
					}
					goto IL_015b;
					IL_015b:
					result = val.GetResult();
					translationCache._manifest = result;
					if (translationCache._manifest != null)
					{
						if (!string.IsNullOrEmpty(<previous>5__5?.Hash) && <previous>5__5.Hash != translationCache._manifest.Hash)
						{
							Logger.Info("[翻译更新] CDN 有新版本翻译内容");
						}
						Logger.Info("Manifest loaded (" + translationCache._language + "). Hash: " + translationCache._manifest.Hash);
					}
					else
					{
						Logger.Warn("No manifest available, will fetch without hash verification.");
					}
				}
				catch (System.Exception exception)
				{
					<>1__state = -2;
					<relativePath>5__2 = null;
					<url>5__3 = null;
					<path>5__4 = null;
					<previous>5__5 = null;
					((AsyncTaskMethodBuilder)(ref <>t__builder)).SetException(exception);
					return;
				}
				<>1__state = -2;
				<relativePath>5__2 = null;
				<url>5__3 = null;
				<path>5__4 = null;
				<previous>5__5 = null;
				((AsyncTaskMethodBuilder)(ref <>t__builder)).SetResult();
			}

			[DebuggerHidden]
			private void SetStateMachine(IAsyncStateMachine stateMachine)
			{
				((AsyncTaskMethodBuilder)(ref <>t__builder)).SetStateMachine(stateMachine);
			}
		}

		private readonly string _cdn;

		private readonly string _cacheDir;

		private readonly string _language;

		private readonly bool _preferLocalFiles;

		private Manifest _manifest;

		private readonly JsonResourceCache _resources;

		public TranslationCache(string cdn, string cacheDir, string language, bool preferLocalFiles, HttpClient client)
		{
			_cdn = cdn.TrimEnd('/');
			_cacheDir = cacheDir;
			_language = language;
			_preferLocalFiles = preferLocalFiles;
			_resources = new JsonResourceCache(client, delegate(string message)
			{
				Logger.Info(message);
			}, delegate(string message)
			{
				Logger.Warn(message);
			});
			Directory.CreateDirectory(_cacheDir);
		}

		[AsyncStateMachine(typeof(<FetchManifestAsync>d__7))]
		public System.Threading.Tasks.Task FetchManifestAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			<FetchManifestAsync>d__7 <FetchManifestAsync>d__ = default(<FetchManifestAsync>d__7);
			<FetchManifestAsync>d__.<>t__builder = AsyncTaskMethodBuilder.Create();
			<FetchManifestAsync>d__.<>4__this = this;
			<FetchManifestAsync>d__.cancellationToken = cancellationToken;
			<FetchManifestAsync>d__.<>1__state = -1;
			((AsyncTaskMethodBuilder)(ref <FetchManifestAsync>d__.<>t__builder)).Start<<FetchManifestAsync>d__7>(ref <FetchManifestAsync>d__);
			return ((AsyncTaskMethodBuilder)(ref <FetchManifestAsync>d__.<>t__builder)).Task;
		}

		public System.Threading.Tasks.Task<Dictionary<string, string>> LoadAsync(string type, string id = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			return LoadWithCacheAsync<Dictionary<string, string>>(TranslationPaths.BuildRelativePath(type, _language, id), GetManifestHash(type, id), GetHash, fetchWhenUnlisted: false, cancellationToken);
		}

		public System.Threading.Tasks.Task<Dictionary<string, Dictionary<string, string>>> LoadUiTextsAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			string type = "ui_texts";
			return LoadWithCacheAsync<Dictionary<string, Dictionary<string, string>>>(TranslationPaths.BuildRelativePath(type, _language), GetManifestHash(type, null), GetUiTextHash, fetchWhenUnlisted: false, cancellationToken);
		}

		public System.Threading.Tasks.Task<Dictionary<string, Dictionary<string, Dictionary<string, string>>>> LoadStaticBundleAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			string type = "static";
			return LoadWithCacheAsync<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(TranslationPaths.BuildRelativePath(type, _language), GetManifestHash(type, null), GetBundleHash, fetchWhenUnlisted: true, cancellationToken);
		}

		private System.Threading.Tasks.Task<T> LoadWithCacheAsync<T>(string relativePath, string expectedHash, Func<T, string> computeHash, bool fetchWhenUnlisted = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			string url = TranslationPaths.BuildRemoteUrl(_cdn, relativePath);
			string text = TranslationPaths.BuildCachePath(_cacheDir, relativePath);
			JsonCachePolicy policy = (_preferLocalFiles ? JsonCachePolicy.PreferLocal : JsonCachePolicy.Refresh);
			if (_manifest != null && expectedHash == null && (!fetchWhenUnlisted || File.Exists(text)))
			{
				policy = JsonCachePolicy.LocalOnly;
			}
			return _resources.RefreshAsync<T>(relativePath, text, url, policy, (expectedHash == null) ? null : ((Func<T, bool>)((T value) => string.Equals(computeHash.Invoke(value), expectedHash, (StringComparison)5))), cancellationToken);
		}

		private string GetManifestHash(string type, string id)
		{
			if (_manifest == null)
			{
				return null;
			}
			if (type == "novels" && id != null)
			{
				Dictionary<string, string> novels = _manifest.Novels;
				string result = default(string);
				if (novels == null || !novels.TryGetValue(id, ref result))
				{
					return null;
				}
				return result;
			}
			return _manifest.GetFileHash(type);
		}

		internal bool IsMissingFromManifest(string type, string id = null)
		{
			if (_manifest != null)
			{
				return GetManifestHash(type, id) == null;
			}
			return false;
		}

		private static string GetHash(Dictionary<string, string> dict)
		{
			if (dict == null)
			{
				return null;
			}
			return ComputeMd5Hex(Enumerable.Select<string, ValueTuple<string, string>>((System.Collections.Generic.IEnumerable<string>)Enumerable.OrderBy<string, string>((System.Collections.Generic.IEnumerable<string>)dict.Keys, (Func<string, string>)((string k) => k), (IComparer<string>)(object)StringComparer.Ordinal), (Func<string, ValueTuple<string, string>>)((string k) => new ValueTuple<string, string>(k, dict[k]))));
		}

		private static string GetBundleHash(Dictionary<string, Dictionary<string, Dictionary<string, string>>> bundle)
		{
			if (bundle == null)
			{
				return null;
			}
			return ComputeMd5Hex(EnumerateBundleEntries(bundle));
		}

		private static string GetUiTextHash(Dictionary<string, Dictionary<string, string>> tables)
		{
			if (tables != null)
			{
				return ComputeMd5Hex(EnumerateUiTextEntries(tables));
			}
			return null;
		}

		[IteratorStateMachine(typeof(<EnumerateUiTextEntries>d__17))]
		private static System.Collections.Generic.IEnumerable<ValueTuple<string, string>> EnumerateUiTextEntries(Dictionary<string, Dictionary<string, string>> tables)
		{
			System.Collections.Generic.IEnumerator<string> enumerator = ((System.Collections.Generic.IEnumerable<string>)Enumerable.OrderBy<string, string>((System.Collections.Generic.IEnumerable<string>)tables.Keys, (Func<string, string>)((string k) => k), (IComparer<string>)(object)StringComparer.Ordinal)).GetEnumerator();
			try
			{
				while (((System.Collections.IEnumerator)enumerator).MoveNext())
				{
					string path = enumerator.Current;
					Dictionary<string, string> translations = tables[path];
					if (translations == null)
					{
						continue;
					}
					System.Collections.Generic.IEnumerator<string> enumerator2 = ((System.Collections.Generic.IEnumerable<string>)Enumerable.OrderBy<string, string>((System.Collections.Generic.IEnumerable<string>)translations.Keys, (Func<string, string>)((string k) => k), (IComparer<string>)(object)StringComparer.Ordinal)).GetEnumerator();
					try
					{
						while (((System.Collections.IEnumerator)enumerator2).MoveNext())
						{
							string current = enumerator2.Current;
							yield return new ValueTuple<string, string>(path + "\u0001" + current, translations[current]);
						}
					}
					finally
					{
						((System.IDisposable)enumerator2)?.Dispose();
					}
				}
			}
			finally
			{
				((System.IDisposable)enumerator)?.Dispose();
			}
		}

		[IteratorStateMachine(typeof(<EnumerateBundleEntries>d__18))]
		private static System.Collections.Generic.IEnumerable<ValueTuple<string, string>> EnumerateBundleEntries(Dictionary<string, Dictionary<string, Dictionary<string, string>>> bundle)
		{
			System.Collections.Generic.IEnumerator<string> enumerator = ((System.Collections.Generic.IEnumerable<string>)Enumerable.OrderBy<string, string>((System.Collections.Generic.IEnumerable<string>)bundle.Keys, (Func<string, string>)((string k) => k), (IComparer<string>)(object)StringComparer.Ordinal)).GetEnumerator();
			try
			{
				while (((System.Collections.IEnumerator)enumerator).MoveNext())
				{
					string type = enumerator.Current;
					Dictionary<string, Dictionary<string, string>> fields = bundle[type];
					if (fields == null)
					{
						continue;
					}
					System.Collections.Generic.IEnumerator<string> enumerator2 = ((System.Collections.Generic.IEnumerable<string>)Enumerable.OrderBy<string, string>((System.Collections.Generic.IEnumerable<string>)fields.Keys, (Func<string, string>)((string k) => k), (IComparer<string>)(object)StringComparer.Ordinal)).GetEnumerator();
					try
					{
						while (((System.Collections.IEnumerator)enumerator2).MoveNext())
						{
							string field = enumerator2.Current;
							Dictionary<string, string> dict = fields[field];
							if (dict == null)
							{
								continue;
							}
							System.Collections.Generic.IEnumerator<string> enumerator3 = ((System.Collections.Generic.IEnumerable<string>)Enumerable.OrderBy<string, string>((System.Collections.Generic.IEnumerable<string>)dict.Keys, (Func<string, string>)((string k) => k), (IComparer<string>)(object)StringComparer.Ordinal)).GetEnumerator();
							try
							{
								while (((System.Collections.IEnumerator)enumerator3).MoveNext())
								{
									string current = enumerator3.Current;
									yield return new ValueTuple<string, string>($"{type}\u0001{field}\u0001{current}", dict[current]);
								}
							}
							finally
							{
								((System.IDisposable)enumerator3)?.Dispose();
							}
						}
					}
					finally
					{
						((System.IDisposable)enumerator2)?.Dispose();
					}
				}
			}
			finally
			{
				((System.IDisposable)enumerator)?.Dispose();
			}
		}

		private static string ComputeMd5Hex(System.Collections.Generic.IEnumerable<ValueTuple<string, string>> entries)
		{
			return StringTableHash.ComputeEntries(entries);
		}
	}
	internal sealed class TranslationCryptoHandler : DelegatingHandler
	{
		[StructLayout((LayoutKind)3)]
		[CompilerGenerated]
		private struct <SendAsync>d__3 : IAsyncStateMachine
		{
			public int <>1__state;

			public AsyncTaskMethodBuilder<HttpResponseMessage> <>t__builder;

			public TranslationCryptoHandler <>4__this;

			public HttpRequestMessage request;

			public CancellationToken cancellationToken;

			private HttpResponseMessage <response>5__2;

			private TaskAwaiter<HttpResponseMessage> <>u__1;

			private HttpContent <originalContent>5__3;

			private TaskAwaiter<byte[]> <>u__2;

			private void MoveNext()
			{
				//IL_005c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0061: Unknown result type (might be due to invalid IL or missing references)
				//IL_0069: Unknown result type (might be due to invalid IL or missing references)
				//IL_001d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				//IL_002c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0041: Unknown result type (might be due to invalid IL or missing references)
				//IL_0043: Unknown result type (might be due to invalid IL or missing references)
				//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
				//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
				//IL_0106: Unknown result type (might be due to invalid IL or missing references)
				//IL_012a: Unknown result type (might be due to invalid IL or missing references)
				//IL_0131: Expected O, but got Unknown
				//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
				//IL_00de: Unknown result type (might be due to invalid IL or missing references)
				//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
				//IL_014d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0152: Unknown result type (might be due to invalid IL or missing references)
				int num = <>1__state;
				TranslationCryptoHandler translationCryptoHandler = <>4__this;
				HttpResponseMessage result3;
				try
				{
					TaskAwaiter<HttpResponseMessage> val;
					if (num != 0)
					{
						if (num == 1)
						{
							goto IL_0088;
						}
						val = translationCryptoHandler.<>n__0(request, cancellationToken).GetAwaiter();
						if (!val.IsCompleted)
						{
							num = (<>1__state = 0);
							<>u__1 = val;
							<>t__builder.AwaitUnsafeOnCompleted<TaskAwaiter<HttpResponseMessage>, <SendAsync>d__3>(ref val, ref this);
							return;
						}
					}
					else
					{
						val = <>u__1;
						<>u__1 = default(TaskAwaiter<HttpResponseMessage>);
						num = (<>1__state = -1);
					}
					HttpResponseMessage result = val.GetResult();
					<response>5__2 = result;
					goto IL_0088;
					IL_0088:
					try
					{
						TaskAwaiter<byte[]> val2;
						if (num != 1)
						{
							if (<response>5__2.Content == null)
							{
								goto IL_01c5;
							}
							<originalContent>5__3 = <response>5__2.Content;
							val2 = HttpContentReader.ReadAsync(<originalContent>5__3, 67108864L, cancellationToken).GetAwaiter();
							if (!val2.IsCompleted)
							{
								num = (<>1__state = 1);
								<>u__2 = val2;
								<>t__builder.AwaitUnsafeOnCompleted<TaskAwaiter<byte[]>, <SendAsync>d__3>(ref val2, ref this);
								return;
							}
						}
						else
						{
							val2 = <>u__2;
							<>u__2 = default(TaskAwaiter<byte[]>);
							num = (<>1__state = -1);
						}
						byte[] result2 = val2.GetResult();
						byte[] array = translationCryptoHandler.Decrypt(result2);
						ByteArrayContent val3 = new ByteArrayContent(array);
						if (result2 == array)
						{
							System.Collections.Generic.IEnumerator<KeyValuePair<string, System.Collections.Generic.IEnumerable<string>>> enumerator = ((HttpHeaders)<originalContent>5__3.Headers).GetEnumerator();
							try
							{
								while (((System.Collections.IEnumerator)enumerator).MoveNext())
								{
									KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> current = enumerator.Current;
									((HttpHeaders)((HttpContent)val3).Headers).TryAddWithoutValidation(current.Key, current.Value);
								}
							}
							finally
							{
								if (num < 0)
								{
									((System.IDisposable)enumerator)?.Dispose();
								}
							}
						}
						else
						{
							((HttpContent)val3).Headers.ContentType = <originalContent>5__3.Headers.ContentType;
						}
						<response>5__2.Content = (HttpContent)(object)val3;
						<originalContent>5__3.Dispose();
						<originalContent>5__3 = null;
						goto IL_01c5;
						IL_01c5:
						result3 = <response>5__2;
					}
					catch
					{
						<response>5__2.Dispose();
						throw;
					}
				}
				catch (System.Exception exception)
				{
					<>1__state = -2;
					<response>5__2 = null;
					<>t__builder.SetException(exception);
					return;
				}
				<>1__state = -2;
				<response>5__2 = null;
				<>t__builder.SetResult(result3);
			}

			[DebuggerHidden]
			private void SetStateMachine(IAsyncStateMachine stateMachine)
			{
				<>t__builder.SetStateMachine(stateMachine);
			}
		}

		private const long MaxResponseBytes = 67108864L;

		private readonly Func<ValueTuple<string, string>> _getSettings;

		public TranslationCryptoHandler(HttpMessageHandler innerHandler, Func<ValueTuple<string, string>> getSettings)
			: base(innerHandler)
		{
			_getSettings = getSettings;
		}

		[AsyncStateMachine(typeof(<SendAsync>d__3))]
		protected override async System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			HttpResponseMessage response = await <>n__0(request, cancellationToken);
			try
			{
				if (response.Content != null)
				{
					HttpContent originalContent = response.Content;
					byte[] array = await HttpContentReader.ReadAsync(originalContent, 67108864L, cancellationToken);
					byte[] array2 = Decrypt(array);
					ByteArrayContent val = new ByteArrayContent(array2);
					if (array == array2)
					{
						System.Collections.Generic.IEnumerator<KeyValuePair<string, System.Collections.Generic.IEnumerable<string>>> enumerator = ((HttpHeaders)originalContent.Headers).GetEnumerator();
						try
						{
							while (((System.Collections.IEnumerator)enumerator).MoveNext())
							{
								KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> current = enumerator.Current;
								((HttpHeaders)((HttpContent)val).Headers).TryAddWithoutValidation(current.Key, current.Value);
							}
						}
						finally
						{
							((System.IDisposable)enumerator)?.Dispose();
						}
					}
					else
					{
						((HttpContent)val).Headers.ContentType = originalContent.Headers.ContentType;
					}
					response.Content = (HttpContent)(object)val;
					originalContent.Dispose();
				}
				return response;
			}
			catch
			{
				response.Dispose();
				throw;
			}
		}

		private byte[] Decrypt(byte[] data)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			ValueTuple<string, string> val = _getSettings.Invoke();
			string item = val.Item1;
			string item2 = val.Item2;
			if (string.IsNullOrEmpty(item))
			{
				return data;
			}
			byte[] bytes = Encoding.UTF8.GetBytes(item);
			if (data.Length < bytes.Length || !MemoryExtensions.SequenceEqual<byte>(System.Span<byte>.op_Implicit(MemoryExtensions.AsSpan<byte>(data, 0, bytes.Length)), System.ReadOnlySpan<byte>.op_Implicit(bytes)))
			{
				return data;
			}
			if (string.IsNullOrEmpty(item2))
			{
				throw new InvalidOperationException("Translation crypto key cannot be empty");
			}
			byte[] array = Convert.FromBase64String(Encoding.UTF8.GetString(data, bytes.Length, data.Length - bytes.Length));
			byte[] bytes2 = Encoding.UTF8.GetBytes(item2);
			for (int i = 0; i < array.Length; i++)
			{
				array[i] ^= bytes2[i % bytes2.Length];
			}
			return array;
		}

		[CompilerGenerated]
		[DebuggerHidden]
		private System.Threading.Tasks.Task<HttpResponseMessage> <>n__0(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			return ((DelegatingHandler)this).SendAsync(request, cancellationToken);
		}
	}
	public class TranslationManager
	{
		[StructLayout((LayoutKind)3)]
		[CompilerGenerated]
		private struct <EnsureNovelTranslationLoadedAsync>d__30 : IAsyncStateMachine
		{
			public int <>1__state;

			public AsyncTaskMethodBuilder <>t__builder;

			public TranslationManager <>4__this;

			public string novelId;

			private ConfiguredTaskAwaiter <>u__1;

			private void MoveNext()
			{
				//IL_005a: Unknown result type (might be due to invalid IL or missing references)
				//IL_005f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0066: Unknown result type (might be due to invalid IL or missing references)
				//IL_00de: Unknown result type (might be due to invalid IL or missing references)
				//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
				//IL_001f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0024: Unknown result type (might be due to invalid IL or missing references)
				//IL_0027: Unknown result type (might be due to invalid IL or missing references)
				//IL_002c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0040: Unknown result type (might be due to invalid IL or missing references)
				//IL_0041: Unknown result type (might be due to invalid IL or missing references)
				//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
				//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
				int num = <>1__state;
				TranslationManager translationManager = <>4__this;
				try
				{
					ConfiguredTaskAwaiter val;
					ConfiguredTaskAwaitable val2;
					if (num != 0)
					{
						if (num == 1)
						{
							val = <>u__1;
							<>u__1 = default(ConfiguredTaskAwaiter);
							num = (<>1__state = -1);
							goto IL_00f9;
						}
						val2 = translationManager.EnsureStaticTranslationsLoadedAsync().ConfigureAwait(false);
						val = ((ConfiguredTaskAwaitable)(ref val2)).GetAwaiter();
						if (!((ConfiguredTaskAwaiter)(ref val)).IsCompleted)
						{
							num = (<>1__state = 0);
							<>u__1 = val;
							((AsyncTaskMethodBuilder)(ref <>t__builder)).AwaitUnsafeOnCompleted<ConfiguredTaskAwaiter, <EnsureNovelTranslationLoadedAsync>d__30>(ref val, ref this);
							return;
						}
					}
					else
					{
						val = <>u__1;
						<>u__1 = default(ConfiguredTaskAwaiter);
						num = (<>1__state = -1);
					}
					((ConfiguredTaskAwaiter)(ref val)).GetResult();
					if (!translationManager._shutdown && !translationManager._novels.ContainsKey(novelId))
					{
						val2 = translationManager.GetNovelTranslationAsync(novelId).ConfigureAwait(false);
						val = ((ConfiguredTaskAwaitable)(ref val2)).GetAwaiter();
						if (!((ConfiguredTaskAwaiter)(ref val)).IsCompleted)
						{
							num = (<>1__state = 1);
							<>u__1 = val;
							((AsyncTaskMethodBuilder)(ref <>t__builder)).AwaitUnsafeOnCompleted<ConfiguredTaskAwaiter, <EnsureNovelTranslationLoadedAsync>d__30>(ref val, ref this);
							return;
						}
						goto IL_00f9;
					}
					goto end_IL_000e;
					IL_00f9:
					((ConfiguredTaskAwaiter)(ref val)).GetResult();
					end_IL_000e:;
				}
				catch (System.Exception exception)
				{
					<>1__state = -2;
					((AsyncTaskMethodBuilder)(ref <>t__builder)).SetException(exception);
					return;
				}
				<>1__state = -2;
				((AsyncTaskMethodBuilder)(ref <>t__builder)).SetResult();
			}

			[DebuggerHidden]
			private void SetStateMachine(IAsyncStateMachine stateMachine)
			{
				((AsyncTaskMethodBuilder)(ref <>t__builder)).SetStateMachine(stateMachine);
			}
		}

		[StructLayout((LayoutKind)3)]
		[CompilerGenerated]
		private struct <GetNovelTranslationAsync>d__29 : IAsyncStateMachine
		{
			public int <>1__state;

			public AsyncTaskMethodBuilder <>t__builder;

			public TranslationManager <>4__this;

			public string novelId;

			private TaskCompletionSource <tcs>5__2;

			private ConfiguredTaskAwaiter <>u__1;

			private object <>7__wrap2;

			private int <>7__wrap3;

			private ConfiguredTaskAwaiter<Dictionary<string, string>> <>u__2;

			private void MoveNext()
			{
				//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
				//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
				//IL_02d6: Unknown result type (might be due to invalid IL or missing references)
				//IL_02db: Unknown result type (might be due to invalid IL or missing references)
				//IL_02df: Unknown result type (might be due to invalid IL or missing references)
				//IL_02e4: Unknown result type (might be due to invalid IL or missing references)
				//IL_02f8: Unknown result type (might be due to invalid IL or missing references)
				//IL_02f9: Unknown result type (might be due to invalid IL or missing references)
				//IL_0042: Unknown result type (might be due to invalid IL or missing references)
				//IL_004c: Expected O, but got Unknown
				//IL_030f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0314: Unknown result type (might be due to invalid IL or missing references)
				//IL_031b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0079: Unknown result type (might be due to invalid IL or missing references)
				//IL_007e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0082: Unknown result type (might be due to invalid IL or missing references)
				//IL_0087: Unknown result type (might be due to invalid IL or missing references)
				//IL_014f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0154: Unknown result type (might be due to invalid IL or missing references)
				//IL_015c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0106: Unknown result type (might be due to invalid IL or missing references)
				//IL_0111: Unknown result type (might be due to invalid IL or missing references)
				//IL_0116: Unknown result type (might be due to invalid IL or missing references)
				//IL_011a: Unknown result type (might be due to invalid IL or missing references)
				//IL_011f: Unknown result type (might be due to invalid IL or missing references)
				//IL_009b: Unknown result type (might be due to invalid IL or missing references)
				//IL_009c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0134: Unknown result type (might be due to invalid IL or missing references)
				//IL_0136: Unknown result type (might be due to invalid IL or missing references)
				int num = <>1__state;
				TranslationManager translationManager = <>4__this;
				try
				{
					ConfiguredTaskAwaiter val;
					if (num == 0)
					{
						val = <>u__1;
						<>u__1 = default(ConfiguredTaskAwaiter);
						num = (<>1__state = -1);
						goto IL_00d0;
					}
					if ((uint)(num - 1) <= 1u)
					{
						goto IL_00dd;
					}
					ConfiguredTaskAwaitable val2;
					if (!translationManager._shutdown && !translationManager._novels.ContainsKey(novelId))
					{
						<tcs>5__2 = new TaskCompletionSource((TaskCreationOptions)64);
						System.Threading.Tasks.Task orAdd = translationManager._loadingNovels.GetOrAdd(novelId, <tcs>5__2.Task);
						if (orAdd != <tcs>5__2.Task)
						{
							val2 = orAdd.ConfigureAwait(false);
							val = ((ConfiguredTaskAwaitable)(ref val2)).GetAwaiter();
							if (!((ConfiguredTaskAwaiter)(ref val)).IsCompleted)
							{
								num = (<>1__state = 0);
								<>u__1 = val;
								((AsyncTaskMethodBuilder)(ref <>t__builder)).AwaitUnsafeOnCompleted<ConfiguredTaskAwaiter, <GetNovelTranslationAsync>d__29>(ref val, ref this);
								return;
							}
							goto IL_00d0;
						}
						goto IL_00dd;
					}
					goto end_IL_000e;
					IL_00dd:
					try
					{
						if (num != 1)
						{
							if (num == 2)
							{
								val = <>u__1;
								<>u__1 = default(ConfiguredTaskAwaiter);
								num = (<>1__state = -1);
								goto IL_032a;
							}
							<>7__wrap3 = 0;
						}
						try
						{
							ConfiguredTaskAwaiter<Dictionary<string, string>> val3;
							if (num != 1)
							{
								val3 = translationManager._cache.LoadAsync("novels", novelId, translationManager._token).ConfigureAwait(false).GetAwaiter();
								if (!val3.IsCompleted)
								{
									num = (<>1__state = 1);
									<>u__2 = val3;
									((AsyncTaskMethodBuilder)(ref <>t__builder)).AwaitUnsafeOnCompleted<ConfiguredTaskAwaiter<Dictionary<string, string>>, <GetNovelTranslationAsync>d__29>(ref val3, ref this);
									return;
								}
							}
							else
							{
								val3 = <>u__2;
								<>u__2 = default(ConfiguredTaskAwaiter<Dictionary<string, string>>);
								num = (<>1__state = -1);
							}
							Dictionary<string, string> result = val3.GetResult();
							object loadLock = translationManager._loadLock;
							bool flag = false;
							try
							{
								Monitor.Enter(loadLock, ref flag);
								if (!translationManager._shutdown)
								{
									if (result != null)
									{
										translationManager._novels[novelId] = result;
										Logger.Info($"Scenario translation loaded. Total: {result.Count}");
									}
									else if (translationManager._cache.IsMissingFromManifest("novels", novelId))
									{
										Logger.Info("Scenario is not translated yet: " + novelId);
									}
									else
									{
										Logger.Warn("Translations loaded failed: " + novelId);
										Toast.Warning("加载失败", "剧本ID: " + novelId);
									}
								}
							}
							finally
							{
								if (num < 0 && flag)
								{
									Monitor.Exit(loadLock);
								}
							}
							<tcs>5__2.SetResult();
						}
						catch (object obj) when (obj is OperationCanceledException && ((CancellationToken)(ref translationManager._token)).IsCancellationRequested)
						{
							<tcs>5__2.TrySetResult();
						}
						catch (System.Exception ex)
						{
							<>7__wrap2 = ex;
							<>7__wrap3 = 1;
						}
						int num2 = <>7__wrap3;
						if (num2 == 1)
						{
							System.Exception exception = (System.Exception)<>7__wrap2;
							<tcs>5__2.SetException(exception);
							val2 = <tcs>5__2.Task.ConfigureAwait(false);
							val = ((ConfiguredTaskAwaitable)(ref val2)).GetAwaiter();
							if (!((ConfiguredTaskAwaiter)(ref val)).IsCompleted)
							{
								num = (<>1__state = 2);
								<>u__1 = val;
								((AsyncTaskMethodBuilder)(ref <>t__builder)).AwaitUnsafeOnCompleted<ConfiguredTaskAwaiter, <GetNovelTranslationAsync>d__29>(ref val, ref this);
								return;
							}
							goto IL_032a;
						}
						goto IL_0331;
						IL_032a:
						((ConfiguredTaskAwaiter)(ref val)).GetResult();
						goto IL_0331;
						IL_0331:
						<>7__wrap2 = null;
					}
					finally
					{
						if (num < 0)
						{
							System.Threading.Tasks.Task task = default(System.Threading.Tasks.Task);
							translationManager._loadingNovels.TryRemove(novelId, ref task);
						}
					}
					goto end_IL_000e;
					IL_00d0:
					((ConfiguredTaskAwaiter)(ref val)).GetResult();
					end_IL_000e:;
				}
				catch (System.Exception exception2)
				{
					<>1__state = -2;
					((AsyncTaskMethodBuilder)(ref <>t__builder)).SetException(exception2);
					return;
				}
				<>1__state = -2;
				((AsyncTaskMethodBuilder)(ref <>t__builder)).SetResult();
			}

			[DebuggerHidden]
			private void SetStateMachine(IAsyncStateMachine stateMachine)
			{
				((AsyncTaskMethodBuilder)(ref <>t__builder)).SetStateMachine(stateMachine);
			}
		}

		[StructLayout((LayoutKind)3)]
		[CompilerGenerated]
		private struct <LoadTranslationAsync>d__16 : IAsyncStateMachine
		{
			public int <>1__state;

			public AsyncTaskMethodBuilder <>t__builder;

			public TranslationManager <>4__this;

			private System.Threading.Tasks.Task<Dictionary<string, Dictionary<string, Dictionary<string, string>>>> <bundleTask>5__2;

			private System.Threading.Tasks.Task<Dictionary<string, string>> <namesTask>5__3;

			private System.Threading.Tasks.Task<Dictionary<string, Dictionary<string, string>>> <uiTask>5__4;

			private ConfiguredTaskAwaiter <>u__1;

			private void MoveNext()
			{
				//IL_006b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0070: Unknown result type (might be due to invalid IL or missing references)
				//IL_0077: Unknown result type (might be due to invalid IL or missing references)
				//IL_0199: Unknown result type (might be due to invalid IL or missing references)
				//IL_019e: Unknown result type (might be due to invalid IL or missing references)
				//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
				//IL_0024: Unknown result type (might be due to invalid IL or missing references)
				//IL_002f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0034: Unknown result type (might be due to invalid IL or missing references)
				//IL_0038: Unknown result type (might be due to invalid IL or missing references)
				//IL_003d: Unknown result type (might be due to invalid IL or missing references)
				//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
				//IL_0051: Unknown result type (might be due to invalid IL or missing references)
				//IL_0052: Unknown result type (might be due to invalid IL or missing references)
				//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
				//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
				//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
				//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
				//IL_02ca: Unknown result type (might be due to invalid IL or missing references)
				//IL_02cf: Unknown result type (might be due to invalid IL or missing references)
				//IL_020f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0214: Unknown result type (might be due to invalid IL or missing references)
				//IL_0101: Unknown result type (might be due to invalid IL or missing references)
				//IL_033a: Unknown result type (might be due to invalid IL or missing references)
				//IL_033f: Unknown result type (might be due to invalid IL or missing references)
				//IL_021a: Unknown result type (might be due to invalid IL or missing references)
				//IL_021f: Unknown result type (might be due to invalid IL or missing references)
				//IL_015d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0162: Unknown result type (might be due to invalid IL or missing references)
				//IL_0166: Unknown result type (might be due to invalid IL or missing references)
				//IL_016b: Unknown result type (might be due to invalid IL or missing references)
				//IL_017f: Unknown result type (might be due to invalid IL or missing references)
				//IL_0180: Unknown result type (might be due to invalid IL or missing references)
				int num = <>1__state;
				TranslationManager translationManager = <>4__this;
				try
				{
					_ = 1;
					try
					{
						ConfiguredTaskAwaiter val;
						ConfiguredTaskAwaitable val2;
						if (num != 0)
						{
							if (num == 1)
							{
								val = <>u__1;
								<>u__1 = default(ConfiguredTaskAwaiter);
								num = (<>1__state = -1);
								goto IL_01b4;
							}
							val2 = translationManager._cache.FetchManifestAsync(translationManager._token).ConfigureAwait(false);
							val = ((ConfiguredTaskAwaitable)(ref val2)).GetAwaiter();
							if (!((ConfiguredTaskAwaiter)(ref val)).IsCompleted)
							{
								num = (<>1__state = 0);
								<>u__1 = val;
								((AsyncTaskMethodBuilder)(ref <>t__builder)).AwaitUnsafeOnCompleted<ConfiguredTaskAwaiter, <LoadTranslationAsync>d__16>(ref val, ref this);
								return;
							}
						}
						else
						{
							val = <>u__1;
							<>u__1 = default(ConfiguredTaskAwaiter);
							num = (<>1__state = -1);
						}
						((ConfiguredTaskAwaiter)(ref val)).GetResult();
						<bundleTask>5__2 = ((translationManager._masterDataTranslator == null) ? translationManager._cache.LoadStaticBundleAsync(translationManager._token) : null);
						<namesTask>5__3 = ((translationManager._names == null) ? translationManager._cache.LoadAsync("names", null, translationManager._token) : null);
						<uiTask>5__4 = ((translationManager._uiTexts == null) ? translationManager._cache.LoadUiTextsAsync(translationManager._token) : null);
						InlineArray3<System.Threading.Tasks.Task> buffer = default(InlineArray3<System.Threading.Tasks.Task>);
						<PrivateImplementationDetails>.InlineArrayElementRef<InlineArray3<System.Threading.Tasks.Task>, System.Threading.Tasks.Task>(ref buffer, 0) = <bundleTask>5__2 ?? System.Threading.Tasks.Task.CompletedTask;
						<PrivateImplementationDetails>.InlineArrayElementRef<InlineArray3<System.Threading.Tasks.Task>, System.Threading.Tasks.Task>(ref buffer, 1) = <namesTask>5__3 ?? System.Threading.Tasks.Task.CompletedTask;
						<PrivateImplementationDetails>.InlineArrayElementRef<InlineArray3<System.Threading.Tasks.Task>, System.Threading.Tasks.Task>(ref buffer, 2) = <uiTask>5__4 ?? System.Threading.Tasks.Task.CompletedTask;
						val2 = System.Threading.Tasks.Task.WhenAll(<PrivateImplementationDetails>.InlineArrayAsReadOnlySpan<InlineArray3<System.Threading.Tasks.Task>, System.Threading.Tasks.Task>(in buffer, 3)).ConfigureAwait(false);
						val = ((ConfiguredTaskAwaitable)(ref val2)).GetAwaiter();
						if (!((ConfiguredTaskAwaiter)(ref val)).IsCompleted)
						{
							num = (<>1__state = 1);
							<>u__1 = val;
							((AsyncTaskMethodBuilder)(ref <>t__builder)).AwaitUnsafeOnCompleted<ConfiguredTaskAwaiter, <LoadTranslationAsync>d__16>(ref val, ref this);
							return;
						}
						goto IL_01b4;
						IL_03d4:
						<bundleTask>5__2 = null;
						<namesTask>5__3 = null;
						<uiTask>5__4 = null;
						goto end_IL_0013;
						IL_01b4:
						((ConfiguredTaskAwaiter)(ref val)).GetResult();
						object loadLock = translationManager._loadLock;
						bool flag = false;
						try
						{
							Monitor.Enter(loadLock, ref flag);
							if (!translationManager._shutdown)
							{
								if (<bundleTask>5__2 != null)
								{
									Dictionary<string, Dictionary<string, Dictionary<string, string>>> result = <bundleTask>5__2.GetAwaiter().GetResult();
									if (result != null)
									{
										Dictionary<string, Dictionary<string, Dictionary<string, string>>> val3 = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
										Enumerator<string, Dictionary<string, Dictionary<string, string>>> enumerator = result.GetEnumerator();
										try
										{
											string text = default(string);
											Dictionary<string, Dictionary<string, string>> val4 = default(Dictionary<string, Dictionary<string, string>>);
											while (enumerator.MoveNext())
											{
												enumerator.Current.Deconstruct(ref text, ref val4);
												string text2 = text;
												Dictionary<string, Dictionary<string, string>> val5 = val4;
												if (text2.StartsWith("m_", (StringComparison)4) && val5 != null)
												{
													val3[text2] = val5;
												}
											}
										}
										finally
										{
											if (num < 0)
											{
												((System.IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
											}
										}
										translationManager._masterDataTranslator = MasterDataTranslator.Create((IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, string>>>)(object)val3);
										Logger.Info($"Static translation bundle loaded. Tables: {val3.Count}");
									}
									else
									{
										translationManager.LogUnavailableStaticTranslation("static");
									}
								}
								if (<namesTask>5__3 != null)
								{
									translationManager._names = <namesTask>5__3.GetAwaiter().GetResult();
									if (translationManager._names == null)
									{
										translationManager.LogUnavailableStaticTranslation("names");
									}
									else
									{
										Logger.Info($"Static translation loaded [names]. Total: {translationManager._names.Count}");
									}
								}
								if (<uiTask>5__4 != null)
								{
									Dictionary<string, Dictionary<string, string>> result2 = <uiTask>5__4.GetAwaiter().GetResult();
									if (result2 == null)
									{
										translationManager.LogUnavailableStaticTranslation("ui_texts");
									}
									else
									{
										translationManager._uiTexts = new UiTextIndex(result2);
										Logger.Info($"Static translation loaded [ui_texts]. Paths: {result2.Count}");
									}
								}
								translationManager._staticTranslationsLoaded = translationManager._names != null && translationManager._masterDataTranslator != null && translationManager._uiTexts != null;
								goto IL_03d4;
							}
						}
						finally
						{
							if (num < 0 && flag)
							{
								Monitor.Exit(loadLock);
							}
						}
						end_IL_0013:;
					}
					catch (object obj) when (obj is OperationCanceledException && ((CancellationToken)(ref translationManager._token)).IsCancellationRequested)
					{
					}
					catch (System.Exception ex)
					{
						if (!translationManager._shutdown)
						{
							Logger.Warn($"Static translation load failed: {ex}");
						}
					}
				}
				catch (System.Exception exception)
				{
					<>1__state = -2;
					((AsyncTaskMethodBuilder)(ref <>t__builder)).SetException(exception);
					return;
				}
				<>1__state = -2;
				((AsyncTaskMethodBuilder)(ref <>t__builder)).SetResult();
			}

			[DebuggerHidden]
			private void SetStateMachine(IAsyncStateMachine stateMachine)
			{
				((AsyncTaskMethodBuilder)(ref <>t__builder)).SetStateMachine(stateMachine);
			}
		}

		private readonly TranslationCache _cache;

		private readonly object _loadLock = new object();

		private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

		private readonly CancellationToken _token;

		private System.Threading.Tasks.Task _loadTask;

		private volatile bool _shutdown;

		private bool _staticTranslationsLoaded;

		private volatile MasterDataTranslator _masterDataTranslator;

		private volatile Dictionary<string, string> _names;

		private volatile UiTextIndex _uiTexts;

		private readonly ConcurrentDictionary<string, System.Threading.Tasks.Task> _loadingNovels = new ConcurrentDictionary<string, System.Threading.Tasks.Task>();

		private readonly ConcurrentDictionary<string, Dictionary<string, string>> _novels = new ConcurrentDictionary<string, Dictionary<string, string>>();

		internal bool HasUiTranslations => _uiTexts != null;

		internal bool MasterDataTranslationReady => _masterDataTranslator != null;

		internal int MasterDataTranslationTableCount => _masterDataTranslator?.TableCount ?? 0;

		public TranslationManager(TranslationCache cache)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Expected O, but got Unknown
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			_cache = cache;
			_token = _lifetime.Token;
		}

		public void Initialize()
		{
			EnsureStaticTranslationsLoadedAsync();
		}

		public void Shutdown()
		{
			lock (_loadLock)
			{
				if (_shutdown)
				{
					return;
				}
				_shutdown = true;
			}
			_lifetime.Cancel();
			_lifetime.Dispose();
		}

		public System.Threading.Tasks.Task EnsureStaticTranslationsLoadedAsync()
		{
			lock (_loadLock)
			{
				if (_shutdown || (!Config.TranslationEnabledAtStartup && !Config.Translation.Value))
				{
					return System.Threading.Tasks.Task.CompletedTask;
				}
				if (_loadTask == null || _loadTask.IsCanceled || _loadTask.IsFaulted || (_loadTask.IsCompleted && !_staticTranslationsLoaded))
				{
					_loadTask = LoadTranslationAsync();
				}
				return _loadTask;
			}
		}

		[AsyncStateMachine(typeof(<LoadTranslationAsync>d__16))]
		private System.Threading.Tasks.Task LoadTranslationAsync()
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			<LoadTranslationAsync>d__16 <LoadTranslationAsync>d__ = default(<LoadTranslationAsync>d__16);
			<LoadTranslationAsync>d__.<>t__builder = AsyncTaskMethodBuilder.Create();
			<LoadTranslationAsync>d__.<>4__this = this;
			<LoadTranslationAsync>d__.<>1__state = -1;
			((AsyncTaskMethodBuilder)(ref <LoadTranslationAsync>d__.<>t__builder)).Start<<LoadTranslationAsync>d__16>(ref <LoadTranslationAsync>d__);
			return ((AsyncTaskMethodBuilder)(ref <LoadTranslationAsync>d__.<>t__builder)).Task;
		}

		public string TranslateName(string sourceText)
		{
			string text = default(string);
			if (string.IsNullOrEmpty(sourceText) || _names == null || !_names.TryGetValue(sourceText, ref text) || string.IsNullOrEmpty(text))
			{
				return sourceText;
			}
			return text;
		}

		public string TranslateUiText(string path, string sourceText)
		{
			UiTextIndex uiTexts = _uiTexts;
			if (uiTexts == null || !uiTexts.TryTranslate(path, sourceText, out var translatedText))
			{
				return sourceText;
			}
			return translatedText;
		}

		internal bool HasMasterDataTranslation(Type cacheType)
		{
			return _masterDataTranslator?.Contains(cacheType) ?? false;
		}

		internal bool TryTranslateMasterDataCache(Type cacheType, IMasterLoadResult result, out int translatedEntries)
		{
			MasterDataTranslator masterDataTranslator = _masterDataTranslator;
			if (masterDataTranslator != null)
			{
				return masterDataTranslator.TryTranslate(cacheType, result, out translatedEntries);
			}
			translatedEntries = 0;
			return false;
		}

		internal bool HasNovel(string novelId)
		{
			return _novels.ContainsKey(novelId);
		}

		internal bool TryGetNovel(string novelId, out Dictionary<string, string> translations)
		{
			return _novels.TryGetValue(novelId, ref translations);
		}

		[AsyncStateMachine(typeof(<GetNovelTranslationAsync>d__29))]
		public System.Threading.Tasks.Task GetNovelTranslationAsync(string novelId)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			<GetNovelTranslationAsync>d__29 <GetNovelTranslationAsync>d__ = default(<GetNovelTranslationAsync>d__29);
			<GetNovelTranslationAsync>d__.<>t__builder = AsyncTaskMethodBuilder.Create();
			<GetNovelTranslationAsync>d__.<>4__this = this;
			<GetNovelTranslationAsync>d__.novelId = novelId;
			<GetNovelTranslationAsync>d__.<>1__state = -1;
			((AsyncTaskMethodBuilder)(ref <GetNovelTranslationAsync>d__.<>t__builder)).Start<<GetNovelTranslationAsync>d__29>(ref <GetNovelTranslationAsync>d__);
			return ((AsyncTaskMethodBuilder)(ref <GetNovelTranslationAsync>d__.<>t__builder)).Task;
		}

		[AsyncStateMachine(typeof(<EnsureNovelTranslationLoadedAsync>d__30))]
		public System.Threading.Tasks.Task EnsureNovelTranslationLoadedAsync(string novelId)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			<EnsureNovelTranslationLoadedAsync>d__30 <EnsureNovelTranslationLoadedAsync>d__ = default(<EnsureNovelTranslationLoadedAsync>d__30);
			<EnsureNovelTranslationLoadedAsync>d__.<>t__builder = AsyncTaskMethodBuilder.Create();
			<EnsureNovelTranslationLoadedAsync>d__.<>4__this = this;
			<EnsureNovelTranslationLoadedAsync>d__.novelId = novelId;
			<EnsureNovelTranslationLoadedAsync>d__.<>1__state = -1;
			((AsyncTaskMethodBuilder)(ref <EnsureNovelTranslationLoadedAsync>d__.<>t__builder)).Start<<EnsureNovelTranslationLoadedAsync>d__30>(ref <EnsureNovelTranslationLoadedAsync>d__);
			return ((AsyncTaskMethodBuilder)(ref <EnsureNovelTranslationLoadedAsync>d__.<>t__builder)).Task;
		}

		private void LogUnavailableStaticTranslation(string type)
		{
			if (_cache.IsMissingFromManifest(type))
			{
				Logger.Info("Static translation is not translated yet [" + type + "]");
				return;
			}
			Logger.Warn("Static translation load failed [" + type + "]");
			if (type == "static")
			{
				Toast.Warning("加载失败", "MasterData 静态翻译合并包加载失败");
			}
		}
	}
	public static class TranslationPaths
	{
		public const string Manifest = "manifest";

		public const string Names = "names";

		public const string Novels = "novels";

		public const string Static = "static";

		public const string UiTexts = "ui_texts";

		public static string BuildRelativePath(string type, string language, string id = null)
		{
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			if (!(type == "novels"))
			{
				if (type == "manifest")
				{
					return "manifest/" + language + ".json";
				}
				return type + "/" + language + ".json";
			}
			if (id == null)
			{
				throw new ArgumentException("Novel ID is required for novels type");
			}
			return $"{"novels"}/{id}/{language}.json";
		}

		public static string BuildRemoteUrl(string cdn, string relativePath)
		{
			return cdn.TrimEnd('/') + "/" + relativePath;
		}

		public static string BuildCachePath(string cacheDir, string relativePath)
		{
			return Path.Combine(cacheDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
		}
	}
	internal sealed class UiTextIndex
	{
		private sealed class UiTextPathRules
		{
			private readonly Regex _pathRegex;

			private readonly Dictionary<string, string> _exactTexts = new Dictionary<string, string>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

			private readonly List<UiTextPattern> _patterns = new List<UiTextPattern>();

			[field: CompilerGenerated]
			public bool IsWildcard
			{
				[CompilerGenerated]
				get;
			}

			[field: CompilerGenerated]
			public int Specificity
			{
				[CompilerGenerated]
				get;
			}

			public UiTextPathRules(string path, Dictionary<string, string> translations)
			{
				//IL_0081: Unknown result type (might be due to invalid IL or missing references)
				//IL_0086: Unknown result type (might be due to invalid IL or missing references)
				//IL_0076: Unknown result type (might be due to invalid IL or missing references)
				//IL_0080: Expected O, but got Unknown
				//IL_008b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0090: Unknown result type (might be due to invalid IL or missing references)
				IsWildcard = path.Contains('*', (StringComparison)4);
				Specificity = path.Length - Enumerable.Count<char>((System.Collections.Generic.IEnumerable<char>)path, (Func<char, bool>)((char character) => character == '*'));
				if (IsWildcard)
				{
					_pathRegex = new Regex(BuildPathPattern(path), (RegexOptions)520);
				}
				Enumerator<string, string> enumerator = translations.GetEnumerator();
				try
				{
					string text = default(string);
					string text2 = default(string);
					while (enumerator.MoveNext())
					{
						enumerator.Current.Deconstruct(ref text, ref text2);
						string text3 = text;
						string text4 = text2;
						if (!string.IsNullOrEmpty(text4))
						{
							if (PlaceholderRegex.IsMatch(text3))
							{
								_patterns.Add(new UiTextPattern(text3, text4));
							}
							else
							{
								_exactTexts[text3] = text4;
							}
						}
					}
				}
				finally
				{
					((System.IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
				}
			}

			public bool MatchesPath(string transformPath)
			{
				if (IsWildcard)
				{
					return _pathRegex.IsMatch(transformPath);
				}
				return true;
			}

			public bool TryTranslate(string sourceText, out string translatedText)
			{
				//IL_0017: Unknown result type (might be due to invalid IL or missing references)
				//IL_001c: Unknown result type (might be due to invalid IL or missing references)
				if (_exactTexts.TryGetValue(sourceText, ref translatedText))
				{
					return true;
				}
				Enumerator<UiTextPattern> enumerator = _patterns.GetEnumerator();
				try
				{
					while (enumerator.MoveNext())
					{
						if (enumerator.Current.TryTranslate(sourceText, out translatedText))
						{
							return true;
						}
					}
				}
				finally
				{
					((System.IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
				}
				translatedText = null;
				return false;
			}

			private static string BuildPathPattern(string path)
			{
				//IL_0005: Unknown result type (might be due to invalid IL or missing references)
				//IL_000b: Expected O, but got Unknown
				StringBuilder val = new StringBuilder("^");
				for (int i = 0; i < path.Length; i++)
				{
					char c = path[i];
					val.Append((c == '*') ? "[^/]*" : Regex.Escape(c.ToString()));
				}
				val.Append('$');
				return ((object)val).ToString();
			}
		}

		private sealed class UiTextPattern
		{
			private readonly Regex _sourceRegex;

			private readonly string _translatedTemplate;

			public UiTextPattern(string sourceTemplate, string translatedTemplate)
			{
				//IL_0012: Unknown result type (might be due to invalid IL or missing references)
				//IL_001c: Expected O, but got Unknown
				_sourceRegex = new Regex(BuildSourcePattern(sourceTemplate), (RegexOptions)536);
				_translatedTemplate = translatedTemplate;
			}

			public bool TryTranslate(string sourceText, out string translatedText)
			{
				//IL_003d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0047: Expected O, but got Unknown
				Match match = _sourceRegex.Match(sourceText);
				if (!((Group)match).Success)
				{
					translatedText = null;
					return false;
				}
				translatedText = PlaceholderRegex.Replace(_translatedTemplate, (MatchEvaluator)delegate(Match matchResult)
				{
					string text = "p" + ((Capture)matchResult.Groups[1]).Value;
					return (!match.Groups[text].Success) ? ((Capture)matchResult).Value : ((Capture)match.Groups[text]).Value;
				});
				return !string.IsNullOrEmpty(translatedText);
			}

			private static string BuildSourcePattern(string sourceTemplate)
			{
				//IL_0005: Unknown result type (might be due to invalid IL or missing references)
				//IL_000b: Expected O, but got Unknown
				//IL_0034: Unknown result type (might be due to invalid IL or missing references)
				//IL_003b: Expected O, but got Unknown
				StringBuilder val = new StringBuilder("^");
				HashSet<string> val2 = new HashSet<string>((IEqualityComparer<string>)(object)StringComparer.Ordinal);
				int num = 0;
				foreach (Match item in PlaceholderRegex.Matches(sourceTemplate))
				{
					Match val3 = item;
					val.Append(Regex.Escape(sourceTemplate.Substring(num, ((Capture)val3).Index - num)));
					string text = "p" + ((Capture)val3.Groups[1]).Value;
					val.Append(val2.Add(text) ? ("(?<" + text + ">.+?)") : ("\\k<" + text + ">"));
					num = ((Capture)val3).Index + ((Capture)val3).Length;
				}
				val.Append(Regex.Escape(sourceTemplate.Substring(num)));
				val.Append('$');
				return ((object)val).ToString();
			}
		}

		private static readonly Regex PlaceholderRegex = new Regex("\\{(\\d+)\\}", (RegexOptions)8);

		private readonly Dictionary<string, UiTextPathRules> _exactPaths = new Dictionary<string, UiTextPathRules>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

		private readonly List<UiTextPathRules> _wildcardPaths = new List<UiTextPathRules>();

		public UiTextIndex(Dictionary<string, Dictionary<string, string>> table)
		{
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			Enumerator<string, Dictionary<string, string>> enumerator = table.GetEnumerator();
			try
			{
				string text = default(string);
				Dictionary<string, string> val = default(Dictionary<string, string>);
				while (enumerator.MoveNext())
				{
					enumerator.Current.Deconstruct(ref text, ref val);
					string text2 = text;
					Dictionary<string, string> val2 = val;
					if (val2 != null)
					{
						UiTextPathRules uiTextPathRules = new UiTextPathRules(text2, val2);
						if (uiTextPathRules.IsWildcard)
						{
							_wildcardPaths.Add(uiTextPathRules);
						}
						else
						{
							_exactPaths[text2] = uiTextPathRules;
						}
					}
				}
			}
			finally
			{
				((System.IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
			}
			_wildcardPaths.Sort((Comparison<UiTextPathRules>)((UiTextPathRules left, UiTextPathRules right) => right.Specificity.CompareTo(left.Specificity)));
		}

		public bool TryTranslate(string transformPath, string sourceText, out string translatedText)
		{
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			translatedText = null;
			if (string.IsNullOrEmpty(transformPath) || string.IsNullOrEmpty(sourceText))
			{
				return false;
			}
			UiTextPathRules uiTextPathRules = default(UiTextPathRules);
			if (_exactPaths.TryGetValue(transformPath, ref uiTextPathRules) && uiTextPathRules.TryTranslate(sourceText, out translatedText))
			{
				return true;
			}
			Enumerator<UiTextPathRules> enumerator = _wildcardPaths.GetEnumerator();
			try
			{
				while (enumerator.MoveNext())
				{
					UiTextPathRules current = enumerator.Current;
					if (current.MatchesPath(transformPath) && current.TryTranslate(sourceText, out translatedText))
					{
						return true;
					}
				}
			}
			finally
			{
				((System.IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
			}
			return false;
		}
	}
}
namespace AbyssMod.Patches
{
	[HarmonyPatch]
	public static class EnhancePatch
	{
		private static readonly Dictionary<Transform, Vector3> _novelLive2DOriginalScales = new Dictionary<Transform, Vector3>();

		private static int _allowStopVoiceCount;

		private static float _novelLive2DScale = 0f / 0f;

		[HarmonyPostfix]
		[HarmonyPatch(typeof(NovelLive2DObject), "Initialize")]
		public static void DisableMosaic(NovelLive2DObject __instance)
		{
			Il2CppReferenceArray<CubismDrawable> drawables = __instance.GetDrawables();
			if (drawables == null)
			{
				return;
			}
			System.Collections.Generic.IEnumerator<CubismDrawable> enumerator = ((Il2CppArrayBase<CubismDrawable>)(object)drawables).GetEnumerator();
			try
			{
				while (((System.Collections.IEnumerator)enumerator).MoveNext())
				{
					CubismDrawable current = enumerator.Current;
					if (!Config.DynamicMosaic.Value && ((Object)current).name.StartsWith("Mosaic", (StringComparison)4))
					{
						((Component)current).gameObject.SetActive(false);
					}
					if (Config.HideNovelGlasses.Value && ((Object)current).name.Contains("glasses", (StringComparison)5))
					{
						((Component)current).gameObject.SetActive(false);
					}
				}
			}
			finally
			{
				((System.IDisposable)enumerator)?.Dispose();
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(SoundCautionPopupController), "SetupPopupEvent")]
		public static void DisableSoundCaution(SoundCautionPopupController __instance)
		{
			if (!Config.SoundCaution.Value)
			{
				Action onClickOk = __instance._onClickOk;
				if (onClickOk != null)
				{
					onClickOk.Invoke();
				}
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(NovelSoundManager), "StopCategory")]
		public static bool CancelStoppingVoice(int nCategory, bool playFade)
		{
			if (Config.VoiceInterruption.Value || _allowStopVoiceCount > 0)
			{
				return true;
			}
			return nCategory != 2 || playFade;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(NovelSoundManager), "PlaySound")]
		public static void StopVoiceBeforePlaying(NovelSoundManager __instance, SoundCategory category)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Invalid comparison between Unknown and I4
			if (!Config.VoiceInterruption.Value && (int)category == 2)
			{
				_allowStopVoiceCount++;
				try
				{
					__instance.StopCategory(2, false);
				}
				finally
				{
					_allowStopVoiceCount--;
				}
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(TopView), "PlayMovie")]
		public static void DisableTitleMovie(TopView __instance, CancellationToken ct)
		{
			if (!Config.TitleMovie.Value && !__instance.MovieComplete)
			{
				__instance.MovieSkip(ct);
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(TopView), "CheckMovieLoop")]
		public static bool DisableTitleMovieLoop()
		{
			return Config.TitleMovie.Value;
		}

		[HarmonyPrefix]
		[HarmonyPatch(/*Could not decode attribute arguments.*/)]
		public static void ChangeTimeoutLimit(ref int value)
		{
			value = 60;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(NovelLive2DController), "Setup")]
		public static void BeginNovelLive2DScale(NovelLive2DController __instance)
		{
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			Transform canvasRoot = __instance._canvasRoot;
			if (!((Object)(object)canvasRoot == (Object)null))
			{
				Vector3 localScale = default(Vector3);
				if (!_novelLive2DOriginalScales.TryGetValue(canvasRoot, ref localScale))
				{
					localScale = canvasRoot.localScale;
					_novelLive2DOriginalScales[canvasRoot] = localScale;
				}
				canvasRoot.localScale = ScaleNovelLive2D(localScale, GetNovelLive2DScale());
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(NovelLive2DController), "Release")]
		public static void EndNovelLive2DScale(NovelLive2DController __instance)
		{
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			Transform canvasRoot = __instance._canvasRoot;
			Vector3 localScale = default(Vector3);
			if (canvasRoot != null && _novelLive2DOriginalScales.Remove(canvasRoot, ref localScale) && (Object)(object)canvasRoot != (Object)null)
			{
				canvasRoot.localScale = localScale;
			}
		}

		private static float GetNovelLive2DScale()
		{
			if (float.IsNaN(_novelLive2DScale))
			{
				_novelLive2DScale = Mathf.Clamp(Config.NovelLive2DScale.Value, 0.1f, 10f);
			}
			return _novelLive2DScale;
		}

		private static Vector3 ScaleNovelLive2D(Vector3 originalScale, float scale)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			return new Vector3(originalScale.x * scale, originalScale.y * scale, originalScale.z);
		}

		internal static void Reset()
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			_allowStopVoiceCount = 0;
			_novelLive2DScale = 0f / 0f;
			Enumerator<Transform, Vector3> enumerator = _novelLive2DOriginalScales.GetEnumerator();
			try
			{
				Transform val = default(Transform);
				Vector3 val2 = default(Vector3);
				while (enumerator.MoveNext())
				{
					enumerator.Current.Deconstruct(ref val, ref val2);
					Transform val3 = val;
					Vector3 localScale = val2;
					try
					{
						if (val3 != null && (Object)(object)val3 != (Object)null)
						{
							val3.localScale = localScale;
						}
					}
					catch (System.Exception ex)
					{
						Logger.Warn("Failed to restore Live2D scale during shutdown: " + ex.Message);
					}
				}
			}
			finally
			{
				((System.IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
			}
			_novelLive2DOriginalScales.Clear();
		}
	}
	[HarmonyPatch]
	public static class MasterDataPatch
	{
		[CompilerGenerated]
		private sealed class <LogSummaryWhenIdle>d__17 : System.Collections.Generic.IEnumerator<object>, System.Collections.IEnumerator, System.IDisposable
		{
			private int <>1__state;

			private object <>2__current;

			public int epoch;

			private int <revision>5__2;

			private int <frame>5__3;

			object System.Collections.Generic.IEnumerator<object>.Current
			{
				[DebuggerHidden]
				get
				{
					return <>2__current;
				}
			}

			object System.Collections.IEnumerator.Current
			{
				[DebuggerHidden]
				get
				{
					return <>2__current;
				}
			}

			[DebuggerHidden]
			public <LogSummaryWhenIdle>d__17(int <>1__state)
			{
				this.<>1__state = <>1__state;
			}

			[DebuggerHidden]
			void System.IDisposable.Dispose()
			{
				int num = <>1__state;
				if (num == -3 || num == 1)
				{
					try
					{
					}
					finally
					{
						<>m__Finally1();
					}
				}
				<>1__state = -2;
			}

			private bool MoveNext()
			{
				try
				{
					int num = <>1__state;
					if (num != 0)
					{
						if (num != 1)
						{
							return false;
						}
						<>1__state = -3;
						<frame>5__3++;
						goto IL_0064;
					}
					<>1__state = -1;
					<>1__state = -3;
					goto IL_008a;
					IL_0064:
					if (<frame>5__3 < 60)
					{
						<>2__current = null;
						<>1__state = 1;
						return true;
					}
					if (<revision>5__2 == _summaryRevision)
					{
						LogSummary(Core.Trans.MasterDataTranslationTableCount);
					}
					goto IL_008a;
					IL_008a:
					if (!_summaryLogged && epoch == _summaryEpoch)
					{
						<revision>5__2 = _summaryRevision;
						<frame>5__3 = 0;
						goto IL_0064;
					}
					<>m__Finally1();
					return false;
				}
				catch
				{
					//try-fault
					((System.IDisposable)this).Dispose();
					throw;
				}
			}

			bool System.Collections.IEnumerator.MoveNext()
			{
				//ILSpy generated this explicit interface implementation from .override directive in MoveNext
				return this.MoveNext();
			}

			private void <>m__Finally1()
			{
				<>1__state = -1;
				if (epoch == _summaryEpoch)
				{
					_summaryCoroutineRunning = false;
				}
			}

			[DebuggerHidden]
			void System.Collections.IEnumerator.Reset()
			{
				//IL_0000: Unknown result type (might be due to invalid IL or missing references)
				throw new NotSupportedException();
			}
		}

		[CompilerGenerated]
		private sealed class <TranslatePendingWhenReady>d__14 : System.Collections.Generic.IEnumerator<object>, System.Collections.IEnumerator, System.IDisposable
		{
			private int <>1__state;

			private object <>2__current;

			public int epoch;

			private System.Threading.Tasks.Task <loadTask>5__2;

			object System.Collections.Generic.IEnumerator<object>.Current
			{
				[DebuggerHidden]
				get
				{
					return <>2__current;
				}
			}

			object System.Collections.IEnumerator.Current
			{
				[DebuggerHidden]
				get
				{
					return <>2__current;
				}
			}

			[DebuggerHidden]
			public <TranslatePendingWhenReady>d__14(int <>1__state)
			{
				this.<>1__state = <>1__state;
			}

			[DebuggerHidden]
			void System.IDisposable.Dispose()
			{
				<loadTask>5__2 = null;
				<>1__state = -2;
			}

			private bool MoveNext()
			{
				//IL_014e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0153: Unknown result type (might be due to invalid IL or missing references)
				//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
				//IL_0159: Unknown result type (might be due to invalid IL or missing references)
				//IL_015e: Unknown result type (might be due to invalid IL or missing references)
				switch (<>1__state)
				{
				default:
					return false;
				case 0:
					<>1__state = -1;
					try
					{
						<loadTask>5__2 = Core.Trans.EnsureStaticTranslationsLoadedAsync();
					}
					catch (System.Exception ex)
					{
						Logger.Error($"[MasterDataTranslation] deferred load failed: {ex}");
						_pendingTranslationRunning = false;
						return false;
					}
					break;
				case 1:
					<>1__state = -1;
					break;
				}
				if (!<loadTask>5__2.IsCompleted && epoch == _pendingEpoch)
				{
					<>2__current = null;
					<>1__state = 1;
					return true;
				}
				if (epoch != _pendingEpoch)
				{
					return false;
				}
				try
				{
					try
					{
						TaskAwaiter awaiter = <loadTask>5__2.GetAwaiter();
						((TaskAwaiter)(ref awaiter)).GetResult();
					}
					catch (System.Exception ex2)
					{
						Logger.Error($"[MasterDataTranslation] deferred translation load failed: {ex2}");
					}
					if (!Core.Trans.MasterDataTranslationReady)
					{
						Logger.Warn($"[MasterDataTranslation] plan unavailable; skipped cached tables: {Pending.Count}");
						Pending.Clear();
						return false;
					}
					if (Pending.Count == 0)
					{
						return false;
					}
					Enumerator<string, ValueTuple<Type, IMasterLoadResult>> enumerator = Pending.Values.GetEnumerator();
					try
					{
						while (enumerator.MoveNext())
						{
							ValueTuple<Type, IMasterLoadResult> current = enumerator.Current;
							Type item = current.Item1;
							IMasterLoadResult item2 = current.Item2;
							Translate(item, item2);
						}
					}
					finally
					{
						((System.IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
					}
					Pending.Clear();
				}
				finally
				{
					if (epoch == _pendingEpoch)
					{
						_pendingTranslationRunning = false;
					}
				}
				return false;
			}

			bool System.Collections.IEnumerator.MoveNext()
			{
				//ILSpy generated this explicit interface implementation from .override directive in MoveNext
				return this.MoveNext();
			}

			[DebuggerHidden]
			void System.Collections.IEnumerator.Reset()
			{
				//IL_0000: Unknown result type (might be due to invalid IL or missing references)
				throw new NotSupportedException();
			}
		}

		private const string DownloadCallbackPrefix = "_DownloadFirstAsync_b__";

		private const int SummaryIdleFrames = 60;

		private static readonly Dictionary<string, ValueTuple<Type, IMasterLoadResult>> Pending = new Dictionary<string, ValueTuple<Type, IMasterLoadResult>>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

		private static readonly HashSet<string> ProcessedTables = new HashSet<string>((IEqualityComparer<string>)(object)StringComparer.Ordinal);

		private static bool _pendingTranslationRunning;

		private static bool _summaryCoroutineRunning;

		private static bool _summaryLogged;

		private static int _summaryRevision;

		private static int _summaryEpoch;

		private static int _translatedTables;

		private static int _translatedEntries;

		private static int _pendingEpoch;

		[HarmonyTargetMethod]
		private static MethodBase TargetMethod()
		{
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			System.Type[] parameterTypes = new System.Type[3]
			{
				typeof(Type),
				typeof(Object),
				typeof(bool)
			};
			return (MethodBase)(object)(Enumerable.SingleOrDefault<MethodInfo>((System.Collections.Generic.IEnumerable<MethodInfo>)AccessTools.GetDeclaredMethods(typeof(MasterDataStore)), (Func<MethodInfo, bool>)((MethodInfo candidate) => ((MemberInfo)candidate).Name.StartsWith("_DownloadFirstAsync_b__", (StringComparison)4) && candidate.ReturnType == typeof(void) && Enumerable.SequenceEqual<System.Type>(Enumerable.Select<ParameterInfo, System.Type>((System.Collections.Generic.IEnumerable<ParameterInfo>)((MethodBase)candidate).GetParameters(), (Func<ParameterInfo, System.Type>)((ParameterInfo parameter) => parameter.ParameterType)), (System.Collections.Generic.IEnumerable<System.Type>)parameterTypes))) ?? throw new MissingMethodException(typeof(MasterDataStore).FullName, "_DownloadFirstAsync_b__*(Type, Object, Boolean)"));
		}

		[HarmonyPostfix]
		public static void TranslateCachedTable(MasterDataStore __instance, Type elementType)
		{
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				bool masterDataTranslationReady = Core.Trans.MasterDataTranslationReady;
				if (masterDataTranslationReady && !Core.Trans.HasMasterDataTranslation(elementType))
				{
					return;
				}
				IMasterLoadResult val = __instance._caches[elementType];
				if (masterDataTranslationReady)
				{
					Translate(elementType, val);
					return;
				}
				Pending[((MemberInfo)elementType).Name] = new ValueTuple<Type, IMasterLoadResult>(elementType, val);
				if (!_pendingTranslationRunning)
				{
					_pendingTranslationRunning = true;
					try
					{
						MelonCoroutines.Start(TranslatePendingWhenReady(_pendingEpoch));
						return;
					}
					catch
					{
						_pendingTranslationRunning = false;
						throw;
					}
				}
				Core.Trans.EnsureStaticTranslationsLoadedAsync();
			}
			catch (System.Exception ex)
			{
				Logger.Error($"[MasterDataTranslation] failed [{((elementType != null) ? ((MemberInfo)elementType).Name : null)}]: {ex}");
			}
		}

		[IteratorStateMachine(typeof(<TranslatePendingWhenReady>d__14))]
		private static System.Collections.IEnumerator TranslatePendingWhenReady(int epoch)
		{
			System.Threading.Tasks.Task loadTask;
			try
			{
				loadTask = Core.Trans.EnsureStaticTranslationsLoadedAsync();
			}
			catch (System.Exception ex)
			{
				Logger.Error($"[MasterDataTranslation] deferred load failed: {ex}");
				_pendingTranslationRunning = false;
				yield break;
			}
			while (!loadTask.IsCompleted && epoch == _pendingEpoch)
			{
				yield return null;
			}
			if (epoch != _pendingEpoch)
			{
				yield break;
			}
			try
			{
				try
				{
					TaskAwaiter awaiter = loadTask.GetAwaiter();
					((TaskAwaiter)(ref awaiter)).GetResult();
				}
				catch (System.Exception ex2)
				{
					Logger.Error($"[MasterDataTranslation] deferred translation load failed: {ex2}");
				}
				if (!Core.Trans.MasterDataTranslationReady)
				{
					Logger.Warn($"[MasterDataTranslation] plan unavailable; skipped cached tables: {Pending.Count}");
					Pending.Clear();
				}
				else
				{
					if (Pending.Count == 0)
					{
						yield break;
					}
					Enumerator<string, ValueTuple<Type, IMasterLoadResult>> enumerator = Pending.Values.GetEnumerator();
					try
					{
						while (enumerator.MoveNext())
						{
							ValueTuple<Type, IMasterLoadResult> current = enumerator.Current;
							Type item = current.Item1;
							IMasterLoadResult item2 = current.Item2;
							Translate(item, item2);
						}
					}
					finally
					{
						((System.IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
					}
					Pending.Clear();
				}
			}
			finally
			{
				if (epoch == _pendingEpoch)
				{
					_pendingTranslationRunning = false;
				}
			}
		}

		private static void Translate(Type type, IMasterLoadResult result)
		{
			if (!Core.Trans.TryTranslateMasterDataCache(type, result, out var translatedEntries))
			{
				return;
			}
			string name = ((MemberInfo)type).Name;
			if (ProcessedTables.Add(name))
			{
				if (translatedEntries > 0)
				{
					_translatedTables++;
					_translatedEntries += translatedEntries;
				}
				int masterDataTranslationTableCount = Core.Trans.MasterDataTranslationTableCount;
				if (!_summaryLogged && masterDataTranslationTableCount > 0 && ProcessedTables.Count >= masterDataTranslationTableCount)
				{
					LogSummary(masterDataTranslationTableCount);
				}
				else
				{
					ScheduleSummary();
				}
			}
		}

		private static void ScheduleSummary()
		{
			_summaryRevision++;
			if (_summaryCoroutineRunning || _summaryLogged)
			{
				return;
			}
			_summaryCoroutineRunning = true;
			try
			{
				MelonCoroutines.Start(LogSummaryWhenIdle(_summaryEpoch));
			}
			catch
			{
				_summaryCoroutineRunning = false;
				throw;
			}
		}

		[IteratorStateMachine(typeof(<LogSummaryWhenIdle>d__17))]
		private static System.Collections.IEnumerator LogSummaryWhenIdle(int epoch)
		{
			try
			{
				while (!_summaryLogged && epoch == _summaryEpoch)
				{
					int revision = _summaryRevision;
					for (int frame = 0; frame < 60; frame++)
					{
						yield return null;
					}
					if (revision == _summaryRevision)
					{
						LogSummary(Core.Trans.MasterDataTranslationTableCount);
					}
				}
			}
			finally
			{
				if (epoch == _summaryEpoch)
				{
					_summaryCoroutineRunning = false;
				}
			}
		}

		private static void LogSummary(int plannedTables)
		{
			_summaryLogged = true;
			Logger.Info($"[MDT] MasterData translation summary. Planned: {plannedTables}, Processed: {ProcessedTables.Count}, Translated: {_translatedTables}, Entries: {_translatedEntries}");
		}

		internal static void Reset()
		{
			ResetPending();
			ProcessedTables.Clear();
			_summaryEpoch++;
			_summaryCoroutineRunning = false;
			_summaryLogged = false;
			_summaryRevision = 0;
			_translatedTables = 0;
			_translatedEntries = 0;
		}

		private static void ResetPending()
		{
			_pendingEpoch++;
			Pending.Clear();
			_pendingTranslationRunning = false;
		}
	}
	public static class PatchManager
	{
		private static Harmony _harmony;

		public static void Initialize()
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Expected O, but got Unknown
			if (_harmony != null)
			{
				return;
			}
			EnhancePatch.Reset();
			MasterDataPatch.Reset();
			TranslationPatch.Reset();
			Harmony val = (_harmony = new Harmony("AbyssMod"));
			try
			{
				val.PatchAll(typeof(EnhancePatch));
				if (Config.TranslationEnabledAtStartup)
				{
					val.PatchAll(typeof(MasterDataPatch));
					val.PatchAll(typeof(UiTranslationPatch));
				}
				val.PatchAll(typeof(TranslationPatch));
			}
			catch
			{
				Shutdown();
				throw;
			}
		}

		public static bool Shutdown()
		{
			Harmony harmony = _harmony;
			try
			{
				if (harmony != null)
				{
					harmony.UnpatchSelf();
				}
			}
			catch (System.Exception ex)
			{
				Logger.Error($"Harmony shutdown failed: {ex}");
				return false;
			}
			_harmony = null;
			EnhancePatch.Reset();
			MasterDataPatch.Reset();
			TranslationPatch.Reset();
			return true;
		}
	}
	[HarmonyPatch]
	public static class TranslationPatch
	{
		[CompilerGenerated]
		private sealed class <LoadNovelTranslationWhenReady>d__27 : System.Collections.Generic.IEnumerator<object>, System.Collections.IEnumerator, System.IDisposable
		{
			private int <>1__state;

			private object <>2__current;

			public TranslationManager manager;

			public string novelId;

			public int epoch;

			private System.Threading.Tasks.Task <loadTask>5__2;

			object System.Collections.Generic.IEnumerator<object>.Current
			{
				[DebuggerHidden]
				get
				{
					return <>2__current;
				}
			}

			object System.Collections.IEnumerator.Current
			{
				[DebuggerHidden]
				get
				{
					return <>2__current;
				}
			}

			[DebuggerHidden]
			public <LoadNovelTranslationWhenReady>d__27(int <>1__state)
			{
				this.<>1__state = <>1__state;
			}

			[DebuggerHidden]
			void System.IDisposable.Dispose()
			{
				<loadTask>5__2 = null;
				<>1__state = -2;
			}

			private bool MoveNext()
			{
				//IL_0077: Unknown result type (might be due to invalid IL or missing references)
				//IL_007c: Unknown result type (might be due to invalid IL or missing references)
				switch (<>1__state)
				{
				default:
					return false;
				case 0:
					<>1__state = -1;
					<loadTask>5__2 = manager.EnsureNovelTranslationLoadedAsync(novelId);
					break;
				case 1:
					<>1__state = -1;
					break;
				}
				if (!<loadTask>5__2.IsCompleted && epoch == _lifecycleEpoch)
				{
					<>2__current = null;
					<>1__state = 1;
					return true;
				}
				if (epoch != _lifecycleEpoch)
				{
					return false;
				}
				try
				{
					TaskAwaiter awaiter = <loadTask>5__2.GetAwaiter();
					((TaskAwaiter)(ref awaiter)).GetResult();
				}
				catch (System.Exception ex)
				{
					Logger.Warn("Scenario translation load failed [" + novelId + "]: " + ex.Message);
					return false;
				}
				if (string.Equals(NovelId, novelId, (StringComparison)4))
				{
					RefreshCurrentMessage();
				}
				return false;
			}

			bool System.Collections.IEnumerator.MoveNext()
			{
				//ILSpy generated this explicit interface implementation from .override directive in MoveNext
				return this.MoveNext();
			}

			[DebuggerHidden]
			void System.Collections.IEnumerator.Reset()
			{
				//IL_0000: Unknown result type (might be due to invalid IL or missing references)
				throw new NotSupportedException();
			}
		}

		private const string UserPlaceholder = "<user>";

		private const string HiddenUserPlaceholder = "%user%";

		private static NovelController _novelController;

		private static NovelViewMessageWindow _messageWindow;

		private static string _currentOriginalMessage;

		private static string _currentTranslatedMessage;

		private static bool _refreshingCurrentMessage;

		private static int _lifecycleEpoch;

		private static string NovelId
		{
			get
			{
				NovelController novelController = _novelController;
				object obj;
				if (novelController == null)
				{
					obj = null;
				}
				else
				{
					NovelModelCommon common = novelController._common;
					obj = ((common != null) ? common.ScriptId : null);
				}
				if (obj == null)
				{
					obj = string.Empty;
				}
				return (string)obj;
			}
		}

		private static bool CanTranslate()
		{
			if (Config.Translation.Value)
			{
				return Core.Trans != null;
			}
			return false;
		}

		private static string TranslateFrom(Dictionary<string, string> table, string sourceText)
		{
			if (string.IsNullOrEmpty(sourceText) || table == null)
			{
				return sourceText;
			}
			string text = default(string);
			if (!table.TryGetValue(sourceText, ref text) || string.IsNullOrEmpty(text))
			{
				return sourceText;
			}
			return text;
		}

		private static string TranslateCurrentNovelText(string sourceText)
		{
			if (!CanTranslate() || !TryGetNovel(NovelId, out var translation))
			{
				return sourceText;
			}
			return TranslateFrom(translation, sourceText);
		}

		private static bool TryGetNovel(string novelId, out Dictionary<string, string> translation)
		{
			translation = null;
			if (Core.Trans != null && !string.IsNullOrEmpty(novelId))
			{
				return Core.Trans.TryGetNovel(novelId, out translation);
			}
			return false;
		}

		private static string TranslateNovelMessage(Dictionary<string, string> translation, string sourceText, string displayName)
		{
			if (string.IsNullOrEmpty(sourceText) || translation == null)
			{
				return sourceText;
			}
			string text = default(string);
			if (!translation.TryGetValue(sourceText, ref text) || string.IsNullOrEmpty(text))
			{
				return sourceText;
			}
			return ExpandUserPlaceholder(text, displayName);
		}

		private static void CaptureCurrentNovelMessages(Dictionary<string, string> translation, string message)
		{
			string displayUserName = GetDisplayUserName();
			_currentOriginalMessage = message;
			_currentTranslatedMessage = TranslateNovelMessage(translation, message, displayUserName);
		}

		private static string GetConfiguredCurrentMessage()
		{
			if (!Config.Translation.Value)
			{
				return _currentOriginalMessage;
			}
			return _currentTranslatedMessage;
		}

		private static string GetDisplayUserName()
		{
			try
			{
				return StringUtility.ToDisplayUserName(Engine.Get<UserData>().UserStatus.Name.Value);
			}
			catch
			{
				return null;
			}
		}

		private static string ExpandUserPlaceholder(string value, string displayName)
		{
			if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(displayName))
			{
				return value;
			}
			return value.Replace("<user>", displayName, (StringComparison)4);
		}

		private static bool ContainsUserPlaceholder(string value)
		{
			if (!string.IsNullOrEmpty(value))
			{
				return value.Contains("<user>", (StringComparison)4);
			}
			return false;
		}

		private static string HideUserPlaceholder(string value)
		{
			return value?.Replace("<user>", "%user%", (StringComparison)4);
		}

		private static string RestoreUserPlaceholder(string value)
		{
			return value?.Replace("%user%", "<user>", (StringComparison)4);
		}

		private static void ResetCurrentMessageState()
		{
			_messageWindow = null;
			_currentOriginalMessage = null;
			_currentTranslatedMessage = null;
			_refreshingCurrentMessage = false;
		}

		private static NovelViewMessageWindow GetMessageWindow()
		{
			if ((Object)(object)_messageWindow == (Object)null || !((Component)_messageWindow).gameObject.activeInHierarchy)
			{
				_messageWindow = Object.FindObjectOfType<NovelViewMessageWindow>();
			}
			return _messageWindow;
		}

		public static void RefreshCurrentMessage()
		{
			try
			{
				if (!TryGetNovel(NovelId, out var translation))
				{
					return;
				}
				NovelViewMessageWindow messageWindow = GetMessageWindow();
				if ((Object)(object)messageWindow == (Object)null)
				{
					Logger.Info("Current novel message refresh skipped: no message window");
					return;
				}
				if (string.IsNullOrEmpty(_currentOriginalMessage))
				{
					NovelModelMessage messageData = messageWindow._messageData;
					string text = ((messageData != null) ? messageData.Message : null);
					if (!string.IsNullOrEmpty(text))
					{
						CaptureCurrentNovelMessages(translation, text);
					}
				}
				if (Config.Translation.Value && !string.IsNullOrEmpty(_currentOriginalMessage))
				{
					_currentTranslatedMessage = TranslateNovelMessage(translation, _currentOriginalMessage, GetDisplayUserName());
				}
				string text2 = GetConfiguredCurrentMessage();
				if (string.IsNullOrEmpty(text2) && messageWindow._messageData != null)
				{
					text2 = (Config.Translation.Value ? TranslateNovelMessage(translation, messageWindow._messageData.Message, GetDisplayUserName()) : messageWindow._messageData.Message);
				}
				if (string.IsNullOrEmpty(text2))
				{
					Logger.Info("Current novel message refresh skipped: no captured message");
					return;
				}
				_refreshingCurrentMessage = true;
				try
				{
					if (messageWindow._messageData != null)
					{
						messageWindow._messageData.Message = text2;
					}
					messageWindow.SetText(text2);
				}
				finally
				{
					_refreshingCurrentMessage = false;
				}
				Logger.Info("Current novel message refreshed");
			}
			catch (System.Exception ex)
			{
				Logger.Warn("Current novel message refresh failed: " + ex.Message);
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(NovelController), "InitNovel")]
		public static void InitNovelController(NovelController __instance)
		{
			_novelController = __instance;
			ResetCurrentMessageState();
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(NovelPathUtility), "GetNovelScenarioDirectory")]
		public static void SetupTranslation(string novelId)
		{
			if (CanTranslate() && !string.IsNullOrEmpty(novelId))
			{
				Logger.Info("NovelId: " + novelId);
				MelonCoroutines.Start(LoadNovelTranslationWhenReady(Core.Trans, novelId, _lifecycleEpoch));
			}
		}

		[IteratorStateMachine(typeof(<LoadNovelTranslationWhenReady>d__27))]
		private static System.Collections.IEnumerator LoadNovelTranslationWhenReady(TranslationManager manager, string novelId, int epoch)
		{
			System.Threading.Tasks.Task loadTask = manager.EnsureNovelTranslationLoadedAsync(novelId);
			while (!loadTask.IsCompleted && epoch == _lifecycleEpoch)
			{
				yield return null;
			}
			if (epoch == _lifecycleEpoch)
			{
				try
				{
					TaskAwaiter awaiter = loadTask.GetAwaiter();
					((TaskAwaiter)(ref awaiter)).GetResult();
				}
				catch (System.Exception ex)
				{
					Logger.Warn("Scenario translation load failed [" + novelId + "]: " + ex.Message);
					yield break;
				}
				if (string.Equals(NovelId, novelId, (StringComparison)4))
				{
					RefreshCurrentMessage();
				}
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(NovelViewMessageWindow), "SetName")]
		public static void TranslateSpeakerName(ref string name)
		{
			if (CanTranslate() && TryGetNovel(NovelId, out var _))
			{
				name = Core.Trans.TranslateName(name);
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(NovelViewMessageWindow), "SetText")]
		public static void TrackMessageWindow(NovelViewMessageWindow __instance)
		{
			_messageWindow = __instance;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(NovelText), "Parse")]
		public static void TranslateNovelText(ref string message)
		{
			if (!_refreshingCurrentMessage)
			{
				if (TryGetNovel(NovelId, out var translation))
				{
					CaptureCurrentNovelMessages(translation, message);
					message = GetConfiguredCurrentMessage();
				}
				else
				{
					_currentOriginalMessage = message;
					_currentTranslatedMessage = message;
				}
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(NovelModelMessageLog), "Add")]
		public static bool NormalizeLogPlaceholders(ref string charaName, ref string message)
		{
			if (_refreshingCurrentMessage)
			{
				return false;
			}
			charaName = HideUserPlaceholder(charaName);
			message = HideUserPlaceholder(message);
			return true;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(NovelLogPopup), "SetData")]
		public static void TranslateLogEntries(ref List<NovelLogData> dataList)
		{
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0089: Expected O, but got Unknown
			List<NovelLogData> val = new List<NovelLogData>();
			Enumerator<NovelLogData> enumerator = dataList.GetEnumerator();
			while (enumerator.MoveNext())
			{
				NovelLogData current = enumerator.Current;
				string text = RestoreUserPlaceholder(current.Name);
				string text2 = RestoreUserPlaceholder(current.Message);
				if (CanTranslate() && TryGetNovel(current.ScriptId, out var translation))
				{
					text = Core.Trans.TranslateName(text);
					text2 = TranslateFrom(translation, text2);
				}
				val.Add(new NovelLogData(current.ScriptId, current.AssetId, text, text2, current.LogId, current.Voice, current.Ct));
			}
			dataList = val;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(NovelModelDotBalloon), "StartBalloonMessage")]
		public static void TranslateBalloonMessage(CommandDotMessageData messageData)
		{
			messageData.Message = TranslateCurrentNovelText(messageData.Message);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(NovelCmdMessageTextCenter), "OnCommandStartASync")]
		public static void HideCenterTextUserPlaceholder(NovelArguments args)
		{
			string value = args.GetString(2, (string)null);
			if (ContainsUserPlaceholder(value))
			{
				args._list[2] = NovelArgument.SetString(HideUserPlaceholder(value));
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(NovelModelMessageText), "SetMessage")]
		public static void TranslateMessageText(CommandMessageTextData data)
		{
			data.Message = RestoreUserPlaceholder(data.Message);
			data.Message = TranslateCurrentNovelText(data.Message);
			if (ContainsUserPlaceholder(data.Message))
			{
				data.Message = ExpandUserPlaceholder(data.Message, GetDisplayUserName());
			}
		}

		internal static void Reset()
		{
			_lifecycleEpoch++;
			_novelController = null;
			ResetCurrentMessageState();
		}
	}
	[HarmonyPatch]
	public static class UiTranslationPatch
	{
		private static bool _errorLogged;

		private static string Translate(TMP_Text text, string source)
		{
			if (Core.Trans == null || string.IsNullOrEmpty(source) || !Core.Trans.HasUiTranslations)
			{
				return source;
			}
			return Core.Trans.TranslateUiText(GetTransformPath((text != null) ? text.transform : null), source);
		}

		private static string TranslateSafely(TMP_Text text, string source)
		{
			try
			{
				return Translate(text, source);
			}
			catch (System.Exception ex)
			{
				if (!_errorLogged)
				{
					_errorLogged = true;
					Logger.Warn("UI text translation failed; further errors suppressed: " + ex.Message);
				}
				return source;
			}
		}

		private static string GetTransformPath(Transform transform)
		{
			if ((Object)(object)transform == (Object)null)
			{
				return null;
			}
			Stack<string> val = new Stack<string>();
			Transform val2 = transform;
			while ((Object)(object)val2 != (Object)null)
			{
				val.Push(((Object)val2).name);
				val2 = val2.parent;
			}
			return string.Join("/", (System.Collections.Generic.IEnumerable<string>)val);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(TMP_Text), "set_text")]
		public static void TranslateTextSetter(TMP_Text __instance, ref string value)
		{
			value = TranslateSafely(__instance, value);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(TMP_Text), "SetText", new System.Type[] { typeof(string) })]
		public static void TranslateSetText(TMP_Text __instance, ref string sourceText)
		{
			sourceText = TranslateSafely(__instance, sourceText);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(TMP_Text), "SetText", new System.Type[]
		{
			typeof(string),
			typeof(bool)
		})]
		public static bool TranslateSetTextAndSyncInputBox(TMP_Text __instance, ref string sourceText)
		{
			__instance.text = TranslateSafely(__instance, sourceText);
			return false;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(TextMeshProUGUI), "OnEnable")]
		public static void TranslateStaticUiText(TextMeshProUGUI __instance)
		{
			TranslateOnEnable((TMP_Text)(object)__instance);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(TextMeshPro), "OnEnable")]
		public static void TranslateStaticUiText(TextMeshPro __instance)
		{
			TranslateOnEnable((TMP_Text)(object)__instance);
		}

		private static void TranslateOnEnable(TMP_Text text)
		{
			if (!((Object)(object)text == (Object)null))
			{
				string text2 = TranslateSafely(text, text.text);
				if (!string.Equals(text2, text.text, (StringComparison)4))
				{
					text.text = text2;
				}
			}
		}
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.1.0.9782' (yours is '9.1.0.7988')
