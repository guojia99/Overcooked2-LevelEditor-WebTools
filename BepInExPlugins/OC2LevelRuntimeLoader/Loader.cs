using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OC2LevelRuntimeLoader
{
    /// <summary>
    /// 关卡运行时程序集加载器（OC2DIYLevel 的配套能力扩展）。
    ///
    /// 约定：关卡集 zip 里除 info_&lt;set&gt; / s_* 外，可包含一个普通（非场景）
    /// bundle 文件 runtime，内嵌名为 *.dll.bytes 的 TextAsset（Unity 编译的关卡集
    /// 程序集，如 Stub_jia_carnival.dll.bytes，由编辑器 Layout Editor/CustomStub 打包）。
    ///
    /// 加载时机：启动首帧扫一遍（程序集必须先于场景进入 AppDomain，MonoScript
    /// 才能解析）；此后每次场景加载再幂等补扫一遍（拾漏：启动后才安装/更新的
    /// 关卡集），并做自愈——检测到随机箱数据载体（tag "RandomCrate|…"）而组件
    /// 缺失（含 MonoScript 解析失败成 Missing Script 的情况）时动态 AddComponent，
    /// 彻底绕开运行时程序集的脚本解析风险。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class LevelRuntimeLoader : BaseUnityPlugin
    {
        public const string PluginGuid = "oc2.levelruntimeloader";
        public const string PluginName = "OC2 LevelRuntime Loader";
        public const string PluginVersion = "1.3.2";

        private static ManualLogSource _log;
        private static readonly List<byte[]> PendingRaw = new List<byte[]>();
        private static readonly HashSet<string> LoadedNames = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> LoadedBundles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _startupScanDone;

        private void Awake()
        {
            _log = Logger;
            // 引用顺序兜底：关卡程序集引用 LevelEditorStub / UnityEngine 等，
            // 绝大多数情况在类型首次使用时已就绪；异常时再从未加载的字节里找。
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            _log.LogInfo("v" + PluginVersion + " ready（启动扫一遍 + 每次场景加载幂等补扫 OC2DIYLevel/levels/*/runtime）");
        }

        private void Update()
        {
            if (_startupScanDone)
                return;
            _startupScanDone = true;
            try
            {
                ScanOnce(true);
            }
            catch (Exception ex)
            {
                _log.LogWarning("启动扫描异常: " + ex);
            }
        }

        /// <summary>幂等扫描：跳过已加载过的 runtime bundle。verbose=false（场景加载
        /// 补扫）时只在发现新内容才输出日志，避免刷屏。</summary>
        private static void ScanOnce(bool verbose)
        {
            var levelsRoot = Path.Combine(Path.Combine(Paths.PluginPath, "OC2DIYLevel"), "levels");
            if (!Directory.Exists(levelsRoot))
            {
                if (verbose)
                    _log.LogInfo("levels 目录不存在（未装 OC2DIYLevel 或无自定义关卡）: " + levelsRoot);
                return;
            }
            if (verbose)
                _log.LogInfo("扫描关卡集目录: " + levelsRoot);

            var setCount = 0;
            var withRuntime = 0;
            var newlyLoaded = 0;
            foreach (var setDir in Directory.GetDirectories(levelsRoot))
            {
                setCount++;
                var setName = Path.GetFileName(setDir);
                var runtimeBundle = Path.Combine(setDir, "runtime");
                if (!File.Exists(runtimeBundle))
                {
                    if (verbose)
                        _log.LogInfo("  [" + setName + "] 无 runtime 文件（该关卡集不含关卡代码）");
                    continue;
                }
                withRuntime++;
                if (LoadedBundles.Contains(runtimeBundle))
                    continue;
                try
                {
                    _log.LogInfo("  [" + setName + "] 加载 runtime bundle: " + runtimeBundle
                        + "（" + new FileInfo(runtimeBundle).Length + " 字节）");
                    var bundle = AssetBundle.LoadFromFile(runtimeBundle);
                    if (bundle == null)
                    {
                        _log.LogWarning("  [" + setName + "] runtime bundle 加载失败（LoadFromFile 返回 null，可能非 bundle 文件或版本不兼容）: " + runtimeBundle);
                        continue;
                    }
                    LoadedBundles.Add(runtimeBundle);
                    // 普通 bundle 不能 Unload（场景组件的类型还活在其中加载的程序集里）。
                    // 按资产路径匹配而非 TextAsset.name：Unity 导入 .bytes 时将其视为
                    // TextAsset 扩展名并剥掉（Stub_x.dll.bytes → 资产名 Stub_x.dll），
                    // 用 asset.name.EndsWith(".dll.bytes") 会永远漏匹配（v1.2.0 及之前的
                    // 静默 bug）；GetAllAssetNames 的路径保留完整文件名。
                    var dllCount = 0;
                    foreach (var assetPath in bundle.GetAllAssetNames())
                    {
                        if (!assetPath.EndsWith(".dll.bytes", StringComparison.OrdinalIgnoreCase))
                        {
                            _log.LogInfo("  [" + setName + "]   跳过非 DLL 资产: " + assetPath);
                            continue;
                        }
                        var asset = bundle.LoadAsset<TextAsset>(assetPath);
                        if (asset == null || asset.bytes == null || asset.bytes.Length == 0)
                        {
                            _log.LogWarning("  [" + setName + "]   DLL 资产读取失败（非 TextAsset 或空）: " + assetPath);
                            continue;
                        }
                        dllCount++;
                        _log.LogInfo("  [" + setName + "] 发现 " + assetPath + "（" + asset.bytes.Length + " 字节）");
                        if (LoadFromBytes(assetPath, asset.bytes))
                            newlyLoaded++;
                    }
                    if (dllCount == 0)
                    {
                        _log.LogWarning("  [" + setName + "] runtime bundle 内没有任何 *.dll.bytes 资产（打错包或旧包？）。bundle 内全部资产: "
                            + string.Join(", ", bundle.GetAllAssetNames()));
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning("  [" + setName + "] runtime 处理异常: " + ex);
                }
            }
            // 汇总：启动扫描必打；场景补扫只在发现新程序集时打（避免每次切场景刷屏）
            if (verbose || newlyLoaded > 0)
                _log.LogInfo("扫描汇总: 关卡集 " + setCount + " 个，含 runtime " + withRuntime + " 个，本次新加载程序集 " + newlyLoaded + " 个");
        }

        /// <summary>返回 true 表示本次真正完成了程序集加载（重复跳过/失败返回 false）。</summary>
        private static bool LoadFromBytes(string displayName, byte[] raw)
        {
            try
            {
                var asm = Assembly.Load(raw);
                var name = asm.GetName().Name;
                if (LoadedNames.Contains(name))
                {
                    _log.LogInfo("程序集已加载过，跳过重复加载: " + name);
                    return false;
                }
                LoadedNames.Add(name);
                PendingRaw.Remove(raw);
                // 内容自检：确认关卡程序集里确有 CustomStub.RandomCrate
                var crateType = asm.GetType("CustomStub.RandomCrate", false);
                int typeCount;
                try { typeCount = asm.GetTypes().Length; }
                catch { typeCount = -1; }
                _log.LogInfo("已加载关卡程序集: " + name + "（来自 " + displayName + "，类型数 "
                    + (typeCount >= 0 ? typeCount.ToString() : "未知") + "）"
                    + (crateType != null ? "，CustomStub.RandomCrate ✓" : "，⚠ 未找到 CustomStub.RandomCrate（旧版或空程序集）"));
                return true;
            }
            catch (Exception ex)
            {
                PendingRaw.Add(raw);
                _log.LogWarning("Assembly.Load 失败（保留字节供 AssemblyResolve 兜底）" + displayName + ": " + ex);
                return false;
            }
        }

        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var simpleName = args.Name.Split(',')[0].Trim();
            foreach (var raw in PendingRaw)
            {
                try
                {
                    var asm = Assembly.Load(raw);
                    if (asm.GetName().Name == simpleName)
                    {
                        PendingRaw.Remove(raw);
                        LoadedNames.Add(simpleName);
                        if (_log != null)
                            _log.LogInfo("AssemblyResolve 兜底加载: " + simpleName);
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

        #region 场景加载补扫 + 自愈（MonoScript 解析失败的保险）

        private void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoadedHeal;
        }

        /// <summary>每次场景加载：先幂等补扫 runtime（拾漏：启动后才安装/更新的关卡集），
        /// 再检测随机箱数据载体（tag 前缀 RandomCrate|）而组件缺失的场景对象，动态
        /// AddComponent 并从载体回填数据。覆盖：MonoScript 解析失败成 Missing Script、
        /// 场景烘焙缺失、关卡后装等一切"载体在组件不在"的情况。</summary>
        private static void OnSceneLoadedHeal(Scene scene, LoadSceneMode mode)
        {
            try
            {
                ScanOnce(false);
                HealScene(scene);
            }
            catch (Exception ex)
            {
                if (_log != null)
                    _log.LogWarning("场景补扫/自愈异常: " + ex.Message);
            }
        }

        private static void HealScene(Scene scene)
        {
            var crateType = FindLoadedType("CustomStub.RandomCrate");
            if (crateType == null)
            {
                _log.LogInfo("自愈检查 [" + scene.name + "]: AppDomain 中无 CustomStub.RandomCrate 类型"
                    + "（无任何关卡 runtime 程序集被加载），跳过自愈");
                return;
            }
            var itemField = crateType.GetField("m_itemSOs");
            var weightField = crateType.GetField("m_weights");
            var textureField = crateType.GetField("m_questionMarkTexture");
            if (itemField == null || weightField == null)
            {
                _log.LogWarning("自愈检查 [" + scene.name + "]: CustomStub.RandomCrate 字段缺失"
                    + "（m_itemSOs=" + (itemField != null) + "，m_weights=" + (weightField != null)
                    + "），程序集版本不匹配？");
                return;
            }

            var tagCount = 0;
            var healed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                MonoBehaviour[] behaviours;
                try
                {
                    behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
                }
                catch
                {
                    continue;
                }
                if (behaviours == null)
                    continue;

                foreach (var mb in behaviours)
                {
                    if (mb == null)
                        continue; // Missing Script（脚本解析失败的残留）
                    var t = mb.GetType();
                    if (t.Name != "SpecificPseudoPrefabTag")
                        continue;
                    var tagField = t.GetField("prefabTag");
                    var tagValue = tagField != null ? tagField.GetValue(mb) as string : null;
                    if (string.IsNullOrEmpty(tagValue) ||
                        !tagValue.StartsWith("RandomCrate|", StringComparison.Ordinal))
                        continue;
                    tagCount++;
                    var go = mb.gameObject;
                    if (go.GetComponent(crateType) != null)
                    {
                        _log.LogInfo("自愈检查 [" + scene.name + "]: " + GetPath(go) + " 已有 RandomCrate 组件，跳过");
                        continue;
                    }

                    Component comp;
                    try
                    {
                        comp = go.AddComponent(crateType);
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning("自愈检查 [" + scene.name + "]: " + GetPath(go) + " AddComponent(RandomCrate) 异常: " + ex.Message);
                        continue;
                    }
                    // 候选列表：同物体 PseudoPrefabSOArray.pseudoPrefabSOs（数组实例直接回填）
                    var candidateCount = 0;
                    var foundCarrier = false;
                    foreach (var sibling in go.GetComponents<MonoBehaviour>())
                    {
                        if (sibling == null)
                            continue;
                        var st = sibling.GetType();
                        if (st.Name != "PseudoPrefabSOArray")
                            continue;
                        var sosField = st.GetField("pseudoPrefabSOs");
                        if (sosField != null)
                        {
                            var sos = sosField.GetValue(sibling);
                            if (sos != null)
                            {
                                itemField.SetValue(comp, sos);
                                foundCarrier = true;
                                var arr = sos as Array;
                                candidateCount = arr != null ? arr.Length : 0;
                            }
                        }
                        break;
                    }
                    // 权重：从 tag 解析（v2: RandomCrate|<iconGuid>|<w1,w2,...>；旧版两段式）
                    var payload = tagValue.Substring("RandomCrate|".Length);
                    var csv = payload;
                    if (payload.StartsWith("|", StringComparison.Ordinal))
                    {
                        var seg = payload.Substring(1).Split('|');
                        csv = seg.Length > 0 ? seg[0] : "";
                    }
                    else if (payload.IndexOf('|') >= 0)
                    {
                        var seg = payload.Split('|');
                        csv = seg.Length > 1 ? seg[1] : "";
                    }
                    var parts = csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var sosArray = itemField.GetValue(comp) as Array;
                    var count = sosArray != null ? sosArray.Length : parts.Length;
                    var weights = new float[count];
                    for (int i = 0; i < count; i++)
                    {
                        weights[i] = 5f;
                        if (i < parts.Length)
                        {
                            float w;
                            if (float.TryParse(parts[i], out w) && w >= 1f)
                                weights[i] = w;
                        }
                    }
                    weightField.SetValue(comp, weights);
                    // 问号贴图无法从 guid 反查（游戏侧无 AssetDatabase），留空 →
                    // 组件回落为不画问号（保留原版图标）；随机逻辑不受影响。
                    if (textureField != null)
                        textureField.SetValue(comp, null);
                    healed++;
                    _log.LogInfo("自愈检查 [" + scene.name + "]: 已挂载 RandomCrate → " + GetPath(go)
                        + "（候选 " + candidateCount + (foundCarrier ? "" : "，⚠ 未找到 PseudoPrefabSOArray 载体")
                        + "，权重 " + string.Join(",", ToStringArray(weights)) + "）");
                }
            }
            if (healed > 0)
                _log.LogInfo("场景自愈: 动态挂载 RandomCrate × " + healed + "（" + scene.name + "，MonoScript 未解析或未烘焙）");
            else if (tagCount > 0)
                _log.LogInfo("自愈检查 [" + scene.name + "]: 发现 " + tagCount + " 个 RandomCrate 载体，均无需挂载");
        }

        private static string[] ToStringArray(float[] values)
        {
            var result = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = values[i].ToString("0.##");
            return result;
        }

        private static string GetPath(GameObject go)
        {
            var path = go.name;
            var t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }

        /// <summary>关卡程序集（RandomCrate 等）的日志桥：它们编不进 BepInEx 插件
        /// 程序集，Debug.Log 又可能被过滤——反射调用本方法转发到插件日志通道，
        /// 与 loader 日志在同一 [OC2 LevelRuntime Loader] 来源下可见。</summary>
        public static void LogFromCrate(string message, bool warn)
        {
            if (_log == null)
                return;
            if (warn)
                _log.LogWarning(message);
            else
                _log.LogInfo(message);
        }

        private static Type FindLoadedType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null)
                    return t;
            }
            return null;
        }

        #endregion
    }
}
