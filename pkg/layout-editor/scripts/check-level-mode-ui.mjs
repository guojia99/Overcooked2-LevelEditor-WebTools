// Read-only focused UI checks. Run: node layout-editor/scripts/check-level-mode-ui.mjs
import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
import { test } from "node:test";
import ts from "../web/node_modules/typescript/lib/typescript.js";

const sources = new Map();
function source(file) {
  if (!sources.has(file)) {
    const text = fs.readFileSync(new URL(`../web/src/${file}`, import.meta.url), "utf8");
    sources.set(file, ts.createSourceFile(file, text, ts.ScriptTarget.Latest, true));
  }
  return sources.get(file);
}

function declarations(file, names) {
  const tree = source(file);
  return names.map(name => {
    const node = tree.statements.find(node =>
      (ts.isFunctionDeclaration(node) && node.name?.text === name) ||
      (ts.isVariableStatement(node) && node.declarationList.declarations.some(d => d.name.getText(tree) === name)));
    assert(node, `${file}: missing declaration ${name}`);
    return node.getText(tree);
  }).join("\n");
}

function execute(text, globals = {}) {
  const context = { exports: {}, ...globals };
  const result = ts.transpileModule(text, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
    reportDiagnostics: true,
  });
  assert.equal(result.diagnostics?.filter(d => d.category === ts.DiagnosticCategory.Error).length, 0);
  vm.runInNewContext(result.outputText, context, { timeout: 2000 });
  return context;
}

// Extract the actual registration statement rather than duplicating its callback.
function configRegistration() {
  const tree = source("editor/init.ts");
  const found = [];
  function visit(node) {
    if (ts.isExpressionStatement(node) && ts.isCallExpression(node.expression)) {
      const call = node.expression;
      let target = ts.isPropertyAccessExpression(call.expression) && call.expression.expression;
      if (target && ts.isNonNullExpression(target)) target = target.expression;
      if (ts.isPropertyAccessExpression(call.expression) && call.expression.name.text === "addEventListener" &&
          target && ts.isCallExpression(target) && ts.isPropertyAccessExpression(target.expression) &&
          target.expression.name.text === "getElementById" && target.arguments[0]?.text === "btn-level-config") found.push(node);
    }
    ts.forEachChild(node, visit);
  }
  visit(tree);
  assert.equal(found.length, 1, "editor/init.ts: unique config button registration");
  return found[0].getText(tree);
}

const decode = text => text.replace(/&quot;/g, '"').replace(/&gt;/g, ">").replace(/&lt;/g, "<").replace(/&amp;/g, "&");

