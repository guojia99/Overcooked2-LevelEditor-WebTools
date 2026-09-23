using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 「自定义菜谱资产瘦身」后端（自定义菜谱页 📦 按钮）。
///
/// 只改 Assets/LevelSets/&lt;set&gt;/custom_recipes/** 的 **导入参数（.meta）**，不改源文件字节：
///  - 贴图：TextureImporter.maxTextureSize（图标 &lt;id&gt;_Icon.png 与材质贴图分开设档，默认 256）；
///  - 模型：ModelImporter.meshCompression（顶点量化，Off/Low/Medium/High）+ isReadable=false
///    （bundle 内不存 CPU 副本）+ optimizeMesh=true。Unity 导入器没有减面能力，体积大头在贴图。
/// GUID 不变 → prefab/SO 引用不受影响；重新导出关卡集即缩小 bundle。
///
/// 边界（与 BunSwapApi 一致）：commonW2 / common01 共享库与关卡集外路径一律不碰——
/// 本类只枚举关卡集自己的 custom_recipes 目录，不接收任意 assetPath。
/// 上传链路（UploadCustomRecipeIcon / UploadCustomRecipeModel）调用本类的默认档，
/// 保证新上传资产无需回头补瘦身。
/// </summary>
public static class LayoutEditorCustomRecipeOptimizeApi
{
    /// <summary>上传链路与一键瘦身的默认贴图档位（maxTextureSize）。</summary>
    public const int DefaultTextureMaxSize = 256;

    /// <summary>上传链路与一键瘦身的默认网格精度档（顶点量化）。</summary>
    public const ModelImporterMeshCompression DefaultMeshCompression = ModelImporterMeshCompression.Medium;

    private const int MaxDetails = 200;

    private static readonly string[] TextureExts = { ".png", ".jpg", ".jpeg", ".tga" };
    private static readonly string[] ModelExts = { ".fbx", ".obj" };

    // ------------------------------------------------------------ DTO

    [Serializable]
    public class TextureUsageDto
    {
        public string path;
        public bool isIcon;
        /// <summary>importer 当前 maxTextureSize（未显式设置时为 Unity 默认 2048）。</summary>
        public int maxTextureSize;
        /// <summary>导入后实际宽高（0 = 加载失败）。</summary>
        public int width;
        public int height;
    }

    [Serializable]
    public class ModelUsageDto
    {
        public string path;
        /// <summary>全部子 Mesh 顶点数合计。</summary>
        public int vertices;
        /// <summary>全部子 Mesh 三角形数合计；-1 = 读写已关，无法读取。</summary>
        public int triangles;
        public int subMeshes;
        public bool isReadable;
        /// <summary>Off / Low / Medium / High。</summary>
        public string meshCompression;
    }

    [Serializable]
    public class OptimizeUsageDto
    {
        public string setName;
        public string recipesDir;
        public bool dirExists;
        public TextureUsageDto[] textures;
        public ModelUsageDto[] models;
        public string error;
    }

    [Serializable]
    public class OptimizeRequestDto
    {
        public string setName;
        /// <summary>材质贴图目标档（128/256/512/1024）。</summary>
        public int textureMaxSize = DefaultTextureMaxSize;
        /// <summary>图标（_Icon.png）目标档，可与材质贴图不同。</summary>
        public int iconMaxSize = DefaultTextureMaxSize;
        /// <summary>网格精度档：off / low / medium / high。</summary>
        public string meshCompression = "medium";
        /// <summary>true = 只统计不落盘。</summary>
        public bool dryRun;
    }

    [Serializable]
    public class OptimizeResultDto
    {
        public int texturesChanged;
        public int modelsChanged;
        /// <summary>逐资产 变更前→后 摘要（最多 MaxDetails 条，超出合并计数）。</summary>
        public string[] details;
        /// <summary>被跳过的条目（含原因）。</summary>
        public string[] skipped;
        public string error;
    }

    // ------------------------------------------------------------ 路径 / 工具

    internal static string SetRecipesDir(string setName)
    {
        return LayoutEditorLevelAdminApi.LevelSetsRoot + "/" + setName + "/custom_recipes";
    }

