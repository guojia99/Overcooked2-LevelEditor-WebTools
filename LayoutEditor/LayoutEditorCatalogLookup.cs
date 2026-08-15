using System.Collections.Generic;

public static class LayoutEditorCatalogLookup
{
    public const float GridCellSize = 1.2f;

    private static readonly Dictionary<string, LayoutFootprint> Footprints = new Dictionary<string, LayoutFootprint>
    {
        { "ServingStation", new LayoutFootprint { cellsX = 2, cellsZ = 1 } },
        // 所有水槽 2×1（含马克杯水槽 / 洗杯槽 / 各 DLC 水槽）
        { "Sink", new LayoutFootprint { cellsX = 2, cellsZ = 1 } },
        { "SinkGlass", new LayoutFootprint { cellsX = 2, cellsZ = 1 } },
        { "workstation_sink_mug_01_wood", new LayoutFootprint { cellsX = 2, cellsZ = 1 } },
        { "dlc09_workstation_sink_mug_01_wood", new LayoutFootprint { cellsX = 2, cellsZ = 1 } },
        { "dlc13_workstation_sink_01_wood", new LayoutFootprint { cellsX = 2, cellsZ = 1 } },
        { "workstation_sink_01_summer", new LayoutFootprint { cellsX = 2, cellsZ = 1 } },
        // DLC 大件：火锅灶台 / 大莲花压力开关 2×2
        { "cooking_region_floorburner", new LayoutFootprint { cellsX = 2, cellsZ = 2 } },
        { "dlc10_cooking_region_floorburner", new LayoutFootprint { cellsX = 2, cellsZ = 2 } },
        { "dlc13_lotuspressureswitch_large", new LayoutFootprint { cellsX = 2, cellsZ = 2 } },
        // 火锅大锅 2×2（与火锅灶台同占地）
        { "utensil_large_pot_01", new LayoutFootprint { cellsX = 2, cellsZ = 2 } },
        { "utensil_dlc10_large_pot_01", new LayoutFootprint { cellsX = 2, cellsZ = 2 } },
        // 可推动方块 = 可推动的大火锅，2×2
        { "pushable_object", new LayoutFootprint { cellsX = 2, cellsZ = 2 } },
        { "dlc10_pushable_object", new LayoutFootprint { cellsX = 2, cellsZ = 2 } },
    };

    public static LayoutFootprint GetFootprint(string prefabId)
    {
        LayoutFootprint fp;
        if (Footprints.TryGetValue(prefabId, out fp))
            return new LayoutFootprint { cellsX = fp.cellsX, cellsZ = fp.cellsZ };
        return new LayoutFootprint { cellsX = 1, cellsZ = 1 };
    }

    /** True when prefabId has an explicit (hand-authored) footprint entry. */
    public static bool TryGetFootprint(string prefabId, out LayoutFootprint fp)
    {
        LayoutFootprint found;
        if (Footprints.TryGetValue(prefabId, out found))
        {
            fp = new LayoutFootprint { cellsX = found.cellsX, cellsZ = found.cellsZ };
            return true;
        }
        fp = null;
        return false;
    }

    public static string DefaultParentForAssetPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return "Art";

        // 可推动方块 = 可推动大火锅，归入锅具目录
        if (assetPath.EndsWith("pushable_object.prefab", System.StringComparison.OrdinalIgnoreCase))
            return "Design/Utensils";

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
