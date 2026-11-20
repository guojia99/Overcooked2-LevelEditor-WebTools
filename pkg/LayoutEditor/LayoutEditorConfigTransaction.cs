using System;
using System.Collections.Generic;
using System.IO;
using LevelEditorStub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Request-local rollback for player configs and SyncRecipeData side effects.
/// Preserve both pre-request disk bytes and dirty in-memory assets; never save during rollback.</summary>
public sealed class LayoutEditorConfigTransaction : IDisposable
{
    private readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
    private readonly List<string> trees = new List<string>();
    private readonly HashSet<string> directories = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<ScriptableObject> originals = new List<ScriptableObject>();
    private readonly List<ScriptableObject> copies = new List<ScriptableObject>();
    private readonly List<Scene> scenes = new List<Scene>();
    private readonly List<bool> sceneDirty = new List<bool>();
    private readonly int undoGroup;

    public LayoutEditorConfigTransaction(LevelInfoSO info)
    {
        Undo.IncrementCurrentGroup();
        undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Save player configuration");
        try
        {
            var infoPath = AssetDatabase.GetAssetPath(info);
            var root = Path.GetDirectoryName(infoPath).Replace('\\', '/');
            trees.Add(root);
            foreach (var path in Directory.GetDirectories(root, "*", SearchOption.AllDirectories)) directories.Add(path.Replace('\\', '/'));
            foreach (var path in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) CaptureFile(path);
            // BurgerOptional allocation may update this set-wide counter, even when its own
            // helper calls SaveAssets before returning. Level-local names are covered by root.
            var parts = infoPath.Split('/');
            CaptureAsset("Assets/LevelSets/" + parts[2] + "/custom_recipes/CustomRecipeConfig.asset");
            CaptureFile("Assets/LevelSets/" + parts[2] + "/custom_recipes/names.json");
            CaptureFile("Assets/LevelSets/" + parts[2] + "/custom_recipes/names.json.meta");
            foreach (var id in AssetDatabase.FindAssets("t:ScriptableObject", new[] { root }))
                CaptureAsset(AssetDatabase.GUIDToAssetPath(id));
            CaptureObject(info);
            foreach (var config in LayoutEditorOrderSequencePolicy.Configs(info)) CaptureObject(config);
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                // Sync fill helpers can mark other loaded scenes dirty; retain their flags too.
                scenes.Add(scene); sceneDirty.Add(scene.isDirty);
                var owned = false;
                foreach (var go in scene.GetRootGameObjects())
                    foreach (var manager in go.GetComponentsInChildren<PseudoPrefabManagerStub>(true))
                        if (manager.levelInfo == info) { owned = true; break; }
                if (owned)
                    foreach (var go in scene.GetRootGameObjects())
                        Undo.RegisterFullObjectHierarchyUndo(go, "Save recipe consumers");
            }
        }
        catch { Dispose(); throw; }
    }

    private void CaptureFile(string path)
    {
        path = path.Replace('\\', '/');
        if (!files.ContainsKey(path)) files.Add(path, File.Exists(path) ? File.ReadAllBytes(path) : null);
    }

    private void CaptureAsset(string path)
    {
        CaptureFile(path); CaptureFile(path + ".meta");
        if (File.Exists(path))
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) CaptureObject(asset as ScriptableObject);
    }

    private void CaptureObject(ScriptableObject asset)
    {
        if (asset == null || originals.Contains(asset)) return;
        var copy = UnityEngine.Object.Instantiate(asset);
        copy.name = asset.name;
        originals.Add(asset); copies.Add(copy);
    }

    public void Commit()
    {
        Undo.CollapseUndoOperations(undoGroup);
    }

    public string Rollback()
    {
        var errors = new List<string>();
        try { Undo.RevertAllDownToGroup(undoGroup); }
        catch (Exception ex) { errors.Add("场景 Undo 恢复失败：" + ex.Message); }
        foreach (var root in trees)
        {
            try
            {
                foreach (var path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    var key = path.Replace('\\', '/');
                    if (files.ContainsKey(key)) continue;
                    try { File.Delete(path); }
                    catch (Exception ex) { errors.Add("删除新派生文件失败：" + key + "：" + ex.Message); }
                }
                var created = new List<string>(Directory.GetDirectories(root, "*", SearchOption.AllDirectories));
                created.Sort(delegate(string a, string b) { return b.Length.CompareTo(a.Length); });
                foreach (var path in created)
                    if (!directories.Contains(path.Replace('\\', '/'))) Directory.Delete(path, false);
            }
            catch (Exception ex) { errors.Add("清理新派生目录失败：" + ex.Message); }
        }
        foreach (var file in files)
        {
            try
            {
                if (file.Value == null) { if (File.Exists(file.Key)) File.Delete(file.Key); }
                else File.WriteAllBytes(file.Key, file.Value);
            }
            catch (Exception ex) { errors.Add("恢复文件失败：" + file.Key + "：" + ex.Message); }
        }
        try { AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); }
        catch (Exception ex) { errors.Add("重新导入失败：" + ex.Message); }
        for (int i = 0; i < originals.Count; i++)
        {
            try
            {
                if (originals[i] == null) throw new InvalidOperationException("原资产已被移除。");
                EditorUtility.CopySerialized(copies[i], originals[i]);
                // Keep restored memory authoritative, including edits that were dirty before this request.
                EditorUtility.SetDirty(originals[i]);
            }
            catch (Exception ex) { errors.Add("恢复内存资产失败：" + ex.Message); }
        }
        for (int s = 0; s < scenes.Count; s++)
            if (scenes[s].IsValid() && scenes[s].isLoaded)
            {
                if (sceneDirty[s]) EditorSceneManager.MarkSceneDirty(scenes[s]);
                // Unity 2017 has no public MarkSceneClean. A restored scene may remain dirty;
                // never save it just to clear that flag and overwrite pre-existing edits.
            }
        return errors.Count == 0 ? null : string.Join("；", errors.ToArray());
    }

    public void Dispose()
    {
        foreach (var copy in copies) if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
        copies.Clear();
        Undo.IncrementCurrentGroup();
    }
}