// Only the HTML, selectors and events exercised below are modeled. Unknown selector
// syntax fails loudly, and innerHTML replacement detaches the old descendants.
class Element {
  constructor(tagName = "div") {
    this.tagName = tagName.toLowerCase();
    this.attributes = new Map();
    this.children = [];
    this.parentElement = null;
    this.style = {};
    this.listeners = new Map();
    this.value = "";
    this.checked = false;
    this.disabled = false;
    this.hidden = false;
    this.textContent = "";
    this.classes = new Set();
    this.classList = {
      add: (...names) => names.forEach(name => this.classes.add(name)),
      contains: name => this.classes.has(name),
      toggle: (name, force = !this.classes.has(name)) => {
        if (force) this.classes.add(name); else this.classes.delete(name);
        return force;
      },
    };
  }
  get dataset() {
    return Object.fromEntries([...this.attributes].filter(([key]) => key.startsWith("data-"))
      .map(([key, value]) => [key.slice(5).replace(/-([a-z])/g, (_, c) => c.toUpperCase()), value]));
  }
  get type() { return this.attributes.get("type") ?? "text"; }
  get defaultValue() { return this.attributes.get("value") ?? ""; }
  get defaultChecked() { return this.attributes.has("checked"); }
  get defaultSelected() { return this.attributes.has("selected"); }
  get options() { return this.querySelectorAll("option"); }
  get valueAsNumber() { return this.value === "" ? NaN : Number(this.value); }
  get isConnected() { return this.tagName === "document" || Boolean(this.parentElement?.isConnected); }
  setAttribute(key, value) {
    this.attributes.set(key, value);
    if (key === "class") this.classes = new Set(value.split(/\s+/));
    if (key === "value") this.value = value;
    if (key === "checked") this.checked = true;
    if (key === "disabled") this.disabled = true;
    if (key === "style") for (const entry of value.split(";")) {
      const [name, val] = entry.split(":");
      if (name?.trim()) this.style[name.trim()] = val?.trim();
    }
  }
  getAttribute(key) { return this.attributes.get(key) ?? null; }
  appendChild(child) { this.children.push(child); child.parentElement = this; return child; }
  contains(child) { return child === this || this.children.some(node => node.contains(child)); }
  set innerHTML(html) {
    this.children.forEach(child => child.parentElement = null);
    this.children = [];
    this.insertAdjacentHTML("beforeend", html);
  }
  insertAdjacentHTML(position, html) {
    assert.equal(position, "beforeend");
    const stack = [this];
    const voidTags = new Set(["input", "img", "br", "hr", "meta", "link", "source", "wbr"]);
    for (const token of html.matchAll(/<!--[\s\S]*?-->|<\/?[a-zA-Z][^>]*>|[^<]+/g)) {
      const text = token[0];
      if (text.startsWith("<!--")) continue;
      if (text.startsWith("</")) {
        const name = text.slice(2, -1).trim().toLowerCase();
        assert.equal(stack.pop().tagName, name, `unbalanced mock HTML: ${text}`);
      } else if (text.startsWith("<")) {
        const [, name, attrs] = /^<([\w-]+)([\s\S]*?)\/?\s*>$/.exec(text);
        const node = name.toLowerCase() === "input" ? new Input() : name.toLowerCase() === "select" ? new Select() : new Element(name);
        for (const attr of attrs.matchAll(/([^\s=]+)(?:\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s]+)))?/g)) {
          node.setAttribute(attr[1], decode(attr[2] ?? attr[3] ?? attr[4] ?? ""));
        }
        stack.at(-1).appendChild(node);
        if (!voidTags.has(node.tagName) && !text.endsWith("/>")) stack.push(node);
      } else stack.at(-1).textContent += decode(text);
    }
    assert.equal(stack.length, 1, "unclosed mock HTML");
    for (const select of this.querySelectorAll("select")) {
      const options = select.querySelectorAll("option");
      select.value = (options.find(option => option.attributes.has("selected")) ?? options[0])?.value ?? "";
    }
  }
  matches(selector) {
    return selector.split(",").some(part => {
      let rest = part.trim();
      let matches = true;
      rest = rest.replace(/^[a-z][\w-]*/i, tag => { matches &&= this.tagName === tag.toLowerCase(); return ""; });
      rest = rest.replace(/\[([\w-]+)(?:="([^"]*)")?\]/g, (_, key, value) => {
        matches &&= this.attributes.has(key) && (value === undefined || this.attributes.get(key) === value);
        return "";
      });
      rest = rest.replace(/([.#])([\w-]+)/g, (_, prefix, value) => {
        matches &&= prefix === "." ? this.classes.has(value) : this.attributes.get("id") === value;
        return "";
      });
      rest = rest.replace(/:checked/g, () => { matches &&= this.checked; return ""; });
      assert.equal(rest, "", `unsupported mock selector: ${selector}`);
      return matches;
    });
  }
  closest(selector) { return this.matches(selector) ? this : this.parentElement?.closest(selector) ?? null; }
  querySelectorAll(selector) {
    return this.children.flatMap(child => [...(child.matches(selector) ? [child] : []), ...child.querySelectorAll(selector)]);
  }
  querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
  getElementById(id) { return this.querySelector(`#${id}`); }
  createElement(tag) { return new Element(tag); }
  addEventListener(type, callback) {
    if (!this.listeners.has(type)) this.listeners.set(type, []);
    this.listeners.get(type).push(callback);
  }
  async dispatch(type, target = this) {
    const event = { target, preventDefault() {} };
    for (let node = this; node; node = node.parentElement) {
      for (const callback of node.listeners.get(type) ?? []) await callback(event);
    }
    // Some production listeners deliberately discard async IIFEs via `void`.
    await new Promise(resolve => setImmediate(resolve));
  }
  async click() { if (!this.disabled) await this.dispatch("click"); }
  reportValidity() { return true; }
}
class Input extends Element { constructor() { super("input"); } }
class Select extends Element { constructor() { super("select"); } }

