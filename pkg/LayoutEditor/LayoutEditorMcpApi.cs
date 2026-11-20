using System;
using System.Text;

/// <summary>
/// HTTP MCP contract exposed by the Unity Bridge. This keeps static web builds
/// usable without requiring Node.js or a separate stdio MCP process.
/// </summary>
public static class LayoutEditorMcpApi
{
    public const string ProtocolVersion = "2025-06-18";
    public const string ServerName = "oc2-level-editor-http";
    public const string ServerVersion = "0.2.0";

    public static string ManifestJson()
    {
        var sb = new StringBuilder();
        sb.Append("{\"protocolVersion\":\"").Append(ProtocolVersion).Append("\",");
        sb.Append("\"server\":{\"name\":\"").Append(ServerName).Append("\",\"version\":\"").Append(ServerVersion).Append("\",\"transport\":\"http\"},");
        sb.Append("\"endpoints\":{\"mcp\":\"/api/mcp\",\"manifest\":\"/api/mcp/manifest\",\"tools\":\"/api/mcp/tools\"},");
        sb.Append("\"bridge\":{\"baseUrl\":\"/\",\"health\":\"/api/health\"},");
        sb.Append("\"tools\":").Append(ToolsJson());
        sb.Append('}');
        return sb.ToString();
    }

