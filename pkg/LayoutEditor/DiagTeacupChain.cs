using System;
using System.Text;
using LevelEditor;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;

/// <summary>
/// commonW5 茶杯洗涤链路（脏茶杯/脏茶杯堆/洗茶杯水槽/茶杯回收台）诊断。
/// 菜单：Layout Editor/诊断/茶杯洗涤链路审计
/// </summary>
public static class DiagTeacupChain
{
    [MenuItem("Layout Editor/诊断/茶杯洗涤链路审计（含 testhotwt 场景）")]
    public static void AuditWithScene()
    {
        Audit();
        var sb = new StringBuilder();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/LevelSets/tester_web/scenes/testhotwt.unity");
        sb.AppendLine("scene loaded: " + scene.name);
        foreach (var root in scene.GetRootGameObjects())
            DumpTree(root.transform, 0, sb);
        string logPath = "logs/teacup_chain_scene_audit.txt";
        System.IO.File.WriteAllText(logPath, sb.ToString());
        Debug.Log(sb.ToString());
    }

    private static void DumpTree(Transform t, int depth, StringBuilder sb)
    {
        var names = new System.Collections.Generic.List<string>();
        foreach (var mb in t.GetComponents<MonoBehaviour>())
            names.Add(mb == null ? "<MISSING>" : mb.GetType().Name);
        sb.AppendLine(new string(' ', depth * 2) + t.name + " [" + string.Join(",", names.ToArray()) + "]");
        foreach (Transform c in t)
            DumpTree(c, depth + 1, sb);
    }

