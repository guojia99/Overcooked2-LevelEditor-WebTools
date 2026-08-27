import { S } from "./state";
import type { EditorItem } from "./state";
import { prefabIdFromPath } from "./coords";
import { isNpcAnimItem } from "./npcAnimations";

/**
 * Prefab 内嵌环境动画装饰知识表。
 *
 * 抽样验证（common03 butterflies_flightpath / flyinglanterns / waterwheel）：
 * 编辑器 prefab 为 PseudoPrefabStub + PseudoPrefab，Animator 在 bundle 子物体上，
 * Play 时由 PseudoPrefab.ResetChild 实例化并自动循环，不在 stub YAML 里直接挂 Animator。
 *
 * common01 Space_Door_Airlock_* 则在 bundle 子物体带 Animator，由按钮 trigger 驱动。
 */

export type BuiltinAnimKind = "npc" | "env";

export interface EnvAnimDecorFamily {
  key: string;
  labelZh: string;
  emoji: string;
  desc: string;
  examples: string;
  test: (id: string, nameZh: string) => boolean;
}

/** 环境动画装饰家族（按匹配优先级排列，先匹配先返回）。 */
export const ENV_ANIM_FAMILIES: EnvAnimDecorFamily[] = [
  {
    key: "butterfly",
    labelZh: "蝴蝶",
    emoji: "🦋",
    desc: "飞行轨迹 / 地面 / 悬停蝴蝶，bundle 内 Animator 循环",
    examples: "butterflies_flightpath, ground_butterfly, p_dlc08_balloon_butterfly_01",
    test: (id, zh) => /butterfl|蝴蝶/i.test(`${id} ${zh}`),
  },
  {
    key: "flying_lantern",
    labelZh: "飞天灯笼",
    emoji: "🏮",
    desc: "空中飘移灯笼组，放置即自动播放",
    examples: "flyinglanterns, flyinglanternsgroup, p_flyingboxlantern_01",
    test: (id) => /flyinglantern|flyingboxlantern/i.test(id),
  },
  {
    key: "floating_lantern",
    labelZh: "漂浮灯笼",
    emoji: "🎐",
    desc: "空中或水面漂浮灯笼",
    examples: "m_dlc4_lantern_floating_air_01, p_floatingboxlantern_01",
    test: (id) => /lantern_floating|floatingboxlantern/i.test(id),
  },
  {
    key: "incense",
    labelZh: "香炉",
    emoji: "🪔",
    desc: "香炉烟雾循环动画",
    examples: "p_dlc4_incense_pot, p_dlc4_map_incense",
    test: (id, zh) => /incense|香炉/i.test(`${id} ${zh}`),
  },
  {
    key: "lily",
    labelZh: "睡莲",
    emoji: "🪷",
    desc: "睡莲叶漂浮动画",
    examples: "p_dlc4_lilypods_01",
    test: (id) => /lilypod/i.test(id),
  },
  {
    key: "waterwheel",
    labelZh: "水车",
    emoji: "⚙️",
    desc: "旋转水车（dlc11 夏日主题）",
    examples: "waterwheel",
    test: (id, zh) => /waterwheel|水车/i.test(`${id} ${zh}`),
  },
  {
    key: "traffic_animated",
    labelZh: "循环交通灯",
    emoji: "🚦",
    desc: "自动循环红绿灯（与 common01 静态灯不同）",
    examples: "dlc11_traffic_light_animated_01",
    test: (id) => /traffic_light_animated/i.test(id),
  },
  {
    key: "sea_float",
    labelZh: "水面浮动",
    emoji: "🌊",
    desc: "鱼灯、海草、海浪、浮冰等水面漂浮物",
    examples: "p_dlc13_fishfloat_01, p_seaweedfloat_01, wavefloat_generic",
    test: (id) =>
      /fishfloat|seafloat|seaweedfloat|seawavefloat|wavefloat|genericfloat|baloon_float|log_float|raft_float|float_playable|1_2_float/i.test(
        id
      ),
  },
  {
    key: "space_door",
    labelZh: "太空舱门",
    emoji: "🚪",
    desc: "可开关门动画，需配合 Switch / PressureSwitch trigger",
    examples: "Space_Door_Airlock_Open, Space_Door_Airlock_Bool_Close",
    test: (id) => /Space_Door_Airlock/i.test(id),
  },
  {
    key: "water_fx",
    labelZh: "水面",
    emoji: "💧",
    desc: "水面片 / 木筏水面 shader 或循环动画",
    examples: "Water_01, raft_water, dynamic_raft_water, poolwater_01",
    test: (id) => /raft_water|poolwater|Water_01|dynamic_raft_water|camp_water/i.test(id),
  },
  {
    key: "particle",
    labelZh: "粒子特效",
    emoji: "💨",
    desc: "海滩沙雾、水花、尾迹等粒子 prefab",
    examples: "PFX_sand_01, PFX_RaftWaterWake_001, SeaShore_PFX_02",
    test: (id) => /^(PFX_|WaterSplash)|SeaShore_PFX/i.test(id),
  },
  {
    key: "circus_npc",
    labelZh: "马戏团 NPC",
    emoji: "🎪",
    desc: "喷火 / 杂耍 / 大力士等表演循环动画",
    examples: "dlc08_NPC_FireBreather01, dlc08_NPC_Juggler01",
    test: (id) => /NPC_(FireBreather|Juggler|Strongman)/i.test(id),
  },
];

