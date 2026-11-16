#!/usr/bin/env node
// Standalone UI regression: no Unity, export requests, installs, or production writes.
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { access, copyFile, mkdtemp, readFile, readdir, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { homedir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const web = fileURLToPath(new URL('../web/', import.meta.url));
const tempRoot = '/var/folders/p8/b3ryxj6s1bd4wgsspxzhfnw40000gn/T/opencode';
const require = createRequire(path.join(web, 'package.json'));
const phases = ['queued', 'starting', 'compile', 'preflight', 'orphan-clean',
  'dual-stub-clean', 'prepare', 'build', 'package', 'zip', 'restore'];
const labels = ['排队等待', '启动导出', '编译运行时', '可选包预检', '孤儿脚本检查',
  '双 Stub 检查', '准备场景', '构建资源包', '收集打包文件', '生成压缩包', '恢复场景'];
const states = { waiting: '等待中', running: '进行中', done: '已完成',
  error: '失败', skipped: '已跳过（不适用）' };
const totalNote = '预检和脚本检查也计入总耗时，切换Unity不能跳过这些检查';
const payload = '<img src=x onerror="window.__unsafe=1"><svg onload="window.__unsafe=2"></svg>';
const failures = [];
const screenshots = [];
let assertions = 0;

async function check(name, action) {
  try {
    await action();
    assertions++;
    console.log(`PASS ${name}`);
  } catch (error) {
    failures.push({ name, message: error.message });
    console.error(`FAIL ${name}: ${error.message}`);
  }
}

async function exists(file) {
  try { await access(file); return true; } catch { return false; }
}

async function cachedPlaywright() {
  if (process.env.PLAYWRIGHT_MODULE) {
    const value = process.env.PLAYWRIGHT_MODULE;
    return import(value.startsWith('file:') ? value : pathToFileURL(path.resolve(value)).href);
  }
  try { return await import(pathToFileURL(require.resolve('playwright')).href); } catch {}
  const cache = path.join(homedir(), '.npm', '_npx');
  const entries = await readdir(cache).catch(() => []);
  for (const entry of entries.sort()) {
    const file = path.join(cache, entry, 'node_modules', 'playwright', 'index.mjs');
    if (await exists(file)) return import(pathToFileURL(file).href);
  }
  throw new Error('Cached Playwright not found. Set PLAYWRIGHT_MODULE to an existing index.mjs; no install is attempted.');
}

async function cachedChromium(chromium) {
  if (process.env.CHROMIUM_PATH) {
    const file = path.resolve(process.env.CHROMIUM_PATH);
    assert.ok(await exists(file), `CHROMIUM_PATH does not exist: ${file}`);
    return file;
  }
  if (await exists(chromium.executablePath())) return chromium.executablePath();
  const caches = [path.join(homedir(), 'Library', 'Caches', 'ms-playwright'),
    path.join(homedir(), '.cache', 'ms-playwright')];
  for (const cache of caches) {
    const entries = await readdir(cache).catch(() => []);
    for (const entry of entries.filter(name => /^chromium[_-]/.test(name))
      .sort((a, b) => b.localeCompare(a, undefined, { numeric: true }))) {
      for (const relative of ['chrome-headless-shell-mac-arm64/chrome-headless-shell',
        'chrome-headless-shell-mac-x64/chrome-headless-shell',
        'chrome-mac-arm64/Chromium.app/Contents/MacOS/Chromium',
        'chrome-mac/Chromium.app/Contents/MacOS/Chromium',
        'chrome-linux/headless_shell', 'chrome-linux/chrome', 'chrome-linux64/chrome']) {
        const file = path.join(cache, entry, relative);
        if (await exists(file)) return file;
      }
    }
  }
  throw new Error('Cached Chromium not found. Set CHROMIUM_PATH; no download is attempted.');
}

function snapshot(phase = 'orphan-clean') {
  const stages = phases.map(name => ({ phase: name, state: 'waiting', elapsedMs: 0,
    completed: 0, total: 0, message: '' }));
  Object.assign(stages[0], { state: 'done', elapsedMs: 20 });
  Object.assign(stages[1], { state: 'done', elapsedMs: 50 });
  Object.assign(stages[2], { state: 'skipped' });
  Object.assign(stages[3], { state: 'done', elapsedMs: 23360 });
  Object.assign(stages[4], { state: phase === 'orphan-clean' ? 'running' : 'done',
    elapsedMs: 33240, message: '检查孤儿脚本引用；快照未报告完成。' });
  if (phase === 'prepare') {
    Object.assign(stages[5], { state: 'done', elapsedMs: 180 });
    Object.assign(stages[6], { state: 'running', elapsedMs: 1234,
      completed: 3, total: 15, message: '准备第 3/15 个场景。' });
  }
  return { status: 'running', mode: 'all', setName: 'ui-fixture', phase,
    message: `导出阶段 ${phase}`, error: '', zipFileName: '', fileCount: 0, stages };
}

async function run() {
  const { chromium } = await cachedPlaywright();
  const executablePath = await cachedChromium(chromium);
  const ts = require('typescript');
  const source = await readFile(path.join(web, 'src', 'busy.ts'), 'utf8');
  const compiled = ts.transpileModule(source, { fileName: 'busy.ts', reportDiagnostics: true,
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ES2022 } });
  assert.equal(compiled.diagnostics?.filter(d => d.category === ts.DiagnosticCategory.Error).length, 0,
    'Real busy.ts must transpile successfully');
  const output = await mkdtemp(path.join(tempRoot, 'export-progress-ui-'));
  console.log(`Artifacts: ${output}\nChromium: ${executablePath}`);
  await writeFile(path.join(output, 'busy.js'), compiled.outputText);
  await copyFile(path.join(web, 'src', 'style.css'), path.join(output, 'style.css'));
  await copyFile(path.join(web, 'src', 'styles', 'tokens.css'), path.join(output, 'tokens.css'));
  await writeFile(path.join(output, 'index.html'), `<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Export progress UI fixture</title><link rel="icon" href="data:,">
<link rel="stylesheet" href="/tokens.css"><link rel="stylesheet" href="/style.css"></head>
<body><button id="launch" type="button">导出进度 UI 检查</button><script type="module" src="/fixture.js"></script></body></html>`);
  await writeFile(path.join(output, 'fixture.js'), `import * as busy from '/busy.js';
window.busy = busy;
window.applySnapshot = async () => {
  const response = await fetch('/mock/snapshot', { cache: 'no-store' });
  if (!response.ok) throw new Error('Mock snapshot HTTP ' + response.status);
  const snapshot = await response.json();
  busy.setBusyStages(snapshot.stages, snapshot.phase);
  busy.setBusyMessage(snapshot.message, snapshot.note || '');
  if (snapshot.status === 'error') busy.setBusyResult(snapshot.error || snapshot.message);
  return snapshot;
};
window.fixtureReady = true;
`);
  let current = snapshot();
  const requests = [];
  const serverErrors = [];
  const assets = new Map([
    ['/', ['index.html', 'text/html; charset=utf-8']],
    ['/busy.js', ['busy.js', 'text/javascript; charset=utf-8']],
    ['/fixture.js', ['fixture.js', 'text/javascript; charset=utf-8']],
    ['/style.css', ['style.css', 'text/css; charset=utf-8']],
    ['/tokens.css', ['tokens.css', 'text/css; charset=utf-8']],
  ]);
  const server = createServer(async (request, response) => {
    const route = request.url;
    requests.push({ method: request.method, route });
    try {
      assert.equal(request.method, 'GET', 'Only fixture GET requests are permitted');
      response.setHeader('Cache-Control', 'no-store');
      if (route === '/mock/snapshot') {
        response.writeHead(200, { 'Content-Type': 'application/json' });
        response.end(JSON.stringify(current));
      } else if (assets.has(route)) {
        const [file, type] = assets.get(route);
        response.writeHead(200, { 'Content-Type': type });
        response.end(await readFile(path.join(output, file)));
      } else if (/^\/icons\/ingredients\/(TomatoSO|CheeseSO|PotatoSO)\.png$/.test(route)) {
        const data = await readFile(path.join(web, 'public', route.slice(1)));
        response.writeHead(200, { 'Content-Type': 'image/png' });
        response.end(data);
      } else {
        throw new Error(`Unexpected fixture request: ${request.method} ${route}`);
      }
    } catch (error) {
      serverErrors.push(error.message);
      response.writeHead(500);
      response.end('Fixture request failed');
    }
  });
  let browser;
  const runtimeErrors = [];
  const blockedRequests = [];
  try {
    await new Promise((resolve, reject) => {
      server.once('error', reject);
      server.listen(0, '127.0.0.1', resolve);
    });
    const origin = `http://127.0.0.1:${server.address().port}`;
    browser = await chromium.launch({ executablePath, headless: true });
    for (const viewport of [{ width: 1280, height: 900 }, { width: 390, height: 844 }]) {
      const name = viewport.width === 1280 ? 'desktop' : 'mobile';
      const context = await browser.newContext({ viewport, deviceScaleFactor: 1, serviceWorkers: 'block' });
      await context.route('**/*', async route => {
        if (new URL(route.request().url()).origin === origin) await route.continue();
        else { blockedRequests.push(route.request().url()); await route.abort(); }
      });
      const page = await context.newPage();
      page.setDefaultTimeout(5000);
      page.on('pageerror', error => runtimeErrors.push(`${name}: ${error.message}`));
      await page.goto(origin, { waitUntil: 'networkidle' });
      await page.waitForFunction(() => window.fixtureReady);
      const overlay = page.locator('#busy-overlay');
      const rows = page.locator('.busy-stage');
      const panel = page.locator('#busy-stages');
      const close = page.locator('.busy-close');
      const takeScreenshot = async label => {
        const file = path.join(output, `${label}.png`);
        await page.screenshot({ path: file, fullPage: true, animations: 'disabled' });
        screenshots.push(file);
        console.log(`SCREENSHOT ${file}`);
      };
      const apply = async value => {
        current = structuredClone(value);
        const received = await page.evaluate(() => window.applySnapshot());
        assert.deepEqual(received, value, 'Fixture delivers the exact backend snapshot');
      };
      const show = async () => {
        await page.locator('#launch').focus();
        await page.evaluate(() => window.busy.showBusy('正在导出测试快照'));
      };
      const verifyRows = async value => {
        await check(`${name}: exactly 11 ordered stage rows and native progress elements`, async () => {
          assert.equal(await rows.count(), 11);
          assert.equal(await panel.locator('progress').count(), 11);
          assert.equal(await panel.getAttribute('role'), 'region');
          assert.equal(await panel.getAttribute('tabindex'), '0');
        });
        for (const [index, stage] of value.stages.entries()) {
          await check(`${name}: ${stage.phase} state, elapsed, native progress and accessible label`, async () => {
            const row = rows.nth(index);
            const progress = row.locator('progress');
            const max = stage.total > 0 ? stage.total : 1;
            const elapsed = stage.elapsedMs < 60000 ? `${(stage.elapsedMs / 1000).toFixed(2)} 秒`
              : `${Math.floor(stage.elapsedMs / 60000)} 分钟 ${((stage.elapsedMs % 60000) / 1000).toFixed(2)} 秒`;
            assert.ok((await row.getAttribute('class')).split(' ').includes(stage.state));
            assert.equal(await row.locator('.busy-stage-head > span').first().textContent(),
              `${labels[index]} · ${states[stage.state]}${stage.total > 0 ? ` · ${stage.completed}/${stage.total}` : ''}`);
            assert.equal(await row.locator('.busy-stage-elapsed').textContent(), elapsed);
            assert.equal(await progress.getAttribute('aria-labelledby'), `busy-stage-${index}`);
            const accessibleName = await row.locator('.busy-stage-head > span').first().textContent();
            assert.equal(await row.getByRole('progressbar', { name: accessibleName, exact: true }).count(), 1);
            assert.equal(await progress.evaluate(el => document.getElementById(el.getAttribute('aria-labelledby'))?.textContent),
              accessibleName);
            assert.equal(await progress.evaluate(el => el.max), max);
            if (stage.state === 'running' && stage.total === 0) {
              assert.equal(await progress.getAttribute('value'), null);
              assert.equal(await progress.evaluate(el => el.matches(':indeterminate')), true);
              assert.equal(await progress.getAttribute('aria-valuetext'), '进行中，尚无计数');
            } else {
              const expected = stage.state === 'done' ? max
                : ['waiting', 'skipped'].includes(stage.state) ? 0 : Math.max(0, Math.min(stage.completed, max));
              assert.equal(await progress.evaluate(el => el.value), expected);
              assert.equal(await progress.evaluate(el => el.position), expected / max);
              if (stage.state === 'done') assert.equal(await progress.getAttribute('aria-valuetext'), '已完成，100%');
            }
            assert.equal(await row.getAttribute('aria-current'),
              stage.phase === value.phase || stage.state === 'running' ? 'step' : null);
          });
        }
      };
      await show();
      const orphan = snapshot();
      await apply(orphan);
      await verifyRows(orphan);
      await check(`${name}: total-time note and every row reachable without horizontal overflow`, async () => {
        assert.equal(await panel.locator('.busy-stage-note').textContent(), totalNote);
        for (let index = 0; index < 11; index++) {
          const geometry = await rows.nth(index).evaluate(row => {
            row.scrollIntoView({ block: 'nearest' });
            const r = row.getBoundingClientRect();
            const p = row.closest('.busy-stages').getBoundingClientRect();
            const bar = row.querySelector('progress').getBoundingClientRect();
            return { left: r.left, right: r.right, top: r.top, bottom: r.bottom,
              panelTop: p.top, panelBottom: p.bottom, barWidth: bar.width,
              scrollWidth: row.scrollWidth, clientWidth: row.clientWidth, width: innerWidth, height: innerHeight };
          });
          assert.ok(geometry.left >= 0 && geometry.right <= geometry.width + 1, `${phases[index]} fits viewport`);
          assert.ok(geometry.top >= Math.max(0, geometry.panelTop) - 1
            && geometry.bottom <= Math.min(geometry.height, geometry.panelBottom) + 1, `${phases[index]} can scroll fully into view`);
          assert.ok(geometry.barWidth > 100 && geometry.scrollWidth <= geometry.clientWidth + 1,
            `${phases[index]} progress readable without horizontal overflow`);
        }
        await panel.evaluate(el => { el.scrollTop = 0; });
      });
      await check(`${name}: running Escape cannot dismiss; no timer invents completion`, async () => {
        await page.keyboard.press('Escape');
        const before = await panel.innerHTML();
        await page.waitForTimeout(150);
        assert.equal(await panel.innerHTML(), before);
        assert.equal(await overlay.isVisible(), true);
        assert.equal(await close.isVisible(), false);
        assert.equal(await rows.nth(4).locator('progress').getAttribute('value'), null);
      });
      const prepare = snapshot('prepare');
      await apply(prepare);
      await verifyRows(prepare);
      await check(`${name}: prepare 3/15 is determinate 20%, not a simulated timer`, async () => {
        assert.equal(await rows.nth(6).locator('progress').evaluate(el => el.position), 0.2);
        assert.equal(await rows.nth(6).locator('progress').getAttribute('aria-valuetext'), '进行中，3/15');
      });
      await panel.evaluate(el => { el.scrollTop = 0; });
      await takeScreenshot(name);
      await check(`${name}: backend status done does not fabricate row completion`, async () => {
        await apply({ ...prepare, status: 'done', phase: 'done' });
        assert.equal(await rows.nth(6).getAttribute('class'), 'busy-stage running');
        assert.equal(await rows.nth(6).locator('progress').evaluate(el => el.value), 3);
        assert.equal(await rows.nth(10).getAttribute('class'), 'busy-stage waiting');
      });
      await check(`${name}: unsafe message/note/phase/detail are literal text`, async () => {
        const unsafe = structuredClone(prepare);
        unsafe.message = payload;
        unsafe.note = payload;
        unsafe.phase = payload;
        Object.assign(unsafe.stages[6], { phase: payload, message: payload });
        await apply(unsafe);
        assert.equal(await page.locator('#busy-msg').textContent(), payload);
        assert.equal(await page.locator('#busy-note').textContent(), payload);
        assert.equal(await rows.nth(6).locator('.busy-stage-detail').textContent(), payload);
        assert.ok((await rows.nth(6).textContent()).includes(`其他阶段（${payload}）`));
        assert.equal(await page.locator('#busy-msg *, #busy-note *, .busy-stage img, .busy-stage svg').count(), 0);
        assert.equal(await page.evaluate(() => window.__unsafe), undefined);
      });
      const error = snapshot('prepare');
      error.status = 'error';
      error.error = `准备场景失败：${payload}`;
      error.stages[6].state = 'error';
      error.stages[6].message = `保留失败详情：${payload}`;
      await apply(error);
      await page.evaluate(() => window.busy.hideBusy());
      await check(`${name}: error retained after hideBusy with exact rows, alert, Close and focus`, async () => {
        assert.equal(await overlay.isVisible(), true);
        assert.equal(await rows.count(), 11);
        assert.equal(await page.locator('.busy-box').getAttribute('aria-busy'), 'false');
        assert.equal(await page.locator('#busy-msg').getAttribute('role'), 'alert');
        assert.equal(await page.locator('#busy-msg').textContent(), error.error);
        assert.equal(await rows.nth(6).locator('.busy-stage-detail').textContent(), error.stages[6].message);
        assert.equal(await rows.nth(6).locator('progress').evaluate(el => el.value), 3);
        assert.equal(await rows.nth(10).getAttribute('class'), 'busy-stage waiting');
        assert.equal(await close.isVisible(), true);
        assert.equal(await close.evaluate(el => el === document.activeElement), true);
        assert.equal(await page.locator('#busy-msg *, .busy-stage img, .busy-stage svg').count(), 0);
        assert.equal(await page.evaluate(() => window.__unsafe), undefined);
      });
      if (name === 'desktop') {
        await rows.nth(6).evaluate(el => el.scrollIntoView({ block: 'center' }));
        await takeScreenshot('error');
      }
      await check(`${name}: retained result Tab/Shift+Tab stays in result controls`, async () => {
        await close.focus();
        await page.keyboard.press('Tab');
        assert.equal(await panel.evaluate(el => el === document.activeElement), true);
        await page.keyboard.press('Shift+Tab');
        assert.equal(await close.evaluate(el => el === document.activeElement), true);
      });
      await check(`${name}: Close clears result and restores launcher focus`, async () => {
        await close.click();
        assert.equal(await overlay.isVisible(), false);
        assert.equal(await rows.count(), 0);
        assert.equal(await page.locator('#launch').evaluate(el => el === document.activeElement), true);
      });
      await show();
      await apply(error);
      await page.evaluate(() => window.busy.hideBusy());
      await check(`${name}: Escape dismisses retained error and restores launcher focus`, async () => {
        await page.keyboard.press('Escape');
        assert.equal(await overlay.isVisible(), false);
        assert.equal(await rows.count(), 0);
        assert.equal(await page.locator('#launch').evaluate(el => el === document.activeElement), true);
      });
      await show();
      await apply(error);
      await page.evaluate(() => window.busy.hideBusy());
      await check(`${name}: a new ordinary busy clears retained error without requiring Close`, async () => {
        await page.evaluate(() => window.busy.showBusy('普通加载任务'));
        assert.equal(await overlay.isVisible(), true);
        assert.equal(await overlay.evaluate(el => el.classList.contains('busy-result')), false);
        assert.equal(await panel.isVisible(), false);
        assert.equal(await rows.count(), 0);
        assert.equal(await close.isVisible(), false);
        assert.equal(await page.locator('#busy-msg').textContent(), '普通加载任务');
        assert.equal(await page.locator('#busy-msg').getAttribute('role'), 'status');
        assert.equal(await page.locator('#busy-note').isVisible(), false);
        assert.equal(await page.locator('.busy-box').getAttribute('aria-busy'), 'true');
        await page.evaluate(() => { window.busy.showBusy('嵌套普通任务'); window.busy.hideBusy(); });
        assert.equal(await overlay.isVisible(), true);
        await page.evaluate(() => window.busy.hideBusy());
        assert.equal(await overlay.isVisible(), false);
        assert.equal(await page.locator('#launch').evaluate(el => el === document.activeElement), true);
      });
      await show();
      await apply(prepare);
      await check(`${name}: old backend without stages hides panel; normal hide releases task`, async () => {
        const legacy = { ...prepare };
        delete legacy.stages;
        await apply(legacy);
        assert.equal(await rows.count(), 0);
        assert.equal(await panel.isVisible(), false);
        assert.equal(await page.locator('.busy-box').evaluate(el => el.classList.contains('has-stages')), false);
        await page.evaluate(() => window.busy.hideBusy());
        assert.equal(await overlay.isVisible(), false);
      });
      await context.close();
    }
    await check('no browser exceptions, unsafe HTML execution, or unexpected fixture requests', () => {
      assert.deepEqual(runtimeErrors, []);
      assert.deepEqual(serverErrors, []);
      assert.deepEqual(blockedRequests, []);
      assert.ok(requests.some(request => request.route === '/mock/snapshot'));
      assert.ok(requests.every(request => request.method === 'GET' && !request.route.startsWith('/api/')));
    });
  } finally {
    if (browser) await browser.close();
    await new Promise(resolve => server.close(resolve));
    const report = { assertions, failures, screenshots, runtimeErrors, serverErrors, blockedRequests, requests };
    await writeFile(path.join(output, 'report.json'), JSON.stringify(report, null, 2));
    console.log(`\nASSERTIONS ${assertions} passed, ${failures.length} failed\nREPORT ${path.join(output, 'report.json')}`);
    for (const file of screenshots) console.log(`SCREENSHOT ${file}`);
  }
  if (failures.length) process.exitCode = 1;
}

run().catch(error => { console.error(error.stack || error); process.exitCode = 1; });