const domGlobals = { Element, HTMLInputElement: Input, HTMLSelectElement: Select };
const plain = value => JSON.parse(JSON.stringify(value));
const noop = () => {};
const button = (label, _variant, attrs = {}) => `<button type="button" ${Object.entries(attrs).map(([key, value]) => `${key}="${value}"`).join(" ")}>${label}</button>`;
const detailFixture = (gameMode = "Campaign") => ({
  levelName: "UI fixture", levelNameZH: "UI fixture", gameMode,
  levelInfoAssetPath: "Assets/LevelSets/test/data/level/LevelInfo.asset",
  sceneAssetPath: "Assets/LevelSets/test/scenes/s_test.unity",
  minOrderCount: 2, maxOrderCount: 5, dependencies: [],
  configs: Array.from({ length: 4 }, () => ({ exists: true, roundTime: 240, targetRepairCostMax: 100 })),
});

function modalHarness({ gameMode = "Campaign", sceneConfig, initialTab, realMode = false, modeApi = {}, saveApi = {} } = {}) {
  const document = new Element("document");
  const events = [];
  const mounts = [];
  const writes = [];
  let confirms = 0;
  let closeGuard = () => true;
  let savedDetail = detailFixture(gameMode);
  let fetchedConfig = {
    gameMode: "Dynamic", assetPath: detailFixture().levelInfoAssetPath,
    minOrderCount: 2, maxOrderCount: 5, targetHealth: 100,
    targetRepairSpeed: 1, targetRepairThreshold: 1, kitchenHealth: 100,
    phases: [1, 2].map(i => ({ id: `phase_${i}`, durationSeconds: 240, recipeGuids: ["recipe"] })),
    hordeConfigs: [], enemies: [],
  };
  const mode = execute(declarations("levelModeEditor.ts", ["esc", "mountLevelModeEditor"]), {
    ...domGlobals, mBtnHtml: button,
    confirm: () => { confirms++; return true; },
    changeLevelMode: async (...args) => { events.push(["switch", ...args]); },
    fetchLevelModeConfig: async () => plain(fetchedConfig),
    fetchRecipeCatalog: async () => [{ guid: "recipe", id: "recipe" }],
    updateLevelModeConfig: async config => { fetchedConfig = plain(config); writes.push(["mode", plain(config)]); },
    ...modeApi,
  }).exports.mountLevelModeEditor;
  const globals = {
    ...domGlobals, document,
    mBtnHtml: button,
    mCancelBtnHtml: () => button("Cancel", "default", { "data-cancel": "" }),
    mPrimaryBtnHtml: label => button(label, "primary", { "data-ok": "" }),
    openModal: (_title, html, footer, options) => {
      closeGuard = options?.beforeClose ?? (() => true);
      document.innerHTML = `<div class="modal-panel"><div class="modal-body">${html}</div><div class="modal-footer">${footer}</div></div>`;
    },
    closeModal: () => { if (closeGuard()) events.push("close"); },
    mountLevelModeEditor: (...args) => {
      const pending = realMode ? mode(...args) : Promise.resolve();
      mounts.push({ args, pending });
      return pending;
    },
    mountNativeScriptedRecipes: async () => {},
    ceilingEnvPaneHtml: () => '<input id="cfg-env-fixture" value="2"><input type="checkbox" id="cfg-env-check">',
    partLevelTabPaneHtml: () => '<input id="cfg-part-fixture" value="3">',
    screenshotPaneHtml: () => '<input id="cfg-shot-fixture" value="original">',
    wireScreenshotPane: noop,
    themeSignalsForScene: async () => ({}),
    wireAudioEditor: async inner => {
      inner.innerHTML = '<select id="cfg-audio-fixture"><option value="original">Original</option><option value="edited">Edited</option></select>';
      return { save: async close => events.push(["audio-save", close]) };
    },
    showBusy: noop, hideBusy: noop,
    setStatus: message => events.push(["status", message]),
    api: {
      updateLevelInfo: async value => { writes.push(["info", plain(value)]); Object.assign(savedDetail, value); },
      updateLevelConfig: async value => {
        writes.push(["config", plain(value)]);
        savedDetail.configs = [value.config_1p, value.config_2p, value.config_3p, value.config_4p].map(plain);
      },
      fetchLevelDetail: async () => plain(savedDetail),
      ...saveApi,
    },
  };
  const context = execute([
    declarations("levels.ts", ["STAR_FIELDS", "RHYTHM_FIELDS", "PLAYER_TABS", "PLAYER_ROW_LABELS"]),
    declarations("autoScore.ts", ["RATIO_MIN", "RATIO_MAX", "RATIO_STEP", "round5"]),
    declarations("levels.ts", ["openConfigTabsModal"]),
  ].join("\n"), globals);
  const detail = detailFixture(gameMode);
  return {
    document, events, mounts, writes, detail,
    get confirms() { return confirms; },
    tryClose: () => globals.closeModal(),
    async open() {
      await context.exports.openConfigTabsModal(detail, "test", () => events.push("saved"), sceneConfig, initialTab);
      await Promise.all(mounts.map(mount => mount.pending));
      await new Promise(resolve => setImmediate(resolve));
    },
    mode,
  };
}

