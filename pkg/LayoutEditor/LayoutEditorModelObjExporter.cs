using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 装饰物 3D 预览用的轻量 OBJ/MTL 导出：把实例化后的静态/蒙皮网格按当前世界变换烘平，
/// 贴图去重到共享 _textures 目录。坐标与 LayoutEditorCounterSkin3dExport 一致：
/// X 轴镜像 + 面绕序反转，three.js 的 OBJLoader 可直接显示。
/// </summary>
public sealed class LayoutEditorModelExportContext
{
    public string TextureDirAbs;
    public string TextureRelFromModelDir = "../_textures";
    public readonly Dictionary<int, string> TextureFiles = new Dictionary<int, string>();
}

public static class LayoutEditorModelObjExporter
{
    private const int MaxTextureSize = 1024;

    public static bool Export(GameObject inst, Vector3 worldOffset, string modelDirAbs, string fileBase,
        LayoutEditorModelExportContext ctx, out string error)
    {
        error = "";
        if (inst == null)
        {
            error = "实例为空";
            return false;
        }
        Directory.CreateDirectory(modelDirAbs);
        if (ctx.TextureDirAbs != null)
            Directory.CreateDirectory(ctx.TextureDirAbs);

        var renderers = inst.GetComponentsInChildren<Renderer>(false);
        var obj = new StringBuilder();
        var mtl = new StringBuilder();
        var materialIndexByMaterial = new Dictionary<int, int>();
        var materialInfos = new List<MaterialInfo>();
        int vBase = 0;
        int meshCount = 0;

        obj.Append("# OC2 decor model ").Append(fileBase).Append('\n');
        obj.Append("mtllib ").Append(fileBase).Append(".mtl\n");

        for (int ri = 0; ri < renderers.Length; ri++)
        {
            Renderer r = renderers[ri];
            if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r is SpriteRenderer)
                continue;

            Mesh mesh = null;
            bool tempMesh = false;
            try
            {
                var smr = r as SkinnedMeshRenderer;
                if (smr != null)
                {
                    // 用共享网格（绑定姿态）而不是 BakeMesh：BakeMesh 在 batchmode/编辑器
                    // 姿态下可能已包含骨骼世界变换，再乘 localToWorld 会尺寸翻倍。
                    mesh = smr.sharedMesh;
                }
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null) mesh = mf.sharedMesh;
                }
                if (mesh == null || mesh.vertexCount == 0)
                    continue;

                Vector3[] verts = mesh.vertices;
                Vector2[] uvs = mesh.uv;
                Matrix4x4 matrix = r.transform.localToWorldMatrix;
                int vCount = verts.Length;
                var materials = r.sharedMaterials;
                bool hasUv = uvs != null && uvs.Length >= vCount && vCount > 0;

                for (int i = 0; i < vCount; i++)
                {
                    Vector3 v = matrix.MultiplyPoint3x4(verts[i]) - worldOffset;
                    obj.Append("v ").Append(Fmt(-v.x)).Append(' ').Append(Fmt(v.y)).Append(' ').Append(Fmt(v.z)).Append('\n');
                }
                if (hasUv)
                {
                    for (int i = 0; i < vCount; i++)
                        obj.Append("vt ").Append(Fmt(uvs[i].x)).Append(' ').Append(Fmt(uvs[i].y)).Append('\n');
                }

                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    Material mat = materials != null && s < materials.Length ? materials[s] : null;
                    int matIndex = EnsureMaterial(mat, modelDirAbs, ctx, materialIndexByMaterial, materialInfos, mtl);
                    string groupName = Sanitize(r.name) + "_" + s;
                    obj.Append("g ").Append(groupName).Append('\n');
                    obj.Append("usemtl m").Append(matIndex).Append('\n');
                    int[] tris = mesh.GetTriangles(s);
                    for (int i = 0; i + 2 < tris.Length; i += 3)
                    {
                        int a = tris[i] + 1 + vBase;
                        int b = tris[i + 1] + 1 + vBase;
                        int c = tris[i + 2] + 1 + vBase;
                        if (hasUv)
                            obj.Append("f ").Append(c).Append('/').Append(c).Append(' ')
                               .Append(b).Append('/').Append(b).Append(' ')
                               .Append(a).Append('/').Append(a).Append('\n');
                        else
                            obj.Append("f ").Append(c).Append(' ').Append(b).Append(' ').Append(a).Append('\n');
                    }
                }
                vBase += vCount;
                meshCount++;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DecorModel] " + inst.name + " 网格导出失败: " + ex.Message);
            }
            finally
            {
                if (tempMesh && mesh != null)
                    UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        if (meshCount == 0 || vBase == 0)
        {
            error = "没有可导出的网格";
            return false;
        }

        File.WriteAllText(Path.Combine(modelDirAbs, fileBase + ".obj"), obj.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(modelDirAbs, fileBase + ".mtl"), mtl.ToString(), new UTF8Encoding(false));
        return true;
    }

    private sealed class MaterialInfo
    {
        public string name;
        public Color tint;
        public string textureRel;
    }

    private static int EnsureMaterial(Material mat, string modelDirAbs, LayoutEditorModelExportContext ctx,
        Dictionary<int, int> indexByMaterial, List<MaterialInfo> infos, StringBuilder mtl)
    {
        int key = mat == null ? 0 : mat.GetInstanceID();
        int existing;
        if (indexByMaterial.TryGetValue(key, out existing))
            return existing;

        var info = new MaterialInfo();
        info.tint = Color.white;
        Texture2D tex = null;
        if (mat != null)
            tex = LayoutEditorCounterSkinIconExport.DiffuseOfMaterial(mat, out info.tint);
        if (tex != null && ctx.TextureDirAbs != null)
        {
            int texKey = tex.GetInstanceID();
            string file;
            if (!ctx.TextureFiles.TryGetValue(texKey, out file))
            {
                file = "tex_" + texKey + ".png";
                var readable = LayoutEditorCounterSkinIconExport.MakeReadable(tex, MaxTextureSize);
                if (readable != null)
                {
                    try { File.WriteAllBytes(Path.Combine(ctx.TextureDirAbs, file), readable.EncodeToPNG()); }
                    finally { UnityEngine.Object.DestroyImmediate(readable); }
                    ctx.TextureFiles[texKey] = file;
                }
                else
                {
                    file = null;
                }
            }
            if (!string.IsNullOrEmpty(file))
                info.textureRel = ctx.TextureRelFromModelDir + "/" + file;
        }

        int index = infos.Count;
        info.name = "m" + index;
        infos.Add(info);
        indexByMaterial[key] = index;

        mtl.Append("newmtl ").Append(info.name).Append('\n');
        mtl.Append("Ka 0 0 0\n");
        mtl.Append("Kd ").Append(Fmt(info.tint.r)).Append(' ').Append(Fmt(info.tint.g)).Append(' ').Append(Fmt(info.tint.b)).Append('\n');
        mtl.Append("Ks 0 0 0\n");
        mtl.Append("d ").Append(Fmt(Mathf.Clamp01(info.tint.a))).Append('\n');
        mtl.Append("illum 1\n");
        if (!string.IsNullOrEmpty(info.textureRel))
            mtl.Append("map_Kd ").Append(info.textureRel).Append('\n');
        mtl.Append('\n');
        return index;
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "mesh";
        var chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsWhiteSpace(chars[i]) || chars[i] == '/' || chars[i] == '\\')
                chars[i] = '_';
        }
        return new string(chars);
    }

    private static string Fmt(float f)
    {
        if (float.IsNaN(f) || float.IsInfinity(f)) return "0";
        return f.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
    }
}
