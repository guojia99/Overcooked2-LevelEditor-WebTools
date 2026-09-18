using UnityEngine;

/// <summary>
/// Measures the real XZ footprint (in grid cells) of a decor prefab instance
/// from its MeshRenderer world bounds. PseudoPrefab placeholders carry their
/// visuals as spawned children, so measuring the instance captures the true
/// appearance size (e.g. exterior_road_01 = 1x2 cells instead of the 1x1
/// catalog fallback).
/// </summary>
public static class LayoutEditorFootprintMeasure
{
    /// <summary>
    /// Native (unscaled) footprint in cells. rotY quarter-turns are undone so a
    /// 90°-rotated 1x2 road still reports cellsX=1, cellsZ=2; the instance scale
    /// is divided out so the web can re-apply it on top of the footprint.
    /// Falls back to 1x1 when nothing measurable is found.
    /// </summary>
    public static LayoutFootprint MeasureCells(GameObject root)
    {
        var fp = new LayoutFootprint { cellsX = 1, cellsZ = 1 };
        if (root == null)
            return fp;

        var renderers = root.GetComponentsInChildren<MeshRenderer>(false);
        if (renderers == null || renderers.Length == 0)
            return fp;

        Bounds bounds = renderers[0].bounds;
        bool any = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null)
                continue;
            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        if (!any)
            return fp;

        float sx = bounds.size.x;
        float sz = bounds.size.z;

        // Undo the Y rotation quarter-turn so the footprint is expressed in the
        // item's local frame (the web rotates the footprint when drawing).
        float rotY = root.transform.localEulerAngles.y % 360f;
        if (rotY < 0f) rotY += 360f;
        int quarter = Mathf.RoundToInt(rotY / 90f) % 4;
        if (quarter == 1 || quarter == 3)
        {
            float tmp = sx;
            sx = sz;
            sz = tmp;
        }

        // Divide out the instance scale: the exported footprint is the native
        // prefab size; the web multiplies localScale back on top for display.
        float scaleX = Mathf.Abs(root.transform.localScale.x);
        float scaleZ = Mathf.Abs(root.transform.localScale.z);
        if (scaleX > 0.0001f) sx /= scaleX;
        if (scaleZ > 0.0001f) sz /= scaleZ;

        fp.cellsX = Mathf.Max(1, Mathf.RoundToInt(sx / LayoutEditorCatalogLookup.GridCellSize));
        fp.cellsZ = Mathf.Max(1, Mathf.RoundToInt(sz / LayoutEditorCatalogLookup.GridCellSize));
        return fp;
    }

    /// <summary>
    /// Native (unscaled) model height in world units — the MeshRenderer world
    /// bounds Y size with the instance scale divided out. Flat floor tiles
    /// report ~0.1, tall pieces (ice cliffs, blocks) report 1+; the web uses
    /// this as the catalog item's intrinsic height for the height-range
    /// palette filter. Returns 0 when nothing measurable is found.
    /// </summary>
    public static float MeasureHeight(GameObject root)
    {
        // 刻意仍走「仅激活 MeshRenderer」的旧口径：装饰实测尺寸
        // (measured-footprints.json) 是既有管线，必须逐字节保持原行为；
        // 放宽渲染器类型只发生在 3D 高度导出的 MeasureBounds 里。
        Bounds bounds;
        if (!UnionRendererBounds(root, out bounds))
            return 0f;
        float scaleY = root != null ? Mathf.Abs(root.transform.localScale.y) : 1f;
        float sy = bounds.size.y;
        if (scaleY > 0.0001f) sy /= scaleY;
        return sy;
    }

    /// <summary>
    /// Native (unscaled) vertical extent of the model relative to its own pivot:
    /// <paramref name="sizeY"/> is the bounds height, <paramref name="minY"/> is
    /// the bottom face offset from the transform origin (0 for base-pivoted
    /// counters, negative for centre-pivoted pots). The web 3D view needs both —
    /// a box is drawn from localPosition.y + minY upwards by sizeY, otherwise
    /// centre-pivoted props float or sink by half their height.
    /// Returns false when nothing measurable is found.
    /// </summary>
    public static bool MeasureBounds(GameObject root, out float sizeY, out float minY)
    {
        sizeY = 0f;
        minY = 0f;
        if (root == null)
            return false;

        Bounds bounds;
        // 先只看激活的渲染器；一个都没有再放宽到含未激活的（部分 prefab 把网格
        // 挂在默认关闭的子物体上，例如多形态的机器/控制终端）。
        if (!UnionRenderableBounds(root, false, out bounds) && !UnionRenderableBounds(root, true, out bounds))
            return false;

        float scaleY = Mathf.Abs(root.transform.localScale.y);
        float inv = scaleY > 0.0001f ? 1f / scaleY : 1f;
        sizeY = bounds.size.y * inv;
        minY = (bounds.min.y - root.transform.position.y) * inv;
        return true;
    }

    /// <summary>
    /// World-space union of every MeshRenderer / SkinnedMeshRenderer under root.
    ///
    /// 刻意用 Renderer 基类再筛类型，而不是只取 MeshRenderer：玩家、大炮、搅拌机、
    /// 控制终端等带骨骼动画的物件挂的是 SkinnedMeshRenderer，只扫 MeshRenderer 会
    /// 整个漏掉（实测 3D 高度导出时这批核心玩法物件全部缺失）。
    /// 同时排除粒子/拖尾/线渲染器 —— 它们的 bounds 是运行期模拟范围，动辄上百米，
    /// 会把盒体撑成怪物。
    /// </summary>
    private static bool UnionRenderableBounds(GameObject root, bool includeInactive, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        if (root == null)
            return false;

        var renderers = root.GetComponentsInChildren<Renderer>(includeInactive);
        if (renderers == null || renderers.Length == 0)
            return false;

        bool any = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null)
                continue;
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))
                continue;
            // 未激活分支下仍要求渲染器本身是启用的，避免把「备用外观」算进来。
            if (!includeInactive && !r.enabled)
                continue;
            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        return any;
    }

    /// <summary>World-space union of every enabled MeshRenderer under root.</summary>
    private static bool UnionRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        if (root == null)
            return false;

        var renderers = root.GetComponentsInChildren<MeshRenderer>(false);
        if (renderers == null || renderers.Length == 0)
            return false;

        bool any = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null)
                continue;
            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        return any;
    }
}
