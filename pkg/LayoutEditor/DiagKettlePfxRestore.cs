using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 热水器粒子原版参数还原（AYCE s_birthday_1_3 实测值，C#4 兼容）。
///
/// 背景：DiagKettleCompat 的 2017 兼容模板把 PFX_Steam/PFX_Drip 的 InitialModule
/// 等重置为 Unity 默认（startLifetime 5s / startSpeed 5 / startSize 1），蒸汽窜
/// 20+ 米、与原版表现完全不符；且 2019 原版 198 文档无法直接反序列化。
///
/// 做法：
///  1. 文本层：用 DiagKettleCompat 留存的 2017 规范模板（logs/ps_ref_2017_full
///     .prefab.txt）替换两个 198 文档，恢复可加载态；
///  2. 数值层：实例化 prefab，按 AYCE 场景提取的数值表用 ParticleSystem API
///     逐模块设置（Initial/Shape/Emission/Size/Color/LimitVel/Force），
///     ReplacePrefab 固化（Unity 原生 2017 序列化，一劳永逸）；
///  3. 验证：重载 prefab 回读关键值写 logs/kettle_pfx_restore_result.txt。
///
/// 数值来源：Ripped s_birthday_1_3.unity Kettle_dispenser → PFX_Steam(198&2254) /
/// PFX_Drip(198&2250)。
/// </summary>
public static class DiagKettlePfxRestore
{
    private const string KettlePath = "Assets/commonW5/prefabs/kettle/web_kettle_hotwater.prefab";
    private const string ResultPath = "logs/kettle_pfx_restore_result.txt";

    private static StringBuilder sb;

    [MenuItem("Layout Editor/诊断/热水器粒子原版参数还原")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        sb = new StringBuilder();
        try
        {
            DoRun();
        }
        catch (Exception e)
        {
            sb.AppendLine("EXCEPTION: " + e);
            Debug.LogError("[PFX-Restore] exception: " + e);
        }
        try
        {
            Directory.CreateDirectory("logs");
            File.WriteAllText(ResultPath, sb.ToString());
        }
        catch (Exception e)
        {
            Debug.LogError("[PFX-Restore] write result failed: " + e.Message);
        }
        Debug.Log("[PFX-Restore] done -> " + ResultPath);
    }

    private static void DoRun()
    {
        // 纯 API 路线：prefab 已被 ReplacePrefab 重序列化（Unity 2017 原生），
        // PFX 子物体若丢失 ParticleSystem 组件（旧 2019 文档导入失败被移除的
        // 遗留）则 AddComponent 重建，再按 AYCE 数值表设置，ReplacePrefab 固化。
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(KettlePath);
        if (asset == null)
        {
            sb.AppendLine("ERROR: prefab 加载失败");
            return;
        }
        GameObject tmp = (GameObject)UnityEngine.Object.Instantiate(asset);
        try
        {
            ApplyPfx(tmp.transform.Find("PFX_Steam"), true);
            ApplyPfx(tmp.transform.Find("PFX_Drip"), false);
#pragma warning disable 618
            PrefabUtility.ReplacePrefab(tmp, asset);
#pragma warning restore 618
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tmp);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(KettlePath, ImportAssetOptions.ForceUpdate);
        sb.AppendLine("values applied & prefab saved");

        // ---------- 验证回读 ----------
        GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(KettlePath);
        foreach (ParticleSystem ps in reloaded.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            sb.AppendLine(ps.gameObject.name + ": life=" +
                main.startLifetime.constantMin + "~" + main.startLifetime.constantMax +
                " speed=" + main.startSpeed.constantMin + "~" + main.startSpeed.constantMax +
                " size=" + main.startSize.constantMin + "~" + main.startSize.constantMax +
                " grav=" + main.gravityModifier.constant +
                " shape=" + ps.shape.shapeType +
                " rate=" + ps.emission.rateOverTime.constantMin + "~" + ps.emission.rateOverTime.constantMax +
                " sizeOL=" + ps.sizeOverLifetime.size.curve.keys.Length + "keys" +
                " colorOL=" + ps.colorOverLifetime.color.gradient.alphaKeys.Length + "alpha" +
                " dampen=" + ps.limitVelocityOverLifetime.dampen +
                " forceY=" + (ps.forceOverLifetime.enabled
                    ? ps.forceOverLifetime.y.curve.keys.Length + "keys" : "off"));
        }
        sb.AppendLine("ps count = " + reloaded.GetComponentsInChildren<ParticleSystem>(true).Length);
    }

