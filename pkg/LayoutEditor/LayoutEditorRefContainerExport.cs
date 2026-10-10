using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using LevelEditorStub;

/// <summary>
/// 导出 web 3D 预览用的参考容器 OBJ（盘子 / 玻璃杯）到 layout-editor/web/public/models/。
/// 几何与 BundleDumper / 桌台皮肤导出一致（X 取反、三角绕序反转），供 three.js OBJLoader 使用。
/// modelPreview.ts 通过 position.y = centerY 将包围盒中心对齐原点（盘 -0.0643、杯 -0.398）。
/// 菜单：Layout Editor → 素材导出 → 导出 3D 预览参考容器 (Export Ref Plate/Glass)。
/// </summary>
public static class LayoutEditorRefContainerExport
{
    private struct RefSpec
    {
        public string label;
        public string soAssetPath;
        public string outFileName;
    }

    private static readonly RefSpec[] Specs =
    {
        new RefSpec
        {
            label = "盘子 (equipment_plate_01)",
            soAssetPath = "Assets/common01/pseudo_prefab_so/utensils/PlateSO.asset",
            outFileName = "ref_plate.obj",
        },
        new RefSpec
        {
            label = "玻璃杯 (equipment_glass_01)",
            soAssetPath = "Assets/common02/pseudo_prefab_so/utensils/equipment_glass_01.asset",
            outFileName = "ref_glass.obj",
        },
    };

    [MenuItem(
        "Layout Editor/素材导出 (Asset Export)/导出 3D 预览参考容器 (Export Ref Plate/Glass)",
        false,
        2)]
    public static void ExportAll()
    {
        string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string outDir = Path.Combine(repoRoot, "layout-editor/web/public/models").Replace("\\", "/");
        Directory.CreateDirectory(outDir);

        var own = new List<AssetBundle>();
        var bundles = new Dictionary<string, AssetBundle>();
        AssetBundleManifest manifest = LayoutEditorCounterSkinIconExport.LoadManifest(bundles, own);

        var report = new StringBuilder();
        int ok = 0;
        try
        {
            for (int i = 0; i < Specs.Length; i++)
            {
                RefSpec spec = Specs[i];
                EditorUtility.DisplayProgressBar("参考容器导出", spec.label, (float)i / Specs.Length);
                string err = ExportOne(outDir, spec, bundles, own, manifest);
                if (string.IsNullOrEmpty(err)) ok++;
                else report.Append("\n").Append(spec.outFileName).Append(": ").Append(err);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            foreach (AssetBundle b in own) b.Unload(true);
            bundles.Clear();
        }

        string summary = "成功 " + ok + "/" + Specs.Length + " → " + outDir +
                         "\n请执行 npm --prefix layout-editor/web run build 同步 dist/models，并提交 public（及 dist）。";
        if (report.Length > 0) summary += "\n失败:" + report;
        Debug.Log("[LayoutEditor/Export] 参考容器 | " + summary.Replace("\n", " | "));
        EditorUtility.DisplayDialog("参考容器导出", summary, "确定");
    }

    private static string ExportOne(
        string outDir,
        RefSpec spec,
        Dictionary<string, AssetBundle> bundles,
        List<AssetBundle> own,
        AssetBundleManifest manifest)
    {
        var so = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(spec.soAssetPath);
        if (so == null) return "未找到 SO: " + spec.soAssetPath;

        GameObject inst = null;
        try
        {
            AssetBundle bundle = LayoutEditorCounterSkinIconExport.EnsureBundleLoaded(
                so.bundleName, bundles, own, manifest);
            if (bundle == null) return "bundle 加载失败: " + so.bundleName;
            var prefab = bundle.LoadAsset<GameObject>(so.assetPath);
            if (prefab == null) return "prefab 加载失败: " + so.assetPath;

            Vector3 offset = new Vector3(-5000f, 0f, -5000f);
            inst = (GameObject)UnityEngine.Object.Instantiate(prefab, offset, Quaternion.identity);

            var renderers = inst.GetComponentsInChildren<Renderer>(false);
            var list = new List<Renderer>();
            foreach (Renderer r in renderers)
                if (r != null && r.enabled) list.Add(r);
            if (list.Count == 0) return "无可渲染网格";

            Bounds bounds = list[0].bounds;
            for (int i = 1; i < list.Count; i++) bounds.Encapsulate(list[i].bounds);

            var obj = new StringBuilder();
            obj.Append("# OC2 ref container ").Append(spec.outFileName).Append('\n');
            int vBase = 0;
            for (int ri = 0; ri < list.Count; ri++)
            {
                Renderer r = list[ri];
                Mesh mesh = MeshOf(r);
                if (mesh == null) continue;
                Matrix4x4 m = r.transform.localToWorldMatrix;
                Vector3[] verts = mesh.vertices;
                int vCount = verts.Length;
                for (int vi = 0; vi < vCount; vi++)
                {
                    Vector3 v = m.MultiplyPoint3x4(verts[vi]) - offset;
                    obj.Append("v ").Append(Fmt(-v.x)).Append(' ').Append(Fmt(v.y)).Append(' ')
                        .Append(Fmt(v.z)).Append('\n');
                }
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    obj.Append("g ").Append(r.name).Append('_').Append(s).Append('\n');
                    int[] tris = mesh.GetTriangles(s);
                    for (int ti = 0; ti < tris.Length; ti += 3)
                    {
                        int a = tris[ti] + 1 + vBase;
                        int b = tris[ti + 1] + 1 + vBase;
                        int c = tris[ti + 2] + 1 + vBase;
                        obj.Append("f ").Append(c).Append(' ').Append(b).Append(' ').Append(a).Append('\n');
                    }
                }
                vBase += vCount;
            }

            string outPath = Path.Combine(outDir, spec.outFileName).Replace("\\", "/");
            File.WriteAllText(outPath, obj.ToString(), new UTF8Encoding(false));

            Vector3 size = bounds.size;
            Vector3 center = bounds.center - offset;
            float dia = Mathf.Max(size.x, size.z);
            Debug.Log("[LayoutEditor/Export] " + spec.outFileName +
                      " φ=" + Fmt(dia * 100f) + " cm 高=" + Fmt(size.y * 100f) +
                      " cm centerY=" + Fmt(center.y) + " m → modelPreview centerY≈" +
                      Fmt(-center.y));
            return null;
        }
        catch (Exception ex)
        {
            return ex.GetType().Name + ": " + ex.Message;
        }
        finally
        {
            if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
        }
    }

    private static Mesh MeshOf(Renderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) return mf.sharedMesh;
        var smr = r as SkinnedMeshRenderer;
        if (smr != null && smr.sharedMesh != null) return smr.sharedMesh;
        return null;
    }

    private static string Fmt(float f)
    {
        if (float.IsNaN(f) || float.IsInfinity(f)) return "0";
        return f.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