function required(document, selector) {
  const element = document.querySelector(selector);
  assert(element, `missing DOM element ${selector}`);
  return element;
}
async function tab(h, name) {
  await required(h.document, `.cfg-ltab-btn[data-ltab="${name}"]`).click();
  assert.equal(required(h.document, ".cfg-ltab-btn.active").dataset.ltab, name);
  for (const pane of h.document.querySelectorAll("[data-lpane]")) {
    assert.equal(pane.style.display, pane.dataset.lpane === name ? "" : "none");
  }
}

test("actual config modal mounts mode editor and routes tabs/footer saves", async () => {
  let layoutDirty = false;
  const sceneConfig = { partLevel: { canEdit: true }, isLayoutDirty: () => layoutDirty };
  const h = modalHarness({ sceneConfig });
  await h.open();
  assert.equal(h.mounts.length, 1);
  const [host, set, asset, detail, refresh, options] = h.mounts[0].args;
  assert.equal(host, required(h.document, "#cfg-mode-pane"));
  assert.equal(host.parentElement.dataset.lpane, "mode");
  assert.equal(set, "test");
  assert.equal(asset, h.detail.levelInfoAssetPath);
  assert.equal(detail, h.detail);
  assert.equal(typeof refresh, "function");
  assert.equal(options.isLayoutDirty(), false);
  layoutDirty = true;
  assert.equal(options.isLayoutDirty(), true, "mode callback reads current canvas dirty state");
  layoutDirty = false;
  const ok = required(h.document, "[data-ok]");
  assert.equal(required(h.document, ".cfg-ltab-btn.active").dataset.ltab, "score");
  assert.equal(ok.style.display, "");
  for (const name of ["mode", "score", "env", "part", "shot", "audio", "mode"]) {
    await tab(h, name);
    assert.equal(ok.style.display, ["mode", "shot"].includes(name) ? "none" : "");
    if (name === "mode") {
      const before = [h.events.length, h.writes.length];
      await ok.click(); // Programmatic click must also be guarded while hidden.
      assert.deepEqual([h.events.length, h.writes.length], before);
    }
    if (name === "audio") {
      await ok.click();
      assert.deepEqual(h.events.at(-1), ["audio-save", false]);
      assert.equal(h.writes.length, 0);
    }
  }
  await tab(h, "score");
  required(h.document, "#cfg-minOrderCount").value = "3";
  await ok.click();
  assert.deepEqual(h.writes.map(([kind]) => kind), ["config", "info"]);
  assert.equal(h.writes[1][1].minOrderCount, 3);
  assert.deepEqual(Object.keys(h.writes[0][1]), ["assetPath", "config_1p", "config_2p", "config_3p", "config_4p"]);
  assert(h.events.includes("close"));
  assert.equal(h.events.at(-1), "saved");
});