    [MenuItem("Layout Editor/诊断/洗茶杯水槽朝向与包围盒")]
    public static void SinkBounds()
    {
        var sb = new StringBuilder();
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/commonW5/prefabs/counters/web_sink_teacup.prefab");
        if (go == null) { Debug.LogError("sink prefab load failed"); return; }
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            sb.AppendLine(r.gameObject.name + " worldBounds=" + r.bounds +
                " localPos=" + r.transform.position);
        }
        foreach (var b in go.GetComponentsInChildren<BoxCollider>(true))
        {
            sb.AppendLine("BOX " + b.gameObject.name + " center=" + b.center +
                " size=" + b.size + " pos=" + b.transform.position);
        }
        Debug.Log(sb.ToString());
        System.IO.File.WriteAllText("logs/teacup_sink_bounds.txt", sb.ToString());
    }

    [MenuItem("Layout Editor/诊断/茶杯回收台接线审计（当前场景）")]
    public static void AuditTeacupReturns()
    {
        var sb = new StringBuilder();
        var stubs = UnityEngine.Object.FindObjectsOfType<PseudoPrefabPlateReturnStub>();
        int found = 0;
        for (int i = 0; i < stubs.Length; i++)
        {
            var stub = stubs[i];
            if (stub == null)
                continue;
            var go = stub.gameObject;
            if (go == null || go.name.IndexOf("web_teacup_return", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            found++;
            var runtime = go.GetComponent<PseudoPrefab>();
            var derived = go.GetComponent<PseudoPrefabPlateReturn>();
            sb.AppendLine("== " + go.name);
            sb.AppendLine("  runtime=" + (runtime == null ? "<MISSING>" : runtime.GetType().Name)
                + "  plateReturnRuntime=" + (derived != null ? "yes" : "NO"));
            sb.AppendLine("  stubSO=" + (stub.pseudoPrefabSO == null ? "<null>" : stub.pseudoPrefabSO.name)
                + "  returnClean=" + stub.returnClean
                + "  dirtySO=" + (stub.dirtyPlateStackSO == null ? "<null>" : stub.dirtyPlateStackSO.name)
                + "  cleanSO=" + (stub.cleanPlateStackSO == null ? "<null>" : stub.cleanPlateStackSO.name));
            var child = runtime != null ? runtime.childGameObject : null;
            if (child == null)
            {
                foreach (Transform t in go.transform)
                {
                    if (t.name.IndexOf("plate_return", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        child = t.gameObject;
                        break;
                    }
                }
            }
            var station = child != null ? child.GetComponent<PlateReturnStation>() : null;
            sb.AppendLine("  child=" + (child == null ? "<null>" : child.name)
                + "  stackPrefab=" + (station == null || station.m_stackPrefab == null
                    ? "<null>" : station.m_stackPrefab.name));
        }
        string logPath = "logs/teacup_return_audit.txt";
        System.IO.File.WriteAllText(logPath, sb.ToString());
        Debug.Log("[DiagTeacupChain] 茶杯回收台 " + found + " 个，审计写入 " + logPath
            + "\n" + sb);
        EditorUtility.DisplayDialog("茶杯回收台接线审计",
            found == 0 ? "当前场景没有茶杯回收台。" : "已写入 " + logPath, "OK");
    }

    [MenuItem("Layout Editor/诊断/茶杯洗涤链路审计")]
    public static void Audit()
    {
        var sb = new StringBuilder();
        string[] paths =
        {
            "Assets/commonW5/prefabs/teacups/web_teacup_dirty.prefab",
            "Assets/commonW5/prefabs/teacups/web_teacup_stack_dirty.prefab",
            "Assets/commonW5/prefabs/counters/web_sink_teacup.prefab",
            "Assets/commonW5/prefabs/counters/web_teacup_return.prefab",
        };
        foreach (var p in paths)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            sb.AppendLine("== " + p + " -> " + (go == null ? "LOAD FAILED" : "ok"));
            if (go == null) continue;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                sb.AppendLine(string.Format("  [{0}] mesh={1} submeshes={2} mats={3}",
                    r.gameObject.name,
                    mesh == null ? "<null>" : mesh.name,
                    mesh == null ? -1 : mesh.subMeshCount,
                    r.sharedMaterials.Length));
                foreach (var m in r.sharedMaterials)
                    sb.AppendLine("    mat=" + (m == null ? "<null>" : m.name +
                        " shader=" + (m.shader == null ? "<null>" : m.shader.name)));
            }
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null)
                {
                    sb.AppendLine("  MISSING SCRIPT on " + go.name);
                    continue;
                }
                var t = mb.GetType().Name;
                if (t == "DirtyPlateStack" || t == "PlateReturnStation" ||
                    t == "WashingStation" || t == "CleanPlateStack" || t == "Stack")
                {
                    sb.AppendLine("  " + t + ": " + DumpRefs(mb));
                }
            }
        }
        // 关键网格子资产
        string[] meshes =
        {
            "Assets/commonW5/teacup/meshes/m_sk_sink_dlc100_wood.obj",
            "Assets/commonW5/teacup/meshes/m_sk_sink_dlc100_top.obj",
            "Assets/commonW5/teacup/meshes/m_sk_plate_return_01.obj",
            "Assets/commonW5/teacup/meshes/m_dlc2_glassdirt_01.obj",
            "Assets/commonW5/kettle/meshes/m_dlc100_teacup_cup.obj",
        };
        foreach (var m in meshes)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(m);
            sb.AppendLine("MESH " + m + " -> " + (mesh == null ? "<null>" :
                mesh.name + " submeshes=" + mesh.subMeshCount + " verts=" + mesh.vertexCount));
        }
        string logPath = "logs/teacup_chain_audit.txt";
        System.IO.File.WriteAllText(logPath, sb.ToString());
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("茶杯洗涤链路审计", "已写入 " + logPath, "OK");
    }

    private static string DumpRefs(MonoBehaviour mb)
    {
        var sb = new StringBuilder();
        var flags = System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance;
        foreach (var f in mb.GetType().GetFields(flags))
        {
            if (!f.Name.StartsWith("m_")) continue;
            object v;
            try { v = f.GetValue(mb); }
            catch { continue; }
            var uo = v as UnityEngine.Object;
            if (uo != null)
                sb.Append(f.Name + "=" + uo.name + "; ");
            else if (v is Array)
                sb.Append(f.Name + "[" + ((Array)v).Length + "]; ");
            else if (v != null && (v is string || v is ValueType))
                sb.Append(f.Name + "=" + v + "; ");
        }
        return sb.ToString();
    }
}
