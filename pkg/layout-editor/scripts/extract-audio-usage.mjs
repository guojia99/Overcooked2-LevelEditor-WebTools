#!/usr/bin/env node
/**
 * Scan Assets/Scripts/Assembly-CSharp for audio tag references and produce
 * item-audio rules (itemAudioRules) for audio-knowledge.json.
 *
 * Usage:  node scripts/extract-audio-usage.mjs [--write]
 *   --write    Overwrite audio-knowledge.json in-place.
 *   (default)  Write rules to stdout.
 *
 * Extraction pipeline:
 *   class file → game object → audio tags → audio directories / ambiences
 *
 * ① Regex-match Game(OneShot|Looping)AudioTag.XXX in every .cs file.
 * ② Map class name → catalog item name via heuristics + alias table.
 * ③ Map tag prefix → theme key (with dir + ambiences) or explicit directory id.
 */

import { readFileSync, writeFileSync } from "fs";
import { join, resolve, dirname } from "path";
import { fileURLToPath } from "url";
import { execSync } from "child_process";

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(__dirname, "../..");
const SRC_DIR = join(ROOT, "Assets", "Scripts", "Assembly-CSharp");
const KNOWLEDGE_PATH = join(__dirname, "data", "audio-knowledge.json");

// ---- ③ Tag‑prefix → (theme key | explicit directories) ----
const TAG_DIR_MAP = {
  DLC_02: { theme: "throne" },
  DLC_03: { directories: ["DLC03AudioDirectorySO"] },
  DLC_04: { directories: ["DLC04AudioDirectory"] },
  DLC_05: { theme: "camping" },
  DLC_06: { directories: ["DLC06AudioDirectory"] },
  DLC_07: { theme: "dlc07_horde" },
  DLC_08: { theme: "circus" },
  DLC_09: { theme: "dlc09_wonderland" },
  DLC_10: { directories: ["DLC10AudioDirectory"] },
  DLC_11: { directories: ["DLC11AudioDirectorySO"] },
  DLC_13: { directories: ["DLC13AudioDirectory"] },
  Flames: { directories: ["FlamethrowerAudioDirectorySO"] },
  Flamethrower: { directories: ["FlamethrowerAudioDirectorySO"] },
  FireIgnition: { directories: ["FlamethrowerAudioDirectorySO"] },
  FireProjectiles: { directories: ["FlamethrowerAudioDirectorySO"] },
  VanEngine: { directories: ["VanAudioDirectory"] },
  VanHorn: { directories: ["VanAudioDirectory"] },
  VanHorn02: { directories: ["VanAudioDirectory"] },
  VanHorn03: { directories: ["VanAudioDirectory"] },
  VanHorn04: { directories: ["VanAudioDirectory"] },
  VanRev: { directories: ["VanAudioDirectory"] },
  WorldMapBoatEngine: { directories: ["BoatEngineAudioDirectory"] },
  WorldMapBoatRev: { directories: ["BoatEngineAudioDirectory"] },
  WorldMapPlaneEngine: { directories: ["BalloonAudioDirectory"] },
  WorldMapPlaneRev: { directories: ["BalloonAudioDirectory"] },
  WorldMapRamp: { theme: "throne" },       // world-map audio, probably don't need a rule
  WorldMapTiles: { theme: "throne" },       // world-map audio, probably don't need a rule
  WorldMapRampButton: { directories: ["UIAudioDirectorySO"] },
  DLC_07_Mist: { theme: "dlc07_horde" },
  DLC_07_Failed: { theme: "dlc07_horde" },
  DLC_07_Success: { theme: "dlc07_horde" },
  DLC_07_Nombie_Despawn: { theme: "dlc07_horde" },
  DLC_07_Nombie_Spawn: { theme: "dlc07_horde" },
  DLC_07_Wave_Incoming: { theme: "dlc07_horde" },
  DLC_08_Cannon_Crowd: { theme: "circus" },
  DLC_08_Cannon_Enter: { theme: "circus" },
  DLC_08_Cannon_Fire: { theme: "circus" },
  DLC_08_Cannon_Fuse: { theme: "circus" },
  DLC_08_Fuse_Death: { theme: "circus" },
  DLC_08_Fuse_Ignite: { theme: "circus" },
  DLC_08_Drinks_Machine_Dispense: { theme: "circus" },
  DLC_08_Sauce_Machine_Dispense: { theme: "circus" },
};