test("scripted dirty/busy guards score save, cancel, modal close and mode dirty callback", async () => {
  const h = modalHarness(); await h.open();
  const pane = required(h.document, "#cfg-scripted-pane");
  pane.setAttribute("data-dirty", "1");
  assert.equal(h.mounts[0].args[5].isLayoutDirty(), true);
  await required(h.document, "[data-ok]").click();
  await required(h.document, "[data-cancel]").click();
  h.tryClose();
  assert.equal(h.writes.length, 0); assert(!h.events.includes("close"));
  assert.match(h.events.at(-1)[1], /未保存草稿/);
  pane.setAttribute("data-dirty", "0");pane.setAttribute("data-saving", "1");
  h.tryClose(); assert(!h.events.includes("close"));
  assert.equal(h.mounts[0].args[5].isLayoutDirty(), true);
  pane.setAttribute("data-saving", "0");h.tryClose();assert(h.events.includes("close"));
});

test("config rejection never posts info; info failure reports partial state and reads back without closing", async () => {
  for (const failed of ["config", "info"]) {
    const calls = []; let reads = 0;
    const h = modalHarness({ saveApi: {
      updateLevelConfig: async () => { calls.push("config"); if(failed === "config") throw new Error("bad order_seq"); },
      updateLevelInfo: async () => { calls.push("info"); throw new Error("grid unavailable"); },
      fetchLevelDetail: async () => { reads++; return detailFixture(); },
    } });
    await h.open();await required(h.document, "[data-ok]").click();
    assert.deepEqual(calls, failed === "config" ? ["config"] : ["config", "info"]);
    assert.equal(reads, failed === "config" ? 1 : 2);assert(!h.events.includes("close"));
    assert.match(h.events.at(-1)[1], failed === "config" ? /基础配置尚未提交/ : /人数配置已保存/);
    assert.match(h.events.at(-1)[1], /当前读回/);
  }
});

test("initialTab mode activates actual click handler; native callback awaits reload before onSaved", async () => {
  const order = [];
  let finishReload;
  const reload = new Promise(resolve => { finishReload = resolve; });
  const h = modalHarness({ initialTab: "mode", sceneConfig: { onNativeModeChanged: async () => {
    order.push("reload-start"); await reload; order.push("reload-done");
  } } });
  await h.open();
  assert.equal(required(h.document, ".cfg-ltab-btn.active").dataset.ltab, "mode");
  assert.equal(required(h.document, '[data-lpane="mode"]').style.display, "");
  assert.equal(required(h.document, "[data-ok]").style.display, "none");
  const pending = h.mounts[0].args[4]();
  assert.deepEqual(h.events, ["close"]);
  assert.deepEqual(order, ["reload-start"]);
  finishReload();
  await pending;
  assert.deepEqual(order, ["reload-start", "reload-done"]);
  assert.deepEqual(h.events, ["close", "saved"]);
});

test("manage btn-level-mode invokes shared modal directly with initialTab mode", async () => {
  const document = new Element("document");
  document.innerHTML = '<button id="btn-level-mode"></button><button id="btn-level-config"></button>';
  const calls = [];
  const renders = [];
  const app = {};
  const detail = detailFixture();
  const context = execute(`${declarations("levels.ts", ["wireDetailActions"])}\nexports.wireDetailActions = wireDetailActions;`, {
    document, wireSummaryBgPane: noop,
    openConfigTabsModal: (...args) => { calls.push(args); return Promise.resolve(); },
    renderLevelDetail: (...args) => renders.push(args),
    api: { fetchLayout: () => assert.fail("mode shortcut must not fetch layout first") },
  });
  context.exports.wireDetailActions(app, "test", "level.asset", detail);
  await required(document, "#btn-level-mode").click();
  assert.equal(calls.length, 1);
  assert.equal(calls[0][0], detail);
  assert.equal(calls[0][1], "test");
  assert.equal(calls[0][3], undefined);
  assert.equal(calls[0][4], "mode");
  calls[0][2]();
  assert.deepEqual(renders, [[app, "test", "level.asset"]]);
});

