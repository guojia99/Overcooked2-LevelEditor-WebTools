using System.IO;
using LevelEditorStub;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LayoutEditorLevelInfoResolver
{
    public static LevelInfoSO ResolveForScene(string sceneAssetPath)
    {
        if (string.IsNullOrEmpty(sceneAssetPath))
            return null;

        sceneAssetPath = sceneAssetPath.Replace('\\', '/');
        var manager = Object.FindObjectOfType<PseudoPrefabManagerStub>();
        if (manager != null && manager.levelInfo != null)
            return manager.levelInfo;

        var parts = sceneAssetPath.Split('/');
        if (parts.Length < 4 || parts[1] != "LevelSets")
            return null;

        var levelSet = parts[2];
        var sceneName = Path.GetFileNameWithoutExtension(sceneAssetPath);
        var dataRoot = "Assets/LevelSets/" + levelSet + "/data";
        if (!AssetDatabase.IsValidFolder(dataRoot))
            return null;

        foreach (var guid in AssetDatabase.FindAssets("t:LevelInfoSO", new[] { dataRoot }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var info = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(path);
            if (info == null)
                continue;

            if (!string.IsNullOrEmpty(info.sceneName) && info.sceneName == sceneName)
                return info;

            if (!string.IsNullOrEmpty(info.levelName) &&
                sceneName.IndexOf(info.levelName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return info;
        }

        return null;
    }

    public static string LevelSetFromScenePath(string sceneAssetPath)
    {
        sceneAssetPath = sceneAssetPath.Replace('\\', '/');
        var parts = sceneAssetPath.Split('/');
        return parts.Length > 2 && parts[1] == "LevelSets" ? parts[2] : string.Empty;
    }
}
