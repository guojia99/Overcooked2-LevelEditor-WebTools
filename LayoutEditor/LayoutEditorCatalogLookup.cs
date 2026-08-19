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
        // 洗餐盘水槽（DLC8 马戏团，3 款皮肤同占地）
        { "dlc08_workstation_01_tray_sink_circus", new LayoutFootprint { cellsX = 2, cellsZ = 1 } },
        { "dlc08_workstation_02_tray_sink_circus", new LayoutFootprint { cellsX = 2, cellsZ = 1 } },
        { "dlc08_workstation_03_tray_sink_circus", new LayoutFootprint { cellsX = 2, cellsZ = 1 } },
        // DLC 大件：大莲花压力开关 2×2；火锅灶台铺满 2×2（大锅 2×2 锅沿外架其上）
        { "cooking_region_floorburner", new LayoutFootprint { cellsX = 2, cellsZ = 2 } },
        { "dlc10_cooking_region_floorburner", new LayoutFootprint { cellsX = 2, cellsZ = 2 } },
        { "dlc13_lotuspressureswitch_large", new LayoutFootprint { cellsX = 2, cellsZ = 2 } },
        // 火锅大锅 2×2（与火锅灶台同占地，铺满）
        { "utensil_large_pot_01", new LayoutFootprint { cellsX = 2, cellsZ = 2 } },
        { "utensil_dlc10_large_pot_01", new LayoutFootprint { cellsX = 2, cellsZ = 2 } },
        // 可推动方块 = 可推动的大火锅，2×2
        { "pushable_object", new LayoutFootprint { cellsX = 2, cellsZ = 2 } },
        { "dlc10_pushable_object", new LayoutFootprint { cellsX = 2, cellsZ = 2 } },
        // 断头台 2×1（切菜台）；大炮 2×2
        { "workstation_guillotine_01", new LayoutFootprint { cellsX = 2, cellsZ = 1 } },
        { "dlc08_cannon", new LayoutFootprint { cellsX = 2, cellsZ = 2 } },
        { "dlc09_cannon", new LayoutFootprint { cellsX = 2, cellsZ = 2 } },
        // 半格小型厨具/工具：1×1 向内缩 10%（0.9 格）；旧数据误设为 1×1
        { "utensil_bellows_01", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "Bellows", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "utensil_water_gun_01", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "WaterGun", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "FireExtinguisher", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "utensil_fire_extinguisher_02", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "dlc08_utensil_fire_extinguisher", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "ToastingFork", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "utensil_toasting_fork_01", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "Skewer", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "utensil_skewer_01", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "MixerBowl", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "BlenderCup", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "utensil_blender_01", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "Blender", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "dlc02_utensil_mixer", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "dlc03_utensil_mixer", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "dlc05_utensil_mixer", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "dlc07_utensil_mixer_01", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "dlc08_utensil_mixer_01", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "dlc09_utensil_mixer", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "dlc13_utensil_mixer_01", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "FryPan", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "dlc02_utensil_frying_pan", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "dlc05_utensil_frying_pan", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "dlc07_utensil_frying_pan_01", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "dlc08_utensil_frying_pan", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "dlc09_utensil_frying_pan", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "utensil_griddlepan", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "GriddlePan", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "utensil_big_ol_spoon", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "utensil_dlc10_big_ol_spoon", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "utensil_coalbucket_01", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "p_dlc7_coal_bucket_coal_01", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "utensil_ingredient_spray_01", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
        { "dlc09_utensil_ingredient_spray", new LayoutFootprint { cellsX = 0.9f, cellsZ = 0.9f } },
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

        // 兼容两代目录结构：prefabs/{category}/（common01/02）与
        // prefabs/{dlcXX|core}/{category}/（common03 通用内容按 dlc 分目录）。
        var cat = System.Text.RegularExpressions.Regex.Match(assetPath,
            @"/prefabs/(?:[^/]+/)?(counters|utensils|mechanisms)/");
        if (cat.Success)
        {
            if (cat.Groups[1].Value == "counters")
                return "Design/Counters";
            if (cat.Groups[1].Value == "utensils")
                return "Design/Utensils";
            return "Design/Counters"; // mechanisms
        }
        if (assetPath.Contains("/prefabs/Player"))
            return "Chefs";
        return "Art";
    }
}
