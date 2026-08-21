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
        if (root == null)
            return 0f;

        var renderers = root.GetComponentsInChildren<MeshRenderer>(false);
        if (renderers == null || renderers.Length == 0)
            return 0f;

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
            return 0f;

        float sy = bounds.size.y;
        float scaleY = Mathf.Abs(root.transform.localScale.y);
        if (scaleY > 0.0001f) sy /= scaleY;
        return sy;
    }
}