    public static string ToolsJson()
    {
        var sb = new StringBuilder();
        sb.Append('[');
        AppendTool(sb, "oc2_editor_health", "检查 Unity Bridge 是否在线。", "read", "{}", true, 1000, 30000, false);
        AppendTool(sb, "oc2_list_level_sets", "列出关卡集。", "read", "{}", true, 5000, 30000, false);
        AppendTool(sb, "oc2_list_levels", "列出指定关卡集中的关卡。", "read", "{\"type\":\"object\",\"properties\":{\"setName\":{\"type\":\"string\"}},\"required\":[\"setName\"]}", true, 5000, 30000, false);
        AppendTool(sb, "oc2_get_level_detail", "读取关卡详情；自动清理并保存历史非法 dependencies。", "read", StringSchema("assetPath"), true, 5000, 30000, false);
        AppendTool(sb, "oc2_save_level_respawn_bounds", "先读详情合并基础标量，保存重生边界并独立读回核对。confirm=true；失败禁止自动重试。", "write_asset", SettingsSchema(true), false, 30000, 180000, true);
        AppendTool(sb, "oc2_save_native_scripted_recipes", "保存 Campaign 指定人数原生序列，GUID 保序保重复，[] 清空；先读详情合并该人数全部标量，仅保存该人数，再读回逐项核对。confirm=true；不自动清除 Web 序列，不自动重试。", "write_asset", SettingsSchema(false), false, 30000, 180000, true);
        AppendTool(sb, "oc2_get_level_mode_config", "读取原生模式配置、Dynamic 阶段、Horde 1P-4P 波次和只读敌人目录。", "read", StringSchema("assetPath"), true, 5000, 30000, false);
        AppendTool(sb, "oc2_save_level_mode_config", "保存完整 Dynamic/Horde config，不切换模式；移除 enemies，2 MiB 上限，记录历史并比较全部可写字段读回。confirm=true；失败禁止自动重试。", "write_asset", ModeConfigSchema(), false, 30000, 180000, true);
        AppendTool(sb, "oc2_change_level_mode", "切换原生模式。convertHordeObjects 默认 false；显式授权可同时转换普通/Horde 交付窗，保留位置名称和回收绑定，改变外观组件，不转换金币门。confirm=true，读回核对，禁止自动重试。", "write_scene", "{\"type\":\"object\",\"properties\":{\"assetPath\":{\"type\":\"string\"},\"gameMode\":{\"enum\":[\"Campaign\",\"Dynamic\",\"Horde\"]},\"convertHordeObjects\":{\"type\":\"boolean\",\"default\":false},\"confirm\":{\"type\":\"boolean\",\"const\":true}},\"required\":[\"assetPath\",\"gameMode\",\"confirm\"],\"additionalProperties\":false}", false, 30000, 180000, true);
        AppendTool(sb, "oc2_create_level", "按原生模板创建关卡，gameMode 缺省 Campaign。confirm=true；从列表发现真实路径，读回模式与详情核对；失败先查列表，禁止自动重试。", "write_scene", "{\"type\":\"object\",\"properties\":{\"setName\":{\"type\":\"string\",\"pattern\":\"^[a-z0-9_]+$\"},\"levelId\":{\"type\":\"string\",\"pattern\":\"^[a-z0-9_]+$\"},\"levelName\":{\"type\":\"string\"},\"levelNameZH\":{\"type\":\"string\"},\"gameMode\":{\"enum\":[\"Campaign\",\"Dynamic\",\"Horde\"],\"default\":\"Campaign\"},\"confirm\":{\"type\":\"boolean\",\"const\":true}},\"required\":[\"setName\",\"levelId\",\"confirm\"],\"additionalProperties\":false}", false, 30000, 180000, true);
        AppendTool(sb, "oc2_get_scene_layout", "读取场景布局。", "read", StringSchema("assetPath"), true, 10000, 60000, false);
        AppendTool(sb, "oc2_get_grid", "读取当前场景网格。", "read", "{}", true, 2000, 30000, false);
        AppendTool(sb, "oc2_get_level_recipe_context", "读取关卡菜谱和菜谱目录；saladSubgroups 的 non_dlc11_official 独占剧情食材沙拉及其酱扩展，其余按互斥酱料组加可交叉的 official 来源组，按菜谱 id 去重计数。", "read", "{\"type\":\"object\",\"properties\":{\"assetPath\":{\"type\":\"string\"},\"levelSet\":{\"type\":\"string\"}},\"required\":[\"assetPath\"]}", true, 10000, 60000, false);
        AppendTool(sb, "oc2_analyze_level_dependencies", "分析原生 ^bundle[0-9]+$ 依赖；自动清理历史非法 dependencies，不过滤共享包分发清单。", "compute", StringSchema("assetPath"), true, 10000, 60000, false);
        AppendTool(sb, "oc2_validate_scene", "读取并验证场景，不写入。", "compute", StringSchema("assetPath"), true, 10000, 60000, false);
        AppendTool(sb, "oc2_commit_scene", "将完整 LayoutDocument 写回 Unity 场景，必须 confirm=true。Play 模式下整次拒绝，不改场景。", "write_scene", "{\"type\":\"object\",\"properties\":{\"confirm\":{\"const\":true},\"snap\":{\"type\":\"number\"},\"syncWalkable\":{\"type\":\"boolean\"},\"only\":{\"type\":\"string\"},\"document\":{\"type\":\"object\"}},\"required\":[\"confirm\",\"document\"]}", false, 30000, 120000, true);
        AppendTool(sb, "oc2_start_export", "启动增量导出；自动预检可选空包并核验引用，返回 starting/preflight/orphan-clean/dual-stub-clean 等实际阶段。levels 仅交付关卡且不 staging 运行时；轮询至恢复结束。", "build", "{\"type\":\"object\",\"properties\":{\"setNames\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},\"setName\":{\"type\":\"string\"},\"mode\":{\"enum\":[\"all\",\"levels\",\"deps\"]},\"selectedLevels\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},\"depsVersion\":{\"type\":\"string\"},\"confirm\":{\"const\":true}},\"required\":[\"confirm\"]}", false, 300000, 1800000, true);
        AppendTool(sb, "oc2_get_export_status", "后台读取导出状态及 exportId，建议每 2 秒轮询并核对任务 ID；compile 重载继续，其他阶段中断后恢复并报 error。", "read", "{}", true, 1000, 30000, false);
        AppendTool(sb, "oc2_salad_sauce_bundles", "仅构建 commonw3/runtime Windows bundle，不保存场景或发布；build 需 confirm=true，status 读回。HTTP 超时先读 Library/SaladSauceBundleBuild/result.json，勿重复 build。", "build", "{\"type\":\"object\",\"properties\":{\"operation\":{\"enum\":[\"build\",\"status\"]},\"confirm\":{\"type\":\"boolean\"}},\"required\":[\"operation\"]}", false, 300000, 1800000, true);
        sb.Append(']');
        return sb.ToString();
    }

    private static string StringSchema(string property)
    {
        return "{\"type\":\"object\",\"properties\":{\"" + property + "\":{\"type\":\"string\"}},\"required\":[\"" + property + "\"]}";
    }

