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
  desc: string;
  evidence: string;
}

export const NPC_ANIM_TYPES: NpcAnimType[] = [
  {
    key: "waiter",
    labelZh: "服务生",
    desc: "自带行走循环动画 Waiter_Walk（0.625s 循环）+ 待机 Waiter_Idle",
    evidence: "Waiter_Walk / Waiter_Idle (sushi_1_2)",
  },
  {
    key: "moon",
    labelZh: "月亮 NPC",
    desc: "月亮沿路径移动动画（16~47s 循环路线）",
    evidence: "Moon_NPC_path / MoonNPC_Path_01",
  },
  {
    key: "walkingbread",
    labelZh: "面包人",
    desc: "自带行走动画 + 路径标记",
    evidence: "walkingbread_*_walking",
  },
  {
    key: "liferaft",
    labelZh: "救生筏 NPC",
    desc: "救生筏循环动画（4s）+ 旋转动画（15s）",
    evidence: "NPC_Liferaft_01 / NPC_Liferaft_Rotate",
  },
  {
    key: "pedestrian",
    labelZh: "行人",
    desc: "长路线行人动画（186s）；城市关 NPC 循环路线（37~67s）",
    evidence: "1_2_Pedestrians_01 / NPC_Walk_*",
  },
];

export function isNpcAnimItem(id: string, nameZh: string): boolean {
  const s = `${id} ${nameZh}`;
  return (
    /_Waiter_/.test(id) ||
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