    private static string AbsPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return "";
        var dataPath = Application.dataPath.Replace('\\', '/');
        if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            return dataPath + assetPath.Substring("Assets".Length);
        return assetPath;
    }

    private static string _projectRoot;

    private static string ProjectRoot()
    {
        if (_projectRoot == null)
        {
            var dataPath = Application.dataPath.Replace('\\', '/');
            _projectRoot = dataPath.Substring(0, dataPath.Length - "/Assets".Length);
        }
        return _projectRoot;
    }

    /// <summary>绝对路径 → Assets/ 相对路径（不在工程内则原样返回）。</summary>
    private static string RelAssetPath(string absPath)
    {
        var p = absPath.Replace('\\', '/');
        var prefix = ProjectRoot() + "/";
        if (p.StartsWith(prefix, StringComparison.Ordinal))
            return p.Substring(prefix.Length);
        return p;
    }

    private static bool HasExt(string file, string[] exts)
    {
        var ext = Path.GetExtension(file ?? "").ToLowerInvariant();
        for (int i = 0; i < exts.Length; i++)
        {
            if (ext == exts[i])
                return true;
        }
        return false;
    }

    /// <summary>图标约定名 &lt;recipeId&gt;_Icon.png（任意图片扩展名）。</summary>
    private static bool IsIconName(string assetPath)
    {
        return assetPath.IndexOf("_Icon.", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>枚举目录下（含子目录）指定扩展名的资产路径，已排序。</summary>
    private static List<string> CollectFiles(string dirAssetPath, string[] exts)
    {
        var result = new List<string>();
        var abs = AbsPath(dirAssetPath);
        if (string.IsNullOrEmpty(abs) || !Directory.Exists(abs))
            return result;
        var files = Directory.GetFiles(abs, "*", SearchOption.AllDirectories);
        foreach (var f in files)
        {
            if (HasExt(f, exts))
                result.Add(RelAssetPath(f));
        }
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static bool IsValidTextureSize(int size)
    {
        return size == 128 || size == 256 || size == 512 || size == 1024 || size == 2048;
    }

    private static bool TryParseCompression(string value, out ModelImporterMeshCompression compression)
    {
        compression = ModelImporterMeshCompression.Off;
        var v = value == null ? "" : value.Trim().ToLowerInvariant();
        if (v == "off")
            return true;
        if (v == "low")
        {
            compression = ModelImporterMeshCompression.Low;
            return true;
        }
        if (v == "medium")
        {
            compression = ModelImporterMeshCompression.Medium;
            return true;
        }
        if (v == "high")
        {
            compression = ModelImporterMeshCompression.High;
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------ GET /api/custom-recipes/optimize-usage

    /// <summary>扫描本关卡集 custom_recipes 的贴图/模型导入参数，供弹窗做瘦身前概览。</summary>
    public static OptimizeUsageDto GetUsage(string setName)
    {
        var dto = new OptimizeUsageDto();
        dto.setName = setName == null ? "" : setName;
        dto.recipesDir = string.IsNullOrEmpty(setName) ? "" : SetRecipesDir(setName);
        dto.textures = new TextureUsageDto[0];
        dto.models = new ModelUsageDto[0];
        if (string.IsNullOrEmpty(setName))
        {
            dto.error = "缺少关卡集名。";
            return dto;
        }
        dto.dirExists = LayoutEditorLevelAdminApi.AssetFolderExists(dto.recipesDir);
        if (!dto.dirExists)
            return dto;

        var textures = new List<TextureUsageDto>();
        foreach (var p in CollectFiles(dto.recipesDir, TextureExts))
        {
            var imp = AssetImporter.GetAtPath(p) as TextureImporter;
            if (imp == null)
                continue;
            var t = new TextureUsageDto();
            t.path = p;
            t.isIcon = IsIconName(p);
            t.maxTextureSize = imp.maxTextureSize;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (tex != null)
            {
                t.width = tex.width;
                t.height = tex.height;
            }
            textures.Add(t);
        }
        dto.textures = textures.ToArray();

        var models = new List<ModelUsageDto>();
        foreach (var p in CollectFiles(dto.recipesDir, ModelExts))
        {
            var imp = AssetImporter.GetAtPath(p) as ModelImporter;
            if (imp == null)
                continue;
            var m = new ModelUsageDto();
            m.path = p;
            m.isReadable = imp.isReadable;
            m.meshCompression = imp.meshCompression.ToString();
            var anyUnreadable = false;
            var subs = AssetDatabase.LoadAllAssetsAtPath(p);
            foreach (var o in subs)
            {
                var mesh = o as Mesh;
                if (mesh == null)
                    continue;
                m.vertices += mesh.vertexCount;
                m.subMeshes += mesh.subMeshCount;
                if (mesh.isReadable)
                    m.triangles += mesh.triangles.Length / 3;
                else
                    anyUnreadable = true;
            }
            if (anyUnreadable && m.triangles == 0)
                m.triangles = -1;
            models.Add(m);
        }
        dto.models = models.ToArray();
        return dto;
    }

    // ------------------------------------------------------------ POST /api/custom-recipes/optimize

    /// <summary>一键瘦身：只改 .meta 导入参数，未达标（更小/未量化）的资产才会被重导入。</summary>
    public static OptimizeResultDto Optimize(OptimizeRequestDto req)
    {
        var res = new OptimizeResultDto();
        res.details = new string[0];
        res.skipped = new string[0];

        if (req == null || string.IsNullOrEmpty(req.setName))
        {
            res.error = "缺少关卡集名。";
            return res;
        }
        if (!IsValidTextureSize(req.textureMaxSize))
        {
            res.error = "材质贴图档位非法（支持 128/256/512/1024/2048）：" + req.textureMaxSize;
            return res;
        }
        if (!IsValidTextureSize(req.iconMaxSize))
        {
            res.error = "图标档位非法（支持 128/256/512/1024/2048）：" + req.iconMaxSize;
            return res;
        }
        ModelImporterMeshCompression compression;
        if (!TryParseCompression(req.meshCompression, out compression))
        {
            res.error = "网格精度档非法（off/low/medium/high）：" + (req.meshCompression ?? "");
            return res;
        }

        var dir = SetRecipesDir(req.setName);
        if (!LayoutEditorLevelAdminApi.AssetFolderExists(dir))
        {
            res.error = "关卡集没有 custom_recipes 目录：" + dir;
            return res;
        }

        var details = new List<string>();
        var skipped = new List<string>();
        int texturesChanged = 0;
        int modelsChanged = 0;

        var texFiles = CollectFiles(dir, TextureExts);
        var modelFiles = CollectFiles(dir, ModelExts);
        var total = texFiles.Count + modelFiles.Count;
        var done = 0;
        try
        {
            foreach (var p in texFiles)
            {
                done++;
                EditorUtility.DisplayProgressBar("资产瘦身", p, total > 0 ? (float)done / total : 0f);
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp == null)
                {
                    skipped.Add(p + "：不是贴图导入器");
                    continue;
                }
                var target = IsIconName(p) ? req.iconMaxSize : req.textureMaxSize;
                if (imp.maxTextureSize <= target)
                    continue; // 已达标（当前更小时不放大，避免无意义重导入）
                var summary = p + "：" + imp.maxTextureSize + " → " + target;
                var ok = true;
                if (!req.dryRun)
                {
                    try
                    {
                        imp.maxTextureSize = target;
                        imp.SaveAndReimport();
                    }
                    catch (Exception ex)
                    {
                        skipped.Add(p + "：" + ex.Message);
                        ok = false;
                    }
                }
                if (ok)
                {
                    details.Add(summary);
                    texturesChanged++;
                }
            }

            foreach (var p in modelFiles)
            {
                done++;
                EditorUtility.DisplayProgressBar("资产瘦身", p, total > 0 ? (float)done / total : 0f);
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null)
                {
                    skipped.Add(p + "：不是模型导入器");
                    continue;
                }
                var changes = new List<string>();
                if (imp.meshCompression != compression)
                    changes.Add("精度 " + imp.meshCompression + "→" + compression);
                if (imp.isReadable)
                    changes.Add("关读写副本");
                if (!imp.optimizeMesh)
                    changes.Add("开网格优化");
                if (changes.Count == 0)
                    continue; // 已达标
                var summary = p + "：" + string.Join("，", changes.ToArray());
                var ok = true;
                if (!req.dryRun)
                {
                    try
                    {
                        ApplyModelSettings(imp, compression);
                        imp.SaveAndReimport();
                    }
                    catch (Exception ex)
                    {
                        skipped.Add(p + "：" + ex.Message);
                        ok = false;
                    }
                }
                if (ok)
                {
                    details.Add(summary);
                    modelsChanged++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (details.Count > MaxDetails)
        {
            var rest = details.Count - MaxDetails + 1;
            details = details.GetRange(0, MaxDetails - 1);
            details.Add("…其余 " + rest + " 项同类调整");
        }

        res.texturesChanged = texturesChanged;
        res.modelsChanged = modelsChanged;
        res.details = details.ToArray();
        res.skipped = skipped.ToArray();

        LayoutEditorLog.Log("[AssetOptimize] " + req.setName + "：贴图 ×" + texturesChanged
            + "、模型 ×" + modelsChanged + (req.dryRun ? "（dryRun）" : "，导入参数已写入 .meta"));
        return res;
    }

    // ------------------------------------------------------------ 上传链路共用

    private static void ApplyModelSettings(ModelImporter imp, ModelImporterMeshCompression compression)
    {
        imp.meshCompression = compression;
        imp.isReadable = false;
        imp.optimizeMesh = true;
    }

    /// <summary>上传链路用：新 FBX/OBJ 落盘后套默认导入档（顶点量化 + 关读写副本 + 网格优化）。
    /// 返回是否发生重导入；找不到模型导入器时静默跳过。</summary>
    public static bool TryApplyDefaultModelSettings(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;
        var imp = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (imp == null)
            return false;
        if (imp.meshCompression == DefaultMeshCompression && !imp.isReadable && imp.optimizeMesh)
            return false;
        ApplyModelSettings(imp, DefaultMeshCompression);
        imp.SaveAndReimport();
        return true;
    }
}
