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
        AppendTool(sb, "oc2_get_level_detail", "读取关卡详情。", "read", StringSchema("assetPath"), true, 5000, 30000, false);
        AppendTool(sb, "oc2_get_scene_layout", "读取场景布局。", "read", StringSchema("assetPath"), true, 10000, 60000, false);
        AppendTool(sb, "oc2_get_grid", "读取当前场景网格。", "read", "{}", true, 2000, 30000, false);
        AppendTool(sb, "oc2_get_level_recipe_context", "读取关卡菜谱和菜谱目录。", "read", "{\"type\":\"object\",\"properties\":{\"assetPath\":{\"type\":\"string\"},\"levelSet\":{\"type\":\"string\"}},\"required\":[\"assetPath\"]}", true, 10000, 60000, false);
        AppendTool(sb, "oc2_analyze_level_dependencies", "分析关卡 AssetBundle 依赖。", "compute", StringSchema("assetPath"), true, 10000, 60000, false);
        AppendTool(sb, "oc2_validate_scene", "读取并验证场景，不写入。", "compute", StringSchema("assetPath"), true, 10000, 60000, false);
        AppendTool(sb, "oc2_commit_scene", "将完整 LayoutDocument 写回 Unity 场景，必须 confirm=true。", "write_scene", "{\"type\":\"object\",\"properties\":{\"confirm\":{\"const\":true},\"snap\":{\"type\":\"number\"},\"syncWalkable\":{\"type\":\"boolean\"},\"only\":{\"type\":\"string\"},\"document\":{\"type\":\"object\"}},\"required\":[\"confirm\",\"document\"]}", false, 30000, 120000, true);
        AppendTool(sb, "oc2_start_export", "启动关卡集导出，立即返回后必须轮询状态。", "build", "{\"type\":\"object\",\"properties\":{\"setNames\":{\"type\":\"array\"},\"setName\":{\"type\":\"string\"},\"mode\":{\"type\":\"string\"},\"depsVersion\":{\"type\":\"string\"},\"confirm\":{\"const\":true}},\"required\":[\"confirm\"]}", false, 300000, 1800000, true);
        AppendTool(sb, "oc2_get_export_status", "查询导出状态，建议每 2 秒轮询。", "read", "{}", true, 1000, 30000, false);
        sb.Append(']');
        return sb.ToString();
    }

    private static string StringSchema(string property)
    {
        return "{\"type\":\"object\",\"properties\":{\"" + property + "\":{\"type\":\"string\"}},\"required\":[\"" + property + "\"]}";
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