// ---- ② Class‑name → catalog item aliases ----
const CLASS_ITEM_MAP = {
  BarbequeCosmeticDecisions: "Barbeque",
  CampfireCosmeticDecisions: "Campfire",
  FurnaceCosmeticDecisions: "Furnace",
  FurnaceOvenCosmeticDecisions: "FurnaceOven",
  CannonCosmeticDecisions: "Cannon",
  BellowsCosmeticDecisions: null,              // Bellows is a campfire tool, not a standalone item
  BackpackCosmeticDecisions: null,             // Backpack is held, not a placed item
  WokEffectsCosmeticDecisions: "Wok",
  CoalScuttleCosmeticDecisions: null,          // coal scuttle bucket — no catalog item
  CookingRegion: null,                         // visual region for Wok (DLC04), same as Wok?
  Flammable: null,                             // fire visual component, not an item
  DrinksMachineCosmeticDecisions: "DrinksMachine",
  CondimentDispenserCosmeticDecisions: "CondimentDispenser",
  TerminalCosmeticDecisions: "MultiControlTerminal",
  Teleportal: "Teleportal",
  SwitchCosmeticDecisions: "Switch",
  Splattable: null,
  FlamethrowerSpray: "Flamethrower",
  ProjectileSpawner: "Burner",
  PlayerControlsImpl_Default: null,            // shared player audio → mandatory dirs
  PlayerRespawnBehaviour: null,
  PlayerSlipBehaviour: null,
  KitchenFlowControllerBase: null,             // shared game flow audio → mandatory dirs
  OrderControllerBase: null,
  PlateReturnStation: null,
  PlateStation: null,
  WashingStation: null,
  CookingUIController: null,
  HordeEnemyCosmeticDecisions: null,           // enemy not a catalog item
  HordeFlowController: null,                   // flow controller, not an item
  HordeLockableCosmeticDecisions: null,
  HordeTargetUIController: null,
  HordeOutroFlowroutine: null,
  UtensilRespawnBehaviour: null,
  TutorialPopupController: null,
  LevelIntroFlowroutine: null,
  LevelTimerUIController: null,
  ScoreScreenOutroFlowroutine: null,
  CompetitiveScoreScreenFlowroutine: null,
  CutsceneOutroFlowroutineBase: null,
  SurvivalModeOutroFlowroutine: null,
  MapAvatarCosmeticDecisions: null,
  MapAvatarControls: null,
  MapNode: null,
  LobbyFlowController: null,
  ScreenTransitionManager: null,
  FrontendChefCustomisation: null,
  FrontendPlayerLobby: null,
  FrontendSwitchFriends: null,
  AwardAvatarUIController: null,
  ChefCustomiser: null,
  EmoteSelector: null,
  LobbyUIController: null,
  PlayerSelectCardUIController: null,
  SelectorOption: null,
  SliderOption: null,
  T17Button: null,
  T17Toggle: null,
  ToggleOption: null,
  DialogueController: null,
  StartScreenFlow: null,
  ServerCookableContainer: null,               // ingredient addition audio → mandatory
  ServerMixableContainer: null,                // ingredient addition audio → mandatory
  ServerContentsDisposalBehaviour: null,        // trash → mandatory
  ServerIngredientDisposalBehaviour: null,      // trash → mandatory
  ServerFlamethrowerSpray: "Flamethrower",
  ServerFurnaceOvenCosmeticDecisions: null,     // backing field, mirrors client
  ServerHordeEnemyCosmeticDecisions: null,
  FlamethrowerSpray: "Flamethrower",
  SprayingUtensil: "FireExtinguisher",
  WindVolume: null,
  TermCosmeticDecisions: null,
  PlayerDive: null,                            // map avatar, not an item
  PlayerFall: null,
};