    private static string ModeConfigSchema()
    {
        var guids = "{\"type\":\"array\",\"minItems\":1,\"maxItems\":256,\"items\":{\"type\":\"string\",\"pattern\":\"^[0-9a-fA-F]{32}$\"}}";
        var id = "{\"type\":\"string\",\"minLength\":1,\"maxLength\":128}";
        var phase = "{\"type\":\"object\",\"properties\":{\"id\":" + id + ",\"durationSeconds\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":86400},\"recipeGuids\":" + guids + "},\"required\":[\"id\",\"durationSeconds\",\"recipeGuids\"],\"additionalProperties\":false}";
        var wave = "{\"type\":\"object\",\"properties\":{\"id\":" + id + ",\"intervalSeconds\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":86400},\"recipeGuids\":" + guids + ",\"enemyGuids\":" + guids + ",\"spawnTimes\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":256,\"items\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":86400}}},\"required\":[\"id\",\"intervalSeconds\",\"recipeGuids\",\"enemyGuids\",\"spawnTimes\"],\"additionalProperties\":false}";
        var player = "{\"type\":\"object\",\"properties\":{\"targetRepairCostMax\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":1000000},\"waves\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":128,\"items\":" + wave + "}},\"required\":[\"targetRepairCostMax\",\"waves\"],\"additionalProperties\":false}";
        var health = "{\"type\":\"integer\",\"minimum\":1,\"maximum\":1000000}";
        var repair = "{\"type\":\"number\",\"minimum\":0,\"maximum\":1000000}";
        var orders = "{\"type\":\"integer\",\"minimum\":1,\"maximum\":10}";
        return "{\"type\":\"object\",\"properties\":{\"config\":{\"type\":\"object\",\"properties\":{\"assetPath\":{\"type\":\"string\",\"maxLength\":1024},\"gameMode\":{\"enum\":[\"Dynamic\",\"Horde\"]},\"phases\":{\"type\":[\"array\",\"null\"],\"maxItems\":64,\"items\":" + phase + "},\"hordeConfigs\":{\"type\":[\"array\",\"null\"],\"maxItems\":4,\"items\":" + player + "},\"targetHealth\":" + health + ",\"targetRepairSpeed\":" + repair + ",\"targetRepairThreshold\":" + repair + ",\"kitchenHealth\":" + health + ",\"minOrderCount\":" + orders + ",\"maxOrderCount\":" + orders + ",\"enemies\":{\"type\":[\"array\",\"null\"],\"maxItems\":6,\"readOnly\":true}},\"required\":[\"assetPath\",\"gameMode\",\"targetHealth\",\"targetRepairSpeed\",\"targetRepairThreshold\",\"kitchenHealth\",\"minOrderCount\",\"maxOrderCount\"],\"additionalProperties\":false,\"allOf\":[{\"if\":{\"properties\":{\"gameMode\":{\"const\":\"Dynamic\"}}},\"then\":{\"required\":[\"phases\"],\"properties\":{\"phases\":{\"type\":\"array\",\"minItems\":2},\"hordeConfigs\":{\"maxItems\":0}}}},{\"if\":{\"properties\":{\"gameMode\":{\"const\":\"Horde\"}}},\"then\":{\"required\":[\"hordeConfigs\"],\"properties\":{\"hordeConfigs\":{\"type\":\"array\",\"minItems\":4},\"phases\":{\"maxItems\":0}}}}]},\"confirm\":{\"type\":\"boolean\",\"const\":true}},\"required\":[\"config\",\"confirm\"],\"additionalProperties\":false}";
    }

    private static string SettingsSchema(bool respawn)
    {
        var fields = respawn ? "\"enableRespawnBounds\":{\"type\":\"boolean\"}"
            : "\"playerCount\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":4},\"scriptedRecipes\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"pattern\":\"^[0-9a-fA-F]{32}$\"}}";
        var required = respawn ? "\"enableRespawnBounds\"" : "\"playerCount\",\"scriptedRecipes\"";
        return "{\"type\":\"object\",\"properties\":{\"assetPath\":{\"type\":\"string\",\"maxLength\":1024,\"pattern\":\"^Assets/LevelSets/[^/\\\\\\\\]+/data/[^/\\\\\\\\]+/[^/\\\\\\\\]+\\\\.asset$\"},"
            + fields + ",\"confirm\":{\"type\":\"boolean\",\"const\":true}},\"required\":[\"assetPath\"," + required + ",\"confirm\"],\"additionalProperties\":false}";
    }

    private static void AppendTool(StringBuilder sb, string name, string description, string sideEffect,
        string schema, bool safeToRetry, int estimatedMs, int deadlineMs, bool requiresConfirmation)
    {
        if (sb[sb.Length - 1] != '[')
            sb.Append(',');
        sb.Append("{\"name\":\"").Append(name).Append("\",\"description\":\"")
            .Append(Escape(description)).Append("\",\"sideEffect\":\"").Append(sideEffect)
            .Append("\",\"safeToRetry\":").Append(safeToRetry ? "true" : "false")
            .Append(",\"estimatedMs\":").Append(estimatedMs)
            .Append(",\"deadlineMs\":").Append(deadlineMs)
            .Append(",\"pollAfterMs\":").Append(sideEffect == "build" ? "2000" : "null")
            .Append(",\"requiresConfirmation\":").Append(requiresConfirmation ? "true" : "false")
            .Append(",\"inputSchema\":").Append(schema).Append('}');
    }

    private static string Escape(string value)
    {
        if (value == null)
            return "";
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
    }
}
