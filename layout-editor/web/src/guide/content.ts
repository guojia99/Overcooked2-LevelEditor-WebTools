import type { GuideBlock, GuideNode } from "./types";

function feat(
  id: string,
  title: string,
  what: string,
  where: string,
  how: string | string[],
  note?: string
): GuideNode {
  const blocks: GuideBlock[] = [
    { type: "paragraph", text: `<b>是什么：</b>${what}` },
    { type: "paragraph", text: `<b>在哪：</b>${where}` },
  ];
  if (Array.isArray(how)) {
    blocks.push({ type: "paragraph", text: "<b>怎么用：</b>" });
    blocks.push({ type: "steps", items: how });
  } else {
    blocks.push({ type: "paragraph", text: `<b>怎么用：</b>${how}` });
  }
  if (note) blocks.push({ type: "callout", text: note });
  return { id, title, blocks };
}

function branch(id: string, title: string, children: GuideNode[], intro?: GuideBlock[]): GuideNode {
  return { id, title, blocks: intro, children };
}

export const GUIDE_TREE: GuideNode[] = [
  branch("start", "快速上手", [
    feat(
      "start-bridge",
      "启动 Unity 桥接",
      "浏览器编排页通过 HTTP 与 Unity Editor 通信，读写场景布局与关卡数据。",
      "Unity 菜单 <code>Tools → Layout Editor → Open Bridge</code>（或 Open Web UI）。",
      [
        "用 Unity 2017.4.8f1 打开工程",
        "打开 Bridge 窗口，点击「启动服务」",
        "浏览器访问 http://127.0.0.1:8765/ 或点「在浏览器中打开编排页」",
        "状态栏显示连接成功后再选关卡加载",
      ],
      "Windows 日常使用无需 Node/npm，仓库已含预构建 dist/。"
    ),
    feat(
      "start-save",
      "写回与保存",
      "将浏览器中的布局、地板、装饰等写回 Unity 场景文件。",
      "关卡编辑器顶栏「💾 写回 Unity」。",
      [
        "确认当前场景已加载且无未保存冲突",
        "点击写回；Bridge 会自动 Prepare For Building 并 Reload Pseudo Assets",
        "回到 Unity 按 Ctrl+S 保存 .unity 场景",
        "Play 模式验证玩法",
      ]
    ),
    feat(
      "start-publish",
      "发布流程",
      "关卡集打包给玩家使用前需构建 AssetBundle。",
      "Unity 编辑器（非 Web）。",
      [
        "编排并 Play 验证",
        "Toggle Prepare For Building",
        "保存场景 → Build AssetBundles",
      ],
      "Web 工具不修改 LevelInfoSO 的 bundle 标记或 common* 宿主资源。"
    ),
  ]),

  branch("nav", "顶栏导航", [
    feat(
      "nav-tabs",
      "五个主页面",
      "同一套顶栏在全部页面间跳转，各自独立路由。",
      "顶栏按钮（Overcooked!2 关卡工具 右侧）。",
      [
        "🗺️ 关卡编辑器 — 俯视图编排（#/layout）",
        "📋 关卡管理 — 关卡集/关卡 CRUD（#/manage）",
        "🍽️ 自定义菜谱 — 按关卡集管理 CustomRecipeSO（#/custom-recipes）",
        "📖 菜谱清单列表 — 只读浏览全部菜谱（/recipes）",
        "📘 功能说明 — 本页（#/guide）",
      ]
    ),
    feat(
      "nav-scene",
      "关卡集 / 关卡下拉",
      "在编排页快速切换要编辑的场景。",
      "仅「关卡编辑器」顶栏右侧。",
      "先选关卡集，再选关卡；切换会加载对应 scene layout。",
      "从「关卡管理」点「打开布局」也会带目标场景进入编排页。"
    ),
    feat(
      "nav-about",
      "关于 · 作者",
      "工具简介与 GitHub 链接。",
      "顶栏最右侧 GitHub 按钮。",
      "点击打开弹窗，可跳转 GitHub 仓库获取更新说明。"
    ),
  ]),

  branch("layout", "关卡编辑器", [
    branch("layout-io", "场景加载与写回", [
      feat("layout-reload", "重新加载", "丢弃未写回的本地改动，从 Unity 重新拉取场景。", "顶栏「🔄 重新加载」。", "有未保存修改时会提示确认。"),
      feat("layout-save-all", "写回 Unity", "保存全部层：核心物品、装饰、地板、背景、移动组等。", "顶栏「💾 写回 Unity」。", "写回前会校验工作台重叠（可勾选允许）。"),
      feat(
        "layout-save-scope",
        "仅核心物品 / 分层写回",
        "只写回当前层相关数据，避免误改其他层。",
        "顶栏「🎯 仅核心物品」（标签随当前层变化：仅装饰 / 仅地板+背景 等）。",
        "在装饰层或地板层编辑后，用此按钮可只提交该层变更。"
      ),
      feat(
        "layout-repair",
        "修复损坏",
        "删除源 prefab 缺失的实例，避免 Play 时 NullReferenceException。",
        "顶栏「🔧 修复损坏」。",
        "仅移除损坏实例，不自动补回替代品。"
      ),
      feat(
        "layout-deps",
        "依赖检查",
        "检查 Bridge、静态页、知识库、音频导出、bundle 等是否就绪。",
        "顶栏「🩺 依赖检查」。",
        "音频/bundle 缺失不影响布局编辑，但试听与分析会不可用。"
      ),
      feat(
        "layout-test",
        "测试布局",
        "一键生成 30×16 地板、默认 FOV、全部食材箱与核心道具的测试场景。",
        "顶栏「🧪 测试布局」。",
        "会清空现有布局（保留玩家），操作前请确认。"
      ),
    ]),
    branch("layout-toolbar", "工具栏弹窗", [
      feat(
        "layout-recipes",
        "菜谱",
        "为当前关卡选择 LevelInfoSO.recipes，并自动填充缺失道具。",
        "顶栏「📖 菜谱」。",
        [
          "选择菜谱 Tab：按类型勾选，保存到关卡",
          "已选菜谱 Tab：查看当前绑定",
          "自动填充 Tab：按所选菜谱补齐食材箱、锅具、饮料机组合等",
        ]
      ),
      feat(
        "layout-utensils",
        "锅具管理",
        "查看场景中所有锅具实例，编辑容量与 allowedIngredientSOs，批量同步。",
        "顶栏「🍳 锅具管理」。",
        "「按菜谱自动填充」根据已选菜谱追加中间产物到对应锅具。"
      ),
      feat("layout-config", "关卡配置", "编辑 1P~4P 分数、时长、订单节奏与关卡截图。", "顶栏「📊 关卡配置」或关卡管理页同名按钮。", "含「一键定分」与截图裁剪上传。"),
      feat("layout-camera", "相机/灯光", "修改游戏相机背景色、FOV；编辑 Art/Lights 下灯光颜色与强度。", "顶栏「🎥 相机/灯光」。", "FOV 变更会即时反映在画布相机视野框上。"),
      feat("layout-audio", "音频", "配置 BGM、氛围音、音效集、死亡特效。", "顶栏「🔊 音频」。", "需场景已加载；保存时会打开并保存 Unity 场景。"),
      feat("layout-summary", "汇总", "查看本关菜谱卡片汇总并导出 PNG。", "顶栏「📋 汇总」。", "会离开编排页进入汇总视图。"),
      feat(
        "layout-sync",
        "同步布局",
        "从同关卡集另一关卡复制物品、地板与背景主题（仅前端，写回后生效）。",
        "顶栏「📥 同步布局…」。",
        "传送门/上菜台 ID 会尝试重映射；可用 Ctrl+Z 撤回一次。"
      ),
    ]),
    branch("layout-layers", "五层编辑", [
      feat("layer-decor", "装饰层", "编辑 art/NPC 等装饰物，调色板仅显示装饰分组。", "第二层 Tab「🎀 装饰层」。", "非活动层半透明不可点选。"),
      feat("layer-items", "核心层", "编辑桌台、厨具、机关、厨师等玩法物品。", "Tab「📦 核心层」。", "左侧调色板按工作流与配套设备聚类。"),
      feat("layer-floor", "地板层", "编辑 Floor 平面：移动、缩放、材质、新增地板。", "Tab「🗺️ 地板层」。", "地板栏可新增实心/主题/空气地板；右键打开地板编辑器。"),
      feat("layer-bg", "背景层", "编辑水面、天空、环境特效等背景 prefab。", "Tab「🌊 背景层」。", "背景类 prefab 拖入画布会自动归入此层。"),
      feat("layer-move", "移动层", "编辑移动组与路点；右侧自动切到移动控制 Tab。", "Tab「🎬 移动层」。", "左侧调色板在此层为空。"),
    ]),
    branch("layout-view", "视图与编辑选项", [
      feat("view-vis", "图层显示", "控制当前层可见类别：核心/装饰/地板/背景。", "第二行「👁 图层显示」。", "关闭的类别不可见也不可点选。"),
      feat("view-snap", "吸附格子", "距半格网格 0.1 内自动吸附。", "「🧲 吸附格子」复选框。", "半格 0.6 用于两格宽桌台对齐。"),
      feat("view-step", "精度", "自由摆放与微移步长。", "精度下拉 0.1 / 0.01 / 0.001。", "写回时可带 snap 参数。"),
      feat("view-grid", "显示网格", "画布网格与 QuadGridManager 蓝框。", "「👁 显示网格」。", ""),
      feat("view-fov", "相机视野", "估算 16:9 游戏相机视锥投影。", "「🎥 相机视野」。", ""),
      feat("view-coords", "坐标系", "悬停显示世界坐标。", "「📏 坐标系」。", ""),
      feat("view-overlap", "允许工作台重叠", "写回时不阻止工作台重叠。", "「⚠ 允许工作台重叠」。", "默认关闭，重叠可能导致玩法问题。"),
      feat("view-inter", "自动中间产物", "补齐锅具时自动分配煎肉排、面糊等到 allowedIngredientSOs。", "「🧩 自动中间产物」。", ""),
    ]),
    branch("layout-canvas", "画布操作", [
      feat(
        "canvas-select",
        "选择与框选",
        "点选、Shift 加选、空白拖拽框选；重叠处弹出选择列表。",
        "画布区域。",
        "点击右侧物品清单条目也会选中并平移视图。"
      ),
      feat(
        "canvas-pan-zoom",
        "平移与缩放",
        "空格+拖动、中键或 Alt+拖动平移；滚轮缩放。",
        "画布。",
        "缩放范围约 0.25×~4×。"
      ),
      feat(
        "canvas-edit",
        "移动 / 旋转 / 删除",
        "拖拽移动；R / Shift+R 旋转 90°；Del 删除。",
        "画布 + 键盘。",
        "右键菜单可精确输入坐标、角度、高度。"
      ),
      feat(
        "canvas-clipboard",
        "复制粘贴",
        "Ctrl+C/V/X 复制/粘贴/裁切；Ctrl+Z / Ctrl+Shift+Z 撤回重做。",
        "键盘快捷键。",
        "地板层支持 Ctrl+D 复制地板。"
      ),
      feat(
        "canvas-height",
        "高度层过滤",
        "按行走面高度分层显示物品/地板/装饰。",
        "画布左下「📐 高度层」按钮（地板/核心/装饰层可用）。",
        "可调层厚与双滑块区间。"
      ),
    ]),
    branch("layout-palette", "调色板与组合", [
      feat("palette-search", "搜索", "按中文名或 prefab ID 过滤。", "左侧调色板顶栏搜索框。", "占位符随当前层变化。"),
      feat("palette-decor-size", "装饰尺寸筛选", "按实测尺寸分 小/中/大/特大。", "装饰层专用下拉。", ""),
      feat(
        "palette-anim-decor",
        "动画装饰标记",
        "调色板对 prefab 内嵌 Animator 的装饰显示徽章：🎞 为 NPC 行走/路径动画，✨ 为环境动画（蝴蝶、灯笼、水车、香炉等）。",
        "装饰层左侧调色板卡片标题旁。",
        [
          "悬停卡片可查看动画类型说明",
          "右侧物品清单在装饰层分开展示「自带行走动画的 NPC」与「自带环境动画的装饰」",
          "自定义轨迹动画请在「移动层」创建 MoveControl 组，与 prefab 内嵌动画无关",
        ]
      ),
      feat("palette-combo", "联合组合", "一次拖入配套组合（如上菜台+脏盘台、饮料机+开关）。", "调色板「联合组合」分组。", "开关组合默认使用组合模式放置。"),
      feat("palette-variant", "皮肤变体", "同功能换肤的 prefab 可右键切换。", "放置后右键「外观/变体」。", "调色板条目 ×N 表示多种皮肤。"),
    ]),
    branch("layout-floor", "地板系统", [
      feat("floor-add", "新增地板", "放置实心、主题或空气地板。", "地板层左侧「+ 新增…」与画布底部地板栏。", "空气地板仅碰撞无视觉。"),
      feat("floor-bar", "地板栏", "背景主题、扩大坠落区、同步可行走到地板。", "画布底部 floor-bar。", "右键地板打开详细编辑器。"),
      feat(
        "floor-modal",
        "地板编辑器",
        "改尺寸、材质、染色、图片贴图、类型切换。",
        "右键地板 → 详情或地板编辑器。",
        "改高度时会联动抬起其上物品。"
      ),
    ]),
    branch("layout-panels", "右侧面板", [
      feat("panel-items", "物品清单", "按分类列出当前层物品，点击选中。", "右侧「📋 物品清单」Tab。", "装饰层另有 NPC 与环境动画装饰清单。"),
      feat("panel-move", "移动控制", "管理移动组：成员、事件路线、路点池、触发器。", "右侧「🎯 移动控制」Tab。", "详见下级「移动控制」。"),
      feat("panel-bevents", "按钮事件组", "为开关/压力开关配置多组事件广播。", "右侧「🔘 按钮事件组」Tab。", "与按钮联动配置配合使用。"),
    ]),
    branch("layout-move", "移动控制", [
      feat("move-create", "创建移动组", "将物品/地板编组做序列动画。", "移动控制 →「新增分组」或右键「创建移动组」。", ""),
      feat("move-members", "成员", "地图选点加入组；可设跟随路点与偏移。", "组编辑器「成员」Tab。", ""),
      feat("move-events", "事件", "移动/抬起/落下/等待；路点路线与时间曲线。", "组编辑器「事件」Tab。", "支持循环与往返。"),
      feat("move-waypoints", "路点", "路点池统一管理，编入事件路线。", "组编辑器「路点」Tab。", "画布可放置新路点。"),
      feat("move-settings", "设置", "启动延迟、循环、触发器、根运动等。", "组编辑器「设置」Tab。", "「预览路线」在画布播放前端预览。"),
    ]),
    branch("layout-buttons", "按钮联动", [
      feat("btn-link", "联动移动组", "开关按压按顺序触发多个移动组。", "右键开关/压力开关 → 联动移动组配置。", "可锁定至组播放完毕。"),
      feat("btn-pair", "共轭按钮", "两个开关互斥，各绑定两组移动。", "联动配置中的共轭模式。", ""),
      feat("btn-events", "按钮事件组", "一次按压向多目标广播触发器。", "右侧按钮事件组 Tab 或 stub 联动事件组。", ""),
    ]),
    branch("layout-stub", "右键参数（Stub）", [
      feat(
        "stub-dispenser",
        "食材箱 / 生成器",
        "配置产出食材；饮料机/酱料机可设多输出循环。",
        "右键 Dispenser / Backpack / AttachingFoodSpawner。",
        ""
      ),
      feat("stub-utensil", "锅具", "容量与额外 allowedIngredientSOs。", "右键 CookingUtensil。", ""),
      feat("stub-switch", "开关 / 压力开关", "初始状态、材质、联动目标与事件组。", "右键 Switch / PressureSwitch。", ""),
      feat("stub-serve", "上菜台 / 回收", "绑定脏盘台、脏杯台、回收台等。", "右键 ServingStation / PlateReturn。", "盘与托盘回收互斥。"),
      feat("stub-other", "其他", "传送门配对、传送带速度与方向、喷火器、大炮、玩家编号等。", "右键对应物品。", "详情面板只读摘要，编辑在右键菜单。"),
    ]),
    branch("layout-shortcuts", "快捷键速查", [], [
      {
        type: "kbdTable",
        rows: [
          ["拖拽空白", "框选"],
          ["Shift+点击", "加选 / 减选"],
          ["Ctrl+C / V / X", "复制 / 粘贴 / 裁切"],
          ["Ctrl+Z / Ctrl+Shift+Z", "撤回 / 重做"],
          ["Del / Backspace", "删除选中"],
          ["R / Shift+R", "旋转 90°"],
          ["方向键", "按当前精度微移"],
          ["空格+拖动", "平移画布"],
          ["滚轮", "缩放"],
          ["Esc", "关闭浮层 / 取消放置"],
        ],
      },
    ]),
  ]),

  branch("manage", "关卡管理", [
    branch("manage-set", "关卡集", [
      feat("set-list", "列表与新建", "管理 LevelSetInfoSO：名称、作者、版本。", "顶栏「📋 关卡管理」→ 关卡集卡片。", "新建会自动创建 data/、scenes/ 目录。"),
      feat("set-edit", "编辑信息", "修改中英文名、作者、version（改 version 重算 uid）。", "卡片「编辑信息」。", "关卡集删除需输入标识二次确认。"),
      feat("set-export", "导出 zip", "打包关卡集供 BepInEx 插件使用。", "卡片「导出」。", "解压到 BepInEx/plugins/OC2DIYLevel/levels/。"),
    ]),
    branch("manage-level", "关卡", [
      feat("level-create", "新建关卡", "生成 config_1p~4p、LevelInfo、复制模板场景。", "关卡列表「+ 新建关卡」。", ""),
      feat("level-reorder", "调整顺序", "拖拽或按钮调整关卡在集内顺序。", "「⇅ 调整顺序」。", ""),
      feat("level-edit", "编辑基础信息", "sceneName、dependencies、订单数量等。", "关卡卡片「编辑」。", "dependencies 填 bundle 名，木筏 BGM 常需 bundle11。"),
      feat("level-delete", "删除关卡", "永久删除场景、配置与目录内自定义资源。", "「删除」二次确认。", ""),
      feat("level-layout", "打开布局", "跳转到编排页并加载该场景。", "「打开关卡编辑器」。", ""),
    ]),
    branch("manage-config", "人数配置", [
      feat(
        "cfg-score",
        "分数与节奏",
        "1P~4P 各星分数、订单超时、间隔、关卡时长等。",
        "「📊 关卡配置」弹窗，四个 Tab。",
        "「一键定分」根据已选菜谱与时长推算星级。"
      ),
      feat("cfg-shot", "关卡截图", "裁剪画布区域上传为关卡缩略图。", "关卡配置 → 截图 Tab。", "与 manage 列表卡片预览共用。"),
    ]),
    branch("manage-audio", "音频配置", [
      feat(
        "audio-tabs",
        "音乐 / 音效 / 氛围",
        "BGM、强制音效集、可选音效集、氛围音；主题推荐与缺口检查。",
        "关卡详情或编排页「🔊 音频」。",
        "许多 BGM 需 dependencies 额外 bundle；可先「导出音频依赖」。"
      ),
    ]),
    branch("manage-summary", "汇总导出", [
      feat("sum-page", "关卡汇总", "按类型展示本关菜谱卡片。", "「📋 汇总」按钮。", ""),
      feat("sum-png", "导出图片", "将汇总页渲染为 PNG。", "汇总页「一键导出图片」。", ""),
    ]),
  ]),

  branch("custom", "自定义菜谱", [
    feat("cr-pick-set", "选择关卡集", "每个关卡集有独立 custom_recipes/ 目录。", "进入页后选关卡集卡片。", "首次进入自动初始化配置。"),
    branch("custom-list", "列表", [
      feat("cr-search", "搜索与筛选", "按菜名/ID/食材搜索；成品/中间产物筛选；分类侧栏。", "列表顶栏与侧栏。", ""),
      feat("cr-card", "菜谱卡片", "与菜谱清单相同卡片样式；底部 3D 预览/编辑/删除。", "网格列表。", ""),
    ]),
    branch("custom-form", "新建 / 编辑", [
      feat("cr-compose", "组成", "添加食材或引用其他菜谱（可重复数量）。", "「＋ 添加食材 / 菜谱」。", "顶部实时预览组装效果。"),
      feat("cr-cook", "烹饪与装盘", "Composite/Cooked/Mixed；步骤、分数、装盘容器。", "表单「烹饪与装盘」区。", ""),
      feat("cr-model", "图标与 3D 模型", "上传 PNG 图标；FBX/OBJ 模型与贴图；在线预览调尺寸。", "模型区与「预览并调整」。", "尺寸单位 cm；1 模型单位 = 1 m。"),
      feat("cr-mid", "中间产物", "score=0 的菜谱可被其他菜谱引用。", "「+ 新建中间产物」。", "与官方中间产物一样可配图标与模型。"),
    ]),
  ]),

  branch("recipes-page", "菜谱清单列表", [
    feat(
      "rl-recipes",
      "菜谱视图",
      "全部菜谱按类型分组，卡片展示烹饪步骤与食材。",
      "顶栏「📖 菜谱清单列表」或 /recipes。",
      "支持搜索、来源、分数、类型 chips 筛选。"
    ),
    feat(
      "rl-ingredients",
      "食材清单视图",
      "按 food group 列出全部食材与图标。",
      "工具栏「食材清单」按钮。",
      "「含半成品」可显示 score≤0 的中间产物。"
    ),
    feat("rl-dedup", "隐藏 DLC 重复", "同一道菜只保留最高 DLC 换皮版本。", "「隐藏DLC重复」默认开启。", ""),
  ]),

  branch("icons-help", "食材与图标说明", [
    feat(
      "icons-path",
      "图标路径约定",
      "静态 PNG 由 extract-icons 解包到 dist/icons/。",
      "本页下方表格。",
      "custom 菜谱图标经 Bridge API 读取，不在 /icons/recipes/。"
    ),
    branch("icons-samples", "精选示例", [], [
      { type: "dynamic", kind: "icon-paths" },
      { type: "paragraph", text: "<b>代表食材（按来源）</b> — 完整列表请打开菜谱清单页的「食材清单」视图。" },
      { type: "dynamic", kind: "ingredient-samples" },
      { type: "paragraph", text: "<b>代表菜谱</b> — 成品图 + 主要组成食材图标：" },
      { type: "dynamic", kind: "recipe-samples" },
      { type: "paragraph", text: "<b>常见烹饪步骤 / 锅具图标</b>（菜谱卡片组尾显示）：" },
      { type: "dynamic", kind: "utensil-icons" },
      { type: "link", label: "查看全部菜谱与食材 → 菜谱清单列表", href: "/recipes" },
    ]),
  ]),

  branch("env", "环境依赖", [
    feat(
      "env-check",
      "依赖检查项",
      "后端、静态页、recipe-knowledge、names-dictionary、game bundle、音频导出、dump manifest。",
      "编排页「🩺 依赖检查」。",
      "任一项失败会给出修复提示。"
    ),
    feat(
      "env-offline",
      "Bridge 离线",
      "Bridge 未启动时前端回退 dist/ 内静态 JSON。",
      "自动。",
      "写回/读场景需 Bridge 在线。"
    ),
    feat(
      "env-maintain",
      "维护者刷新目录",
      "更新 catalog/recipes 等需 Node 跑 build-catalog。",
      "维护者文档 layout-editor/README.md。",
      "改 src/ 后需 npm run build 更新 dist/。"
    ),
  ]),
];