/** Strip Client/Server prefix, map via table or guess. */
function classToItem(filePath) {
  const fileName = filePath.replace(/^.*[\\/]/, ""); // basename
  const base = fileName.replace(/\.cs$/, "");
  const stripped = base.replace(/^(Client|Server)/, "");
  if (CLASS_ITEM_MAP[stripped] !== undefined) return CLASS_ITEM_MAP[stripped];
  if (CLASS_ITEM_MAP[base] !== undefined) return CLASS_ITEM_MAP[base];
  // Auto‑guess: remove Client/Server + CosmeticDecisions/Spray/Behaviour/Effects
  const guess = stripped
    .replace(/CosmeticDecisions$/, "")
    .replace(/Spray$/, "")
    .replace(/Behaviour$/, "")
    .replace(/Effects$/, "")
    .replace(/Cosmetic$/, "");
  if (!guess || guess === stripped) return null;
  return guess; // tentative
}

// ---- run ripgrep ----
let raw;
try {
  raw = execSync(
    `rg -noH "Game(?:OneShot|Looping)AudioTag\\.\\w+" --glob '*.cs' --color never .`,
    { cwd: SRC_DIR, maxBuffer: 2 * 1024 * 1024, encoding: "utf8" }
  );
} catch (e) {
  if (e.status === 1) raw = ""; // no matches
  else throw e;
}

const lines = raw.trim().split("\n").filter(Boolean);

// file → Set<tag>
const fileTags = new Map();
for (const line of lines) {
  const colonIdx = line.indexOf(":");
  if (colonIdx < 0) continue;
  const secondColon = line.indexOf(":", colonIdx + 1);
  if (secondColon < 0) continue;
  const fileName = line.substring(0, colonIdx);
  const matched = line.substring(secondColon + 1);
  const tag = matched.substring(matched.lastIndexOf(".") + 1);
  if (!tag || tag === "COUNT" || tag === "None") continue;
  if (!fileTags.has(fileName)) fileTags.set(fileName, new Set());
  fileTags.get(fileName).add(tag);
}

// Build rules: item → { tags, dirs, theme }
const itemRules = new Map(); // item → { tags: Set, dirs: Set, theme: string | null }

for (const [file, tags] of fileTags) {
  const item = classToItem(file);
  if (!item) continue;
  if (!itemRules.has(item)) itemRules.set(item, { tags: new Set(), dirs: new Set(), theme: null });
  const entry = itemRules.get(item);
  for (const tag of tags) {
    entry.tags.add(tag);
    const rule = resolveTagRule(tag);
    if (!rule) continue;
    if (rule.theme) {
      entry.theme = rule.theme; // last one wins; theme-based items typically have one theme
    }
    if (rule.directories) {
      for (const d of rule.directories) entry.dirs.add(d);
    }
  }
}

function resolveTagRule(tag) {
  if (TAG_DIR_MAP[tag]) return TAG_DIR_MAP[tag];
  for (const prefix of Object.keys(TAG_DIR_MAP).sort((a, b) => b.length - a.length)) {
    if (tag.startsWith(prefix + "_")) return TAG_DIR_MAP[prefix];
    if (tag.startsWith(prefix)) return TAG_DIR_MAP[prefix];
  }
  return null;
}

/** Pick label zh from item name. */
function itemLabelZh(item) {
  const LABELS = {
    Campfire: "篝火（DLC5 露营）",
    Barbeque: "烧烤架（DLC2 沙滩）",
    Furnace: "熔炉（DLC7 部落）",
    FurnaceOven: "熔炉烤箱（DLC7 部落）",
    Cannon: "大炮（DLC8 马戏团）",
    Wok: "炒锅（DLC4 新年）",
    Burner: "燃烧弹射器（DLC4 新年）",
    Flamethrower: "喷火器",
    FireExtinguisher: "灭火器",
    DrinksMachine: "饮料机（DLC8 马戏团）",
    CondimentDispenser: "酱料机（DLC8 马戏团）",
    Teleportal: "传送门",
    Switch: "开关",
    MultiControlTerminal: "控制终端",
  };
  return LABELS[item] || item;
}

