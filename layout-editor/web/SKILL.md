# Web 编辑器开发规范

## 项目结构

```
layout-editor/web/
├── public/              # 静态数据文件（JSON），构建时原样复制到 dist/
│   ├── recipes.json     # 菜谱定义
│   ├── ingredients.json # 食材定义
│   ├── catalog.json     # 道具/物件目录
│   └── ...
├── src/                 # TypeScript 源代码
│   ├── main.ts          # 主入口，包含 STEP_UTENSILS 等核心逻辑
│   ├── types.ts         # 类型定义
│   ├── stacking.ts      # 锅具叠加规则
│   ├── autoScore.ts     # 自动评分
│   ├── api.ts           # API 通信（含菜谱运行时修正）
│   ├── customRecipes.ts # 自定义菜谱创建/编辑
│   └── ...
├── dist/                # **构建产物，禁止直接编辑**
│   ├── assets/          # 打包后的 JS（如 index-BR0jkxM8.js）
│   ├── recipes.json     # public/ 的副本
│   ├── catalog.json     # public/ 的副本
│   └── ...
├── package.json         # 依赖与脚本
├── vite.config.ts       # Vite 构建配置
└── SKILL.md             # 本文件
```

## 核心规则

### 禁止编辑 dist/

`dist/` 目录下的所有文件（包括 `dist/assets/*.js`）都是 `vite build` 的输出产物。**禁止直接编辑 dist/ 中的任何文件**——修改会在下次构建时被覆盖。

### 修改后必须重新构建

对 `src/` 中 TypeScript 源码做任何改动后，**必须执行构建**才能让 dist/ 生效：

```bash
cd layout-editor/web && npm run build
```

### 正确的修改流程

| 改什么 | 改哪里 | 是否需要构建 |
|---|---|---|
| TypeScript 逻辑、UI 行为 | `src/*.ts` | ✅ `npm run build` |
| 静态菜谱/食材/物件数据 | `public/*.json` | 无需构建（public/ 直接复制到 dist/），但需同步更新 dist/ |
| 菜谱知识库验证规则 | `scripts/data/recipe-knowledge.json` | 无需构建（仅 build-catalog 使用） |
| 命名字典 | `scripts/data/names-dictionary.json` | 无需构建（仅 build-catalog 使用） |
| 图标映射 | `scripts/data/recipe-icons.json` | 无需构建（仅 build-catalog 使用） |

### 开发环境

```bash
cd layout-editor/web && npm run dev   # 启动开发服务器（端口 5173）
```

开发服务器启用 HMR 热更新，无需手动构建。

### 静态 JSON 数据同步

`public/*.json` 文件在构建时会复制到 `dist/`。如果编辑了 `public/` 中的 JSON，需要同步更新 `dist/` 中的副本（或直接 `npm run build`）。