test("editor/init registration forwards live S.dirty and reloads current scene", async () => {
  const document = new Element("document");
  document.innerHTML = '<button id="btn-level-config"></button>';
  const S = { dirty: false, scenePath: "scene-a", currentLevelSet: "test" };
  const calls = [];
  const reloads = [];
  const detail = detailFixture();
  execute(configRegistration(), {
    document, S, withLevelDetail: async callback => callback(detail),
    openConfigTabsModal: (...args) => { calls.push(args); return Promise.resolve(); },
    loadScene: async scene => reloads.push(scene),
    partLevelActive: () => false, layoutThemeSignals: noop,
  });
  await required(document, "#btn-level-config").click();
  assert.equal(calls.length, 1);
  assert.equal(calls[0][0], detail);
  assert.equal(calls[0][1], "test");
  const options = calls[0][3];
  assert.equal(options.isLayoutDirty(), false);
  S.dirty = true;
  assert.equal(options.isLayoutDirty(), true);
  S.dirty = false;
  assert.equal(options.isLayoutDirty(), false);
  S.scenePath = "scene-b";
  await options.onNativeModeChanged();
  S.scenePath = null;
  await options.onNativeModeChanged();
  assert.deepEqual(reloads, ["scene-b", ""]);
});

test("nested mode editor blocks switching for sibling tab inputs, checkboxes and live canvas dirty", async () => {
  const S = { dirty: false };
  const h = modalHarness({ realMode: true, initialTab: "mode", sceneConfig: {
    isLayoutDirty: () => S.dirty, partLevel: { canEdit: true },
  } });
  await h.open();
  const host = required(h.document, "#cfg-mode-pane");
  assert.notEqual(host.parentElement, host.closest(".modal-body"), "fixture must expose parentElement-only regression");
  const target = required(host, "[data-mode-target]");
  const switchButton = required(host, "[data-mode-switch]");
  assert.equal(switchButton.disabled, true);
  target.value = "Dynamic";
  await target.dispatch("change");
  assert.equal(switchButton.disabled, false);
  for (const selector of ["#cfg-minOrderCount", "#cfg-1p-oneStarScore", "#cfg-env-fixture", "#cfg-part-fixture", "#cfg-env-check"]) {
    const input = required(h.document, selector);
    const original = input.type === "checkbox" ? input.checked : input.value;
    if (input.type === "checkbox") input.checked = !original; else input.value = `${original}1`;
    await switchButton.click();
    assert.equal(h.confirms, 0, `dirty ${selector} must block before confirmation`);
    assert.equal(h.events.length, 0, `dirty ${selector} must not reach API/reload`);
    assert.equal(required(host, "[data-switch-status]").getAttribute("role"), "alert");
    if (input.type === "checkbox") input.checked = original; else input.value = original;
  }
  S.dirty = true;
  await switchButton.click();
  assert.equal(h.confirms, 0);
  assert.equal(h.events.length, 0);
  S.dirty = false;
  required(host, "[data-mode-convert]").checked = true;
  await switchButton.click();
  assert.equal(h.confirms, 1);
  assert.deepEqual(h.events, [["switch", h.detail.levelInfoAssetPath, "Dynamic", true], "close", "saved"]);
  assert.equal(switchButton.disabled, false);
  assert.equal(required(h.document, "#cfg-minOrderCount").disabled, false);
});

test("actual Dynamic mode save uses sibling order counts, round-trips and resets dirty baseline", async () => {
  const h = modalHarness({ realMode: true, gameMode: "Dynamic", initialTab: "mode" });
  await h.open();
  const host = required(h.document, "#cfg-mode-pane");
  const min = required(h.document, "#cfg-minOrderCount");
  const max = required(h.document, "#cfg-maxOrderCount");
  const duration = required(host, '[data-number="phase-0"]');
  duration.value = "300";
  await duration.dispatch("input");
  const target = required(host, "[data-mode-target]");
  target.value = "Campaign";
  await target.dispatch("change");
  await required(host, "[data-mode-switch]").click();
  assert.equal(h.confirms, 0, "mode's own unsaved input must block independently of sibling inputs");
  min.value = "3";
  max.value = "6";
  await required(host, '[data-action="save"]').click();
  assert.equal(h.writes.length, 1, required(host, "[data-mode-status]").textContent);
  assert.equal(h.writes[0][0], "mode");
  assert.equal(h.writes[0][1].minOrderCount, 3);
  assert.equal(h.writes[0][1].maxOrderCount, 6);
  assert.equal(h.writes[0][1].phases[0].durationSeconds, 300);
  assert.equal(h.detail.minOrderCount, 3);
  assert.equal(h.detail.maxOrderCount, 6);
  assert.equal(required(host, "[data-mode-status]").getAttribute("role"), "status");
  assert.equal(required(h.document, "[data-ok]").style.display, "none");
  assert.equal(h.events.length, 0, "mode config save keeps modal open without native-mode reload callback");
  await required(host, "[data-mode-switch]").click();
  assert.equal(h.confirms, 1, "successful dedicated save clears mode dirty and saved order-count baseline");
  assert.deepEqual(h.events, [["switch", h.detail.levelInfoAssetPath, "Campaign", false], "close", "saved"]);
});

