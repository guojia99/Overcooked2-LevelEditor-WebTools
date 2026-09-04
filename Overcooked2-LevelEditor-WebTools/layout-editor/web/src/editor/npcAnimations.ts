import { S } from "./state";
import type { EditorItem } from "./state";
import { prefabIdFromPath } from "./coords";

/**
 * 自带移动动画的 NPC 知识表（来自原版 bundle 调研）：
 *  - 服务生：Waiter_Walk（0.625s 行走循环，36 骨骼绑定）+ Waiter_Idle（sushi_1_2 关卡）
 *  - 月亮 NPC：Moon_NPC_path / MoonNPC_Path_01（16~47s 循环路径）
 *  - 面包人：walkingbread_*_walking prefab（行走动画 + 路径标记）
 *  - 救生筏 NPC：NPC_Liferaft_01（4s 循环）+ NPC_Liferaft_Rotate（15s）
 *  - 行人：1_2_Pedestrians_01（186s 长路线）；城市关 NPC_Walk_*（37~67s 循环）
 */

export interface NpcAnimType {
  key: string;
  labelZh: string;
  /** 展示用 emoji。 */
  emoji: string;
  desc: string;
  evidence: string;
  /** 是否可直接从目录放置（自带动画），否则为关卡专属物体。 */
  placeable: boolean;
}

export const NPC_ANIM_TYPES: NpcAnimType[] = [
  {
    key: "waiter",
    labelZh: "服务生",
    emoji: "🍽️",
    desc: "自带行走循环动画 Waiter_Walk（0.625s 循环）+ 待机 Waiter_Idle",
    evidence: "Waiter_Walk / Waiter_Idle (sushi_1_2)",
    placeable: true,
  },
  {
    key: "moon",
    labelZh: "月亮 NPC",
    emoji: "🌙",
    desc: "月亮沿路径移动动画（16~47s 循环路线）",
    evidence: "Moon_NPC_path / MoonNPC_Path_01",
    placeable: false,
  },
  {
    key: "walkingbread",
    labelZh: "面包人",
    emoji: "🍞",
    desc: "自带行走动画 + 路径标记",
    evidence: "walkingbread_*_walking",
    placeable: false,
  },
  {
    key: "liferaft",
    labelZh: "救生筏 NPC",
    emoji: "🛟",
    desc: "救生筏循环动画（4s）+ 旋转动画（15s）",
    evidence: "NPC_Liferaft_01 / NPC_Liferaft_Rotate",
    placeable: false,
  },
  {
    key: "pedestrian",
    labelZh: "行人",
    emoji: "🚶",
    desc: "长路线行人动画（186s）；城市关 NPC 循环路线（37~67s）",
    evidence: "1_2_Pedestrians_01 / NPC_Walk_*",
    placeable: false,
  },
];

/** 空态时展示的「已知自带动画 NPC 类型」说明（美化卡片列表）。 */
export function npcTypesHintHtml(): string {
  const cards = NPC_ANIM_TYPES.map(
    (t) => `<div class="npc-type-card${t.placeable ? " npc-type-placeable" : ""}">
      <div class="npc-type-head"><span class="npc-type-emoji">${t.emoji}</span><span class="npc-type-name">${t.labelZh}</span></div>
      <div class="npc-type-desc">${t.desc}</div>
      <div class="npc-type-ev">${t.evidence}</div>
    </div>`
  ).join("");
  return `<div class="npc-types-grid">${cards}</div>
    <div class="npc-types-note">✅ 服务生为目录内可直接放置的自带动画 NPC · 🔒 月亮 / 面包人 / 行人为关卡专属物体</div>`;
}

export function isNpcAnimItem(id: string, nameZh: string): boolean {
  const s = `${id} ${nameZh}`;
  return (
    /NPC_Walk_/i.test(id) ||
    /_Waiter_/.test(id) ||
    /^npc_waiter/i.test(id) ||
    /moon/i.test(s) ||
    /月亮/.test(s) ||
    /walkingbread/i.test(s) ||
    /面包人/.test(s) ||
    /liferaft/i.test(s) ||
    /pedestrian/i.test(s) ||
    /行人/.test(s)
  );
}

/** 场景中自带移动动画的 NPC 实例。 */
export function sceneNpcAnimItems(): EditorItem[] {
  return S.items.filter((it) => {
    const id = prefabIdFromPath(it.prefabAssetPath) || it.instanceId;
    return isNpcAnimItem(id, it.displayName);
  });
}
