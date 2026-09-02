using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace OC2LevelRuntimeLoader
{
    /// <summary>
    /// 关卡运行时程序集加载器（通用，与 OC2DIYLevel 零耦合）。
    ///
    /// 约定：关卡集 zip 里除 info_&lt;set&gt; / s_* 外，可包含一个普通（非场景）
    /// bundle 文件 runtime，内嵌名为 *.dll.bytes 的 TextAsset（Unity 编译的关卡集
    /// 程序集，如 Stub_jia_carnival.dll.bytes，由编辑器 Tools/CustomStub 打包）。
    ///
    /// 启动后首帧：扫描 BepInEx/plugins/OC2DIYLevel/levels/*/runtime →
    /// AssetBundle.LoadFromFile → LoadAllAssets&lt;TextAsset&gt; → Assembly.Load。
    /// 程序集在关卡场景加载前进入 AppDomain，场景里的脚本引用按程序集名解析
    /// （与 LevelEditorStub 同机制）。场景组件随场景自行激活，无需额外入口。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class LevelRuntimeLoader : BaseUnityPlugin
    {
        public const string PluginGuid = "oc2.levelruntimeloader";
        public const string PluginName = "OC2 LevelRuntime Loader";
        public const string PluginVersion = "1.0.0";

        private readonly List<byte[]> _pendingRaw = new List<byte[]>();
        private readonly HashSet<string> _loadedNames = new HashSet<string>(StringComparer.Ordinal);
        private bool _done;

        private void Awake()
        {
            // 引用顺序兜底：关卡程序集引用 LevelEditorStub / UnityEngine 等，
            // 绝大多数情况在类型首次使用时已就绪；异常时再从未加载的字节里找。
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            Log("v" + PluginVersion + " ready");
        }

        private void Update()
        {
            if (_done)
                return;
            _done = true;
            try
            {
                ScanAndLoad();
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex);
            }
        }

        private void ScanAndLoad()
        {
            var levelsRoot = Path.Combine(Path.Combine(Paths.PluginPath, "OC2DIYLevel"), "levels");
            if (!Directory.Exists(levelsRoot))
            {
                Log("levels 目录不存在（未装 OC2DIYLevel 或无自定义关卡）: " + levelsRoot);
                return;
            }

            foreach (var setDir in Directory.GetDirectories(levelsRoot))
            {
                var runtimeBundle = Path.Combine(setDir, "runtime");
                if (!File.Exists(runtimeBundle))
                    continue;
                try
                {
                    var bundle = AssetBundle.LoadFromFile(runtimeBundle);
                    if (bundle == null)
                    {
                        Log("bundle 加载失败: " + runtimeBundle);
                        continue;
                    }
                    // 普通 bundle 不能 Unload（场景组件的类型还活在其中加载的程序集里）
                    foreach (var asset in bundle.LoadAllAssets<TextAsset>())
                    {
                        if (asset == null || string.IsNullOrEmpty(asset.name))
                            continue;
                        if (!asset.name.EndsWith(".dll.bytes", StringComparison.OrdinalIgnoreCase))
                            continue;
                        LoadFromBytes(asset.name, asset.bytes);
                    }
                }
                catch (Exception ex)
                {
                    Log("bundle 处理异常 " + runtimeBundle + ": " + ex.Message);
                }
            }
        }

        private void LoadFromBytes(string displayName, byte[] raw)
        {
            try
            {
                var asm = Assembly.Load(raw);
                var name = asm.GetName().Name;
                if (_loadedNames.Contains(name))
                {
                    Log("程序集已加载过，跳过重复加载: " + name);
                    return;
                }
                _loadedNames.Add(name);
                _pendingRaw.Remove(raw);
                Log("已加载关卡程序集: " + name + "（来自 " + displayName + "）");
            }
            catch (Exception ex)
            {
                _pendingRaw.Add(raw);
                Log("Assembly.Load 失败（保留字节供 AssemblyResolve 兜底）" + displayName + ": " + ex.Message);
            }
        }

        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var simpleName = args.Name.Split(',')[0].Trim();
            foreach (var raw in _pendingRaw)
            {
                try
                {
                    var asm = Assembly.Load(raw);
                    if (asm.GetName().Name == simpleName)
                    {
                        _pendingRaw.Remove(raw);
                        _loadedNames.Add(simpleName);
                        Log("AssemblyResolve 兜底加载: " + simpleName);
                        return asm;
                    }
                }
                catch
                {
                    // 尝试下一个
                }
            }
            return null;
        }

        private static void Log(string message)
        {
            Debug.Log("[OC2LevelRuntimeLoader] " + message);
        }
    }
}