    private static void ApplyPfx(Transform t, bool steam)
    {
        if (t == null)
        {
            sb.AppendLine("WARN: PFX 节点缺失");
            return;
        }
        ParticleSystem ps = t.GetComponent<ParticleSystem>();
        if (ps == null)
        {
            ps = t.gameObject.AddComponent<ParticleSystem>();
            sb.AppendLine("re-added ParticleSystem: " + t.name);
        }
        if (t.GetComponent<ParticleSystemRenderer>() == null)
            t.gameObject.AddComponent<ParticleSystemRenderer>();
        if (steam)
            ApplySteam(ps);
        else
            ApplyDrip(ps);
    }

    // ---- AYCE 数值表（s_birthday_1_3 实测） ----

    private static void ApplySteam(ParticleSystem ps)
    {
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
        main.startSize = new ParticleSystem.MinMaxCurve(1f, 1.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-1.0471976f, 1.0471976f);
        main.gravityModifier = 0.05f;
        main.maxParticles = 100;

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 15f;
        sh.length = 5f;
        sh.radius = 0.25f;

        ParticleSystem.EmissionModule em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 10f;

        ParticleSystem.SizeOverLifetimeModule so = ps.sizeOverLifetime;
        so.enabled = true;
        so.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.49230736f, 3.9127252f, 3.9127252f),
            new Keyframe(1f, 1f, 0f, 0f)));

        ParticleSystem.ColorOverLifetimeModule co = ps.colorOverLifetime;
        co.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.93333334f, 0.8f), 1f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(0.078431375f, 1f),
            });
        co.color = new ParticleSystem.MinMaxGradient(g);

        ParticleSystem.LimitVelocityOverLifetimeModule lv = ps.limitVelocityOverLifetime;
        lv.enabled = true;
        lv.dampen = 0.25f;
        lv.drag = 1f;
    }

    private static void ApplyDrip(ParticleSystem ps)
    {
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = 1f;
        main.startSpeed = 0f;
        main.startSize = 0.08f;
        main.maxParticles = 100;

        ParticleSystem.EmissionModule em = ps.emission;
        em.enabled = true;
        em.rateOverTime = new ParticleSystem.MinMaxCurve(0.25f, 1.5f);

        // 原版 Drip 无 Shape 模块（AddComponent 默认开锥形，需关闭）
        ParticleSystem.ShapeModule shp = ps.shape;
        shp.enabled = false;

        ParticleSystem.SizeOverLifetimeModule so = ps.sizeOverLifetime;
        so.enabled = true;
        so.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.38509318f, 2.89931f, 2.89931f),
            new Keyframe(0.5009027f, 1f, 0f, 0f)));

        ParticleSystem.ColorOverLifetimeModule co = ps.colorOverLifetime;
        co.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.93333334f, 0.8f), 1f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.78431374f, 0.25f),
                new GradientAlphaKey(0.5019608f, 0.9093808f),
                new GradientAlphaKey(0.078431375f, 1f),
            });
        co.color = new ParticleSystem.MinMaxGradient(g);

        ParticleSystem.ForceOverLifetimeModule fo = ps.forceOverLifetime;
        fo.enabled = true;
        fo.x = 1f;
        fo.z = 1f;
        fo.y = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.0124223605f, -5.0694118f, -0.048879247f),
            new Keyframe(0.4f, -1f, 0f, 0f)));
    }

    // ---- 文本工具 ----

    private static string FindPsBlockByGo(string text, string goFid)
    {
        MatchCollection matches = Regex.Matches(text,
            @"^--- !u!198 &\d+\nParticleSystem:\n(?:(?!^--- ).*\n)*",
            RegexOptions.Multiline);
        foreach (Match m in matches)
        {
            string head = m.Value.Substring(0, Math.Min(m.Value.Length, 400));
            if (head.Contains("m_GameObject: {fileID: " + goFid + "}"))
                return m.Value;
        }
        return null;
    }

    private static string ExtractBody(string text, string cls)
    {
        Match m = Regex.Match(text,
            @"^--- !u!" + cls + @" &\d+\n\w+:\n((?:  .*\n)+)",
            RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value : null;
    }
}