export interface BuiltinAnimDecorMatch {
  kind: BuiltinAnimKind;
  family?: EnvAnimDecorFamily;
  emoji: string;
  badgeZh: string;
  title: string;
}

/** 匹配环境动画装饰家族；不匹配 NPC（NPC 由 npcAnimations 处理）。 */
export function matchEnvAnimDecor(id: string, nameZh: string): EnvAnimDecorFamily | null {
  if (isNpcAnimItem(id, nameZh)) return null;
  const s = `${id} ${nameZh}`;
  for (const fam of ENV_ANIM_FAMILIES) {
    if (fam.test(id, nameZh)) return fam;
  }
  return null;
}

export function isEnvAnimDecorItem(id: string, nameZh: string): boolean {
  return matchEnvAnimDecor(id, nameZh) !== null;
}

/** 调色板 / 物品清单用：NPC 或环境动画装饰。 */
export function matchBuiltinAnimDecor(id: string, nameZh: string): BuiltinAnimDecorMatch | null {
  if (isNpcAnimItem(id, nameZh)) {
    return {
      kind: "npc",
      emoji: "🎞",
      badgeZh: "自带移动动画",
      title: "自带移动动画（行走循环 / 路径动画）",
    };
  }
  const family = matchEnvAnimDecor(id, nameZh);
  if (!family) return null;
  return {
    kind: "env",
    family,
    emoji: "✨",
    badgeZh: `自带环境动画 · ${family.labelZh}`,
    title: `${family.labelZh}：${family.desc}`,
  };
}

export function isBuiltinAnimDecorItem(id: string, nameZh: string): boolean {
  return matchBuiltinAnimDecor(id, nameZh) !== null;
}

/** 空态说明：环境动画装饰家族卡片。 */
export function envAnimTypesHintHtml(): string {
  const cards = ENV_ANIM_FAMILIES.map(
    (t) => `<div class="npc-type-card env-anim-type-card">
      <div class="npc-type-head"><span class="npc-type-emoji">${t.emoji}</span><span class="npc-type-name">${t.labelZh}</span></div>
      <div class="npc-type-desc">${t.desc}</div>
      <div class="npc-type-ev">${t.examples}</div>
    </div>`
  ).join("");
  return `<div class="npc-types-grid">${cards}</div>
    <div class="npc-types-note">调色板中带 ✨ 徽章的条目为 prefab 内嵌 Animator，放置即用；轨迹可在「移动层」自建 MoveControl 组覆盖。</div>`;
}

/** 场景中环境动画装饰实例（不含 NPC）。 */
export function sceneEnvAnimDecorItems(): EditorItem[] {
  return S.items.filter((it) => {
    const id = prefabIdFromPath(it.prefabAssetPath) || it.instanceId;
    return isEnvAnimDecorItem(id, it.displayName);
  });
}