// Build output rules — group by theme for theme-based, then explicit dirs
const output = [];
const handled = new Set();

// ---- Manual overrides — items whose audio comes from nested components (e.g. Flammable) ----
const MANUAL_RULES = [
  { items: ["Flamethrower"], directories: ["FlamethrowerAudioDirectorySO"], labelZh: "喷火器" },
  { items: ["FireExtinguisher"], directories: ["FlamethrowerAudioDirectorySO"], labelZh: "灭火器" },
];

// Theme-based first
for (const [item, entry] of itemRules) {
  if (!entry.theme || handled.has(item)) continue;
  output.push({
    items: [item],
    theme: entry.theme,
    labelZh: itemLabelZh(item),
  });
  handled.add(item);
}

// Same theme → merge items
for (let i = 0; i < output.length; i++) {
  for (let j = i + 1; j < output.length; j++) {
    if (output[i].theme && output[j].theme && output[i].theme === output[j].theme) {
      output[i].items.push(...output[j].items);
      output[j].items = [];
    }
  }
}
const finalOutput = output.filter((r) => r.items.length > 0);

// Manual overrides (not auto-detected from code — e.g. Flammable component provides fire audio)
for (const mr of MANUAL_RULES) {
  finalOutput.push({ ...mr });
}

// Directory-only rules
for (const [item, entry] of itemRules) {
  if (handled.has(item)) continue;
  if (entry.dirs.size === 0) continue;
  handled.add(item);
  finalOutput.push({
    items: [item],
    directories: [...entry.dirs],
    labelZh: itemLabelZh(item),
  });
}

// Merge identical directory rules
for (let i = 0; i < finalOutput.length; i++) {
  if (finalOutput[i].directories) {
    const dKey = finalOutput[i].directories.join(",");
    for (let j = i + 1; j < finalOutput.length; j++) {
      if (finalOutput[j].directories && finalOutput[j].directories.join(",") === dKey) {
        finalOutput[i].items.push(...finalOutput[j].items);
        finalOutput[j].items = [];
      }
    }
  }
}

// Remove empty entries
const clean = finalOutput.filter((r) => r.items.length > 0);

// For items without audio dirs/theme, skip them
const valid = clean.filter((r) => r.theme || (r.directories && r.directories.length > 0));

// Deduplicate directories in rules
for (const r of valid) {
  if (r.directories) r.directories = [...new Set(r.directories)];
  if (r.ambiences) r.ambiences = [...new Set(r.ambiences)];
}

const outputObj = {
  _meta: {
    generatedAt: new Date().toISOString(),
    comment: "Auto‑generated from Assembly-CSharp source code. Edit TAG_DIR_MAP / CLASS_ITEM_MAP in extract-audio-usage.mjs to adjust.",
    totalItems: valid.reduce((s, r) => s + r.items.length, 0),
    tagsScanned: fileTags.size,
  },
  itemAudioRules: valid,
};

if (process.argv.includes("--write")) {
  const knowledge = JSON.parse(readFileSync(KNOWLEDGE_PATH, "utf8"));
  knowledge.itemAudioRules = valid;
  writeFileSync(KNOWLEDGE_PATH, JSON.stringify(knowledge, null, 2) + "\n");
  console.error(`✓ Wrote ${valid.length} itemAudioRules to audio-knowledge.json`);
} else {
  // Pretty-print with comment
  const rulesJson = JSON.stringify(valid, null, 2);
  console.log(rulesJson);
}

// Debug: print unhandled tags
if (process.argv.includes("--debug")) {
  const allFileTags = new Set();
  for (const [file, tags] of fileTags) {
    for (const t of tags) allFileTags.add(t);
  }
  const unhandled = [];
  for (const t of allFileTags) {
    if (!resolveTagRule(t)) unhandled.push(t);
  }
  if (unhandled.length) {
    console.error(`\n⚠ ${unhandled.length} un-mapped audio tags:`);
    unhandled.sort().forEach((t) => console.error(`  ${t}`));
  }
}