test("mode switch also blocks unsaved controls mounted asynchronously in the audio tab", async () => {
  const h = modalHarness({ realMode: true });
  await h.open();
  await tab(h, "audio");
  required(h.document, "#cfg-audio-fixture").value = "edited";
  await tab(h, "mode");
  const host = required(h.document, "#cfg-mode-pane");
  const target = required(host, "[data-mode-target]");
  target.value = "Dynamic";
  await target.dispatch("change");
  await required(host, "[data-mode-switch]").click();
  assert.equal(h.confirms, 0, "unsaved audio select must block before confirmation, even when mounted after the mode editor");
  assert.equal(h.events.length, 0, "must not switch/reload and discard unsaved audio edits");
});

test("mode save tolerates float32 readback only for repair floats", async () => {
  for (const gameMode of ["Dynamic", "Horde"]) {
  for (const mismatch of [null, "json7", "float", "count", "phase", "missing", "nonfinite"]) {
    let stored;
    let reads = 0;
    const h = modalHarness({ realMode: true, gameMode, initialTab: "mode", modeApi: {
      fetchLevelModeConfig: async () => {
        if (!reads++) return {
          gameMode, assetPath: detailFixture().levelInfoAssetPath,
          minOrderCount: 2, maxOrderCount: 5, targetHealth: 100, kitchenHealth: 100,
          targetRepairSpeed: 0.11111115, targetRepairThreshold: 999999.9,
          phases: gameMode === "Dynamic" ? [1, 2].map(i => ({ id: `phase_${i}`, durationSeconds: 240, recipeGuids: ["recipe"] })) : [],
          hordeConfigs: gameMode === "Horde" ? Array.from({ length: 4 }, () => ({ targetRepairCostMax: 100, waves: [{
            id: "wave_1", intervalSeconds: 5, recipeGuids: ["recipe"], enemyGuids: ["enemy", "enemy"], spawnTimes: [0, 0],
          }] })) : [], enemies: [{ guid: "enemy", id: "enemy" }],
        };
        const saved = plain(stored);
        saved.targetRepairSpeed = Math.fround(saved.targetRepairSpeed);
        saved.targetRepairThreshold = Math.fround(saved.targetRepairThreshold);
        if (mismatch === "json7") saved.targetRepairSpeed = Number(saved.targetRepairSpeed.toPrecision(7));
        if (mismatch === "float") saved.targetRepairSpeed += 0.001;
        if (mismatch === "count") saved.targetHealth++;
        if (mismatch === "phase") {
          if (gameMode === "Dynamic") saved.phases[0].durationSeconds += 0.000001;
          else saved.hordeConfigs[0].waves[0].spawnTimes[0] += 0.000001;
        }
        if (mismatch === "missing") delete saved.targetRepairSpeed;
        if (mismatch === "nonfinite") saved.targetRepairSpeed = NaN;
        return saved;
      },
      updateLevelModeConfig: async config => { stored = plain(config); },
    } });
    await h.open();
    const host = required(h.document, "#cfg-mode-pane");
    await required(host, '[data-action="save"]').click();
    assert(stored, "actual save handler submitted config");
    assert.equal(required(host, "[data-mode-status]").getAttribute("role"), mismatch && mismatch !== "json7" ? "alert" : "status", `${gameMode}: ${mismatch}`);
  }
  }
});
