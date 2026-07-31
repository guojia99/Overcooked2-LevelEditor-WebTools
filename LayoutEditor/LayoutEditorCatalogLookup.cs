using System.Collections.Generic;

public static class LayoutEditorCatalogLookup
{
    public const float GridCellSize = 1.2f;

    private static readonly Dictionary<string, LayoutFootprint> Footprints = new Dictionary<string, LayoutFootprint>
    {
        { "ServingStation", new LayoutFootprint { cellsX = 2, cellsZ = 1 } },
        { "Sink", new LayoutFootprint { cellsX = 2, cellsZ = 1 } },
    };

    public static LayoutFootprint GetFootprint(string prefabId)
    {
        LayoutFootprint fp;
        if (Footprints.TryGetValue(prefabId, out fp))
            return new LayoutFootprint { cellsX = fp.cellsX, cellsZ = fp.cellsZ };
        return new LayoutFootprint { cellsX = 1, cellsZ = 1 };
    }

    public static string DefaultParentForAssetPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return "Art";

        if (assetPath.Contains("/prefabs/counters/"))
            return "Design/Counters";
        if (assetPath.Contains("/prefabs/utensils/"))
            return "Design/Utensils";
        if (assetPath.Contains("/prefabs/mechanisms/"))
            return "Design/Counters";
        if (assetPath.Contains("/prefabs/Player"))
            return "Chefs";
        return "Art";
    }
}
