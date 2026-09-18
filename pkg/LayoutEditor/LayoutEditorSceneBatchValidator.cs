using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LayoutEditor
{
    /// <summary>
    /// 关卡集场景批量自检：逐关打开场景，统计物件/缺失引用，并把控制台报错收进报告。
    /// 用途是重建关卡后一次性确认「Unity 能正常打开、没有丢引用」。
    ///
    /// 命令行：
    ///   Unity -batchmode -quit -projectPath &lt;proj&gt; \
    ///     -executeMethod LayoutEditor.LayoutEditorSceneBatchValidator.RunFromCommandLine \
    ///     -oc2sets oc2_dlc_story,oc2_story -oc2out /tmp/scene_validate.json
    /// </summary>
    public static class LayoutEditorSceneBatchValidator
    {
        private const string DefaultOutput = "layout-editor/scripts/oc2-rebuild/out/unity_scene_check.json";

        private static readonly List<string> s_errors = new List<string>();
        private static bool s_capturing;

        [MenuItem("OC2 Layout/校验/批量打开关卡集场景")]
        public static void RunMenu()
        {
            var sets = new List<string> { "oc2_dlc_story", "oc2_story" };
            Run(sets, Path.Combine(ProjectRoot(), DefaultOutput));
        }

        public static void RunFromCommandLine()
        {
            var sets = new List<string>();
            string output = Path.Combine(ProjectRoot(), DefaultOutput);
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-oc2sets" && i + 1 < args.Length)
                {
                    string[] parts = args[i + 1].Split(',');
                    for (int k = 0; k < parts.Length; k++)
                    {
                        string p = parts[k].Trim();
                        if (p.Length > 0) sets.Add(p);
                    }
                }
                else if (args[i] == "-oc2out" && i + 1 < args.Length)
                {
                    output = args[i + 1];
                }
            }
            if (sets.Count == 0)
            {
                sets.Add("oc2_dlc_story");
                sets.Add("oc2_story");
            }
            Run(sets, output);
        }

        public static void Run(List<string> sets, string outputPath)
        {
            var sb = new StringBuilder();
            sb.Append("{\n \"scenes\": [\n");
            int total = 0;
            int failed = 0;
            bool first = true;

            Application.logMessageReceived += OnLog;
            s_capturing = true;
            try
            {
                for (int si = 0; si < sets.Count; si++)
                {
                    string dir = "Assets/LevelSets/" + sets[si] + "/scenes";
                    if (!Directory.Exists(Path.Combine(ProjectRoot(), dir))) continue;
                    string[] files = Directory.GetFiles(Path.Combine(ProjectRoot(), dir), "*.unity");
                    Array.Sort(files);
                    for (int fi = 0; fi < files.Length; fi++)
                    {
                        string rel = dir + "/" + Path.GetFileName(files[fi]);
                        total++;
                        s_errors.Clear();
                        int objects = 0;
                        int missing = 0;
                        var missingPaths = new List<string>();
                        string err = "";
                        try
                        {
                            Scene sc = EditorSceneManager.OpenScene(rel, OpenSceneMode.Single);
                            GameObject[] roots = sc.GetRootGameObjects();
                            for (int r = 0; r < roots.Length; r++)
                            {
                                Transform[] all = roots[r].GetComponentsInChildren<Transform>(true);
                                objects += all.Length;
                                for (int t = 0; t < all.Length; t++)
                                {
                                    Component[] comps = all[t].GetComponents<Component>();
                                    for (int c = 0; c < comps.Length; c++)
                                    {
                                        if (comps[c] == null)
                                        {
                                            missing++;
                                            if (missingPaths.Count < 8)
                                            {
                                                string pp = HierarchyPath(all[t]);
                                                GameObject src = PrefabUtility.GetPrefabParent(all[t].gameObject) as GameObject;
                                                string from = src == null ? "" : AssetDatabase.GetAssetPath(src);
                                                missingPaths.Add(pp + " <- " + from);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            err = e.Message;
                        }
                        bool ok = err.Length == 0 && missing == 0 && s_errors.Count == 0;
                        if (!ok) failed++;
                        if (!first) sb.Append(",\n");
                        first = false;
                        sb.Append("  {\"scene\": \"").Append(Escape(rel))
                          .Append("\", \"objects\": ").Append(objects)
                          .Append(", \"missingComponents\": ").Append(missing)
                          .Append(", \"ok\": ").Append(ok ? "true" : "false")
                          .Append(", \"error\": \"").Append(Escape(err))
                          .Append("\", \"logs\": [");
                        for (int li = 0; li < s_errors.Count && li < 5; li++)
                        {
                            if (li > 0) sb.Append(", ");
                            sb.Append("\"").Append(Escape(s_errors[li])).Append("\"");
                        }
                        sb.Append("]");
                        sb.Append(", \"missingPaths\": [");
                        for (int li = 0; li < missingPaths.Count; li++)
                        {
                            if (li > 0) sb.Append(", ");
                            sb.Append("\"").Append(Escape(missingPaths[li])).Append("\"");
                        }
                        sb.Append("]}");
                        Debug.Log("[SceneCheck] " + rel + " objects=" + objects
                                  + " missing=" + missing + " logs=" + s_errors.Count
                                  + (ok ? " OK" : " FAIL"));
                    }
                }
            }
            finally
            {
                s_capturing = false;
                Application.logMessageReceived -= OnLog;
            }

            sb.Append("\n ],\n \"total\": ").Append(total)
              .Append(",\n \"failed\": ").Append(failed).Append("\n}\n");
            string dirOut = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dirOut) && !Directory.Exists(dirOut)) Directory.CreateDirectory(dirOut);
            File.WriteAllText(outputPath, sb.ToString());
            Debug.Log("[SceneCheck] 完成：" + total + " 关，失败 " + failed + "，报告 " + outputPath);
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (!s_capturing) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (condition != null && condition.StartsWith("[SceneCheck]")) return;
            if (s_errors.Count < 20) s_errors.Add(condition);
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", " ").Replace("\r", " ").Replace("\t", " ");
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static string HierarchyPath(Transform t)
        {
            string p = t.name;
            Transform cur = t.parent;
            while (cur != null)
            {
                p = cur.name + "/" + p;
                cur = cur.parent;
            }
            return p;
        }
    }
}
