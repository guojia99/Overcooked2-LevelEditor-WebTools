using LevelEditorStub;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 主网格半宽烘焙：把 LevelInfoSO.gridHalfSizeX/Z 写进场景
/// CampaignGameEnvironment/GridManager 上的 QuadGridManager.m_gridHalfSize，
/// 经 SerializedObject 形成 prefab instance 属性覆盖，随 SaveScene / 导出 bundle
/// 持久化（写回与导出两处调用，与 LayoutEditorHudOrderLimits 同范式）。
///
/// 语义：网格以 GridManager 位置为中心向两侧各扩展 gridHalfSize 格（总格数 2N+1），
/// 需覆盖全部工作台；X/Z 每轴独立，0 = 不调整该轴（保持场景现值），Y 不动（实测恒 1）。
/// 字段双形态：游戏程序集内 QuadGridManager 的 m_gridHalfSize 为自定义结构
/// （.X/.Y/.Z int 相对属性）或 Vector3 —— 与 LayoutEditorGridReader 读法一致。
/// </summary>
public static class LayoutEditorGridBake
{
    /// <summary>对当前激活场景烘焙主网格半宽覆盖；返回警告文本（null = 正常 / 无需烘焙）。</summary>
    public static string BakeActiveScene()
    {
        var stub = Object.FindObjectOfType<PseudoPrefabManagerStub>();
        if (stub == null || stub.levelInfo == null)
            return "[GridBake] 场景缺少 PseudoPrefabManagerStub/levelInfo，跳过网格半宽烘焙";

        var info = stub.levelInfo;
        var halfX = Mathf.Clamp(info.gridHalfSizeX, 0, 50);
        var halfZ = Mathf.Clamp(info.gridHalfSizeZ, 0, 50);
        if (halfX <= 0 && halfZ <= 0)
            return null; // 未设置：不动场景

        var grid = FindMainGridManager();
        if (grid == null)
            return "[GridBake] 未找到 CampaignGameEnvironment/GridManager 上的 QuadGridManager，跳过网格半宽烘焙";

        var so = new SerializedObject(grid);
        so.Update();

        var half = so.FindProperty("m_gridHalfSize");
        if (half == null)
            half = so.FindProperty("gridHalfSize");
        if (half == null)
            return "[GridBake] QuadGridManager 序列化字段缺失（脚本版本异常），跳过网格半宽烘焙";

        var x = half.FindPropertyRelative("X");
        var z = half.FindPropertyRelative("Z");
        if (x != null && z != null)
        {
            if (halfX > 0)
                x.intValue = halfX;
            if (halfZ > 0)
                z.intValue = halfZ;
        }
        else if (half.propertyType == SerializedPropertyType.Vector3)
        {
            var v = half.vector3Value;
            if (halfX > 0)
                v.x = halfX;
            if (halfZ > 0)
                v.z = halfZ;
            half.vector3Value = v;
        }
        else
        {
            return "[GridBake] m_gridHalfSize 形态无法识别（既无 .X/.Z 相对属性也非 Vector3），跳过";
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return null;
    }

    /// <summary>定位主网格组件：根对象 CampaignGameEnvironment → 子 GridManager →
    /// QuadGridManager（游戏程序集类型，按类型名匹配）。</summary>
    private static MonoBehaviour FindMainGridManager()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return null;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == null || root.name != "CampaignGameEnvironment")
                continue;
            var t = root.transform.Find("GridManager");
            if (t == null)
                continue;
            foreach (var mb in t.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "QuadGridManager")
                    return mb;
            }
        }
        return null;
    }

    // ---- 场景磁盘值读取（关卡配置弹窗回填「实际值」用；无需打开场景） ----

    private const string EnvPrefabFallbackPath =
        "Assets/common01/game_environment/CampaignGameEnvironment.prefab";

    // 覆盖行形态：target 行的长 fileID 会被 YAML 折行（"guid: G,\n        type: 2}"），
    // 因此各段之间用有界 [\s\S] 窗口而非固定空白。
    private static readonly Regex GridOverrideRe = new Regex(
        "target: \\{fileID: -?\\d+, guid: ([0-9a-f]{32}),[\\s\\S]{0,30}?type: 2\\}"
        + "[\\s\\S]{0,30}?propertyPath: m_gridHalfSize\\.([XZ])[\\s\\S]{0,30}?value: (-?\\d+)",
        RegexOptions.Multiline);

    private static readonly Regex PrefabGridRe = new Regex(
        "m_gridHalfSize:\\s*\\r?\\n\\s*X:\\s*(-?\\d+)\\s*\\r?\\n\\s*Y:\\s*(-?\\d+)\\s*\\r?\\n\\s*Z:\\s*(-?\\d+)");

    /// <summary>从场景磁盘文件解析主网格（CampaignGameEnvironment/GridManager）当前生效的
    /// m_gridHalfSize：场景 prefab 覆盖优先，无覆盖回退 env prefab 默认值。
    /// 解析不到（非模板场景等）返回 0/0。</summary>
    public static void ReadSceneMainGridHalfSize(string sceneAssetPath, out int halfX, out int halfZ)
    {
        halfX = 0;
        halfZ = 0;
        if (string.IsNullOrEmpty(sceneAssetPath))
            return;

        // Unity 2017 的 .NET 无 Path.Combine 3 参重载，须链式两参。
        var abs = Path.GetFullPath(Path.Combine(Path.Combine(Application.dataPath, ".."), sceneAssetPath));
        if (!File.Exists(abs))
            return;

        string text;
        try
        {
            text = File.ReadAllText(abs);
        }
        catch
        {
            return;
        }

        var envGuid = "";
        foreach (Match m in GridOverrideRe.Matches(text))
        {
            var guid = m.Groups[1].Value;
            if (!IsEnvPrefabGuid(guid))
                continue;
            envGuid = guid;
            var v = 0;
            int.TryParse(m.Groups[3].Value, out v);
            if (m.Groups[2].Value == "X")
                halfX = v;
            else
                halfZ = v;
        }
        if (halfX > 0 && halfZ > 0)
            return;

        // 无覆盖的轴回退 env prefab 默认值（common01 本体为 20/1/20）。
        var prefabPath = !string.IsNullOrEmpty(envGuid)
            ? AssetDatabase.GUIDToAssetPath(envGuid)
            : EnvPrefabFallbackPath;
        int defX, defZ;
        ReadPrefabDefaultGridHalfSize(prefabPath, out defX, out defZ);
        if (halfX <= 0)
            halfX = defX;
        if (halfZ <= 0)
            halfZ = defZ;
    }

    /// <summary>guid 是否指向 CampaignGameEnvironment prefab（平台网格等其它 prefab 的
    /// m_gridHalfSize 覆盖会被同样的正则命中，必须按 guid 路径区分）。</summary>
    private static bool IsEnvPrefabGuid(string guid)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        return !string.IsNullOrEmpty(path)
            && path.EndsWith("CampaignGameEnvironment.prefab", System.StringComparison.Ordinal);
    }

    private static void ReadPrefabDefaultGridHalfSize(string prefabPath, out int halfX, out int halfZ)
    {
        halfX = 0;
        halfZ = 0;
        if (string.IsNullOrEmpty(prefabPath))
            return;
        var abs = Path.GetFullPath(Path.Combine(Path.Combine(Application.dataPath, ".."), prefabPath));
        if (!File.Exists(abs))
            return;
        try
        {
            var m = PrefabGridRe.Match(File.ReadAllText(abs));
            if (m.Success)
            {
                int.TryParse(m.Groups[1].Value, out halfX);
                int.TryParse(m.Groups[3].Value, out halfZ);
            }
        }
        catch
        {
        }
    }
}
