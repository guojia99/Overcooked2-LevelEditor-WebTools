# OC2 关卡编辑器 MCP

本目录描述 Overcooked2 关卡编辑器的 MCP 接口。完整能力的 MCP Server 位于
`layout-editor/mcp/`，通过 stdio 与 AI/agent 通信，再调用 Unity Editor Bridge；静态网页和其他 HTTP 客户端也可以直接调用 Unity Bridge 的原生 HTTP MCP：

```text
AI / agent --stdio MCP--> layout-editor/mcp --> HTTP --> http://10.211.55.3:8765
静态网页/HTTP 客户端 --MCP JSON-RPC--> http://10.211.55.3:8765/api/mcp
```

## 快速启动

先确认 Unity Editor 中 Layout Editor Bridge 已启动，再执行：

```bash
cd layout-editor/mcp
npm install
npm run build
node dist/server.js
```

默认 Bridge 地址为 `http://10.211.55.3:8765`，可用环境变量覆盖：

```bash
OC2_EDITOR_BRIDGE_URL=http://10.211.55.3:8765 node dist/server.js
```

不安装 Node 时使用原生 HTTP MCP：

```text
GET  http://10.211.55.3:8765/api/mcp/manifest
GET  http://10.211.55.3:8765/api/mcp/tools
POST http://10.211.55.3:8765/api/mcp
```

HTTP MCP 支持 `initialize`、`tools/list` 和 `tools/call`。它提供无状态读取、场景验证、完整场景提交和导出启动/状态查询；草稿工具仍由 stdio Server 持有，因为草稿状态保存在 MCP Server 进程内。导出不提供同步等待接口，客户端应按清单中的 `pollAfterMs=2000` 轮询 `oc2_get_export_status`。

OpenCode 配置示例：

```json
{
  "mcp": {
    "oc2-level-editor": {
      "type": "local",
      "command": ["node", "layout-editor/mcp/dist/server.js"],
      "environment": {
        "OC2_EDITOR_BRIDGE_URL": "http://10.211.55.3:8765"
      },
      "enabled": true
    }
  }
}
```

## 调用顺序

1. 调用 `oc2_editor_health` 确认 Unity Bridge 在线。
2. 调用 `oc2_list_level_sets` 和 `oc2_list_levels` 选择关卡。
3. 使用后端返回的 `sceneAssetPath` 调用 `oc2_get_scene_layout`。
4. 需要修改时调用 `oc2_create_scene_draft`。
5. 用 `oc2_apply_scene_patch` 修改草稿。
6. 调用 `oc2_validate_scene_draft` 和 `oc2_diff_scene_draft`。
7. 只有确认差异正确后，才调用 `oc2_commit_scene_draft` 并传 `confirm: true`。
8. 导出时调用 `oc2_start_export`，再每 2 秒调用 `oc2_get_export_status`。

所有工具的机器可读定义在 `../mcp/tool-manifest.json`。

## 文档索引

- [快速开始](01-快速开始.md)
- [架构说明](02-架构说明.md)
- [工具总览](03-工具总览.md)
- [调用模型](04-调用模型.md)
- [场景编辑接口](05-场景编辑接口.md)
- [菜谱接口](06-菜谱接口.md)
- [异步任务与导出](07-异步任务与导出.md)
- [错误与重试](08-错误与重试.md)
- [安全与确认](09-安全与确认.md)
