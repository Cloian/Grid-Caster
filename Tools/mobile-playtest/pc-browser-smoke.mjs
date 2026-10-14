// 실제 PC Web의 클릭/키 입력/크기 변경 검사. 재미·실기기 Safari 검증과는 별개다.
import { createRequire } from 'node:module';
import fs from 'node:fs/promises';
import path from 'node:path';
const require = createRequire(import.meta.url);
const { chromium } = require(process.argv[3] || 'playwright');
const output = path.resolve(process.argv[4] || 'Builds/PcBoardWeb/verification');
await fs.mkdir(output, { recursive: true });
const browser = await chromium.launch({ executablePath: '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', headless: true,
  args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const context = await browser.newContext({ viewport: { width: 1920, height: 1080 }, deviceScaleFactor: 1 });
const page = await context.newPage();
const logs = [], errors = [], checks = [], sizes = [];
page.on('console', message => { logs.push(message.text()); if (message.type() === 'error') errors.push(message.text()); });
page.on('pageerror', error => errors.push(error.message));
function check(ok, name) { if (!ok) throw new Error(name); checks.push('PASS ' + name); console.log('PASS', name); }
async function state() {
  const before = logs.length;
  await page.evaluate(() => instance.SendMessage('GameHUD', 'ReportPcPlaytestState'));
  for (let i = 0; i < 100; i++) {
    const line = logs.slice(before).find(item => item.includes('PC_PLAYTEST_STATE '));
    if (line) return JSON.parse(line.slice(line.indexOf('PC_PLAYTEST_STATE ') + 18));
    await page.waitForTimeout(50);
  }
  throw new Error('No PC diagnostic snapshot');
}
async function waitState(predicate) {
  const limit = Date.now() + 15000;
  let s;
  do { s = await state(); if (predicate(s)) return s; await page.waitForTimeout(100); } while (Date.now() < limit);
  throw new Error('PC state timeout: ' + JSON.stringify(s));
}
async function clickUi(name) {
  const s = await state(), t = s.ui.find(item => item.name === name);
  if (!t) throw new Error('No visible UI: ' + name);
  const box = await page.locator('#game').boundingBox();
  await page.mouse.move(box.x + t.x * box.width / s.width, box.y + t.y * box.height / s.height);
  await page.waitForTimeout(200);
  await page.mouse.down(); await page.waitForTimeout(300); await page.mouse.up();
  await page.waitForTimeout(1000);
  return state();
}
async function boardPoint(dx, dy, click = false) {
  const s = await state(), box = await page.locator('#game').boundingBox();
  const x = box.x + (s.playerX + dx * s.tilePixels) * box.width / s.width;
  const y = box.y + (s.playerY - dy * s.tilePixels) * box.height / s.height;
  if (click) await page.mouse.click(x, y, { delay: 100 }); else await page.mouse.move(x, y);
  await page.waitForTimeout(500); return state();
}
async function capture(name) { await page.screenshot({ path: path.join(output, name + '.png') }); }
function geometry(s) {
  const scale = Math.min(s.width / 1920, s.height / 1080), [x, y, w, h] = s.board;
  check(!s.mobile && Math.abs(w - h) < 1 && Math.abs(w - 800 * scale) < 1, `square full board ${s.width}x${s.height}`);
  check(Math.abs(x + w / 2 - s.width / 2) < 1 && Math.abs(y + h / 2 - (s.height / 2 - 40 * scale)) < 1, 'board remains fixed at composition center');
  for (const name of ['BuildHudPanel', 'RelicHudPanel', 'WaveHud', 'TimeHud', 'HealthHud', 'UltimateFrame', 'TraitHud', 'AttackActionFrame', 'MoveActionFrame', 'ActionModeHud']) {
    const t = s.ui.find(item => item.name === name); check(!!t, 'HUD exists: ' + name);
    const left = t.x - t.width / 2, top = t.y - t.height / 2, right = left + t.width, bottom = top + t.height;
    check(left >= -1 && top >= -1 && right <= s.width + 1 && bottom <= s.height + 1, 'HUD inside screen: ' + name);
    check(right <= x + 1 || left >= x + w - 1 || bottom <= y + 1 || top >= y + h - 1, 'HUD outside full board: ' + name);
  }
  const left = s.ui.find(t => t.name === 'BuildHudPanel'), right = s.ui.find(t => t.name === 'RelicHudPanel');
  check(Math.abs(left.width - 330 * scale) < 1 && Math.abs(right.width - 330 * scale) < 1, 'side panels share common scale');
  check(Math.abs(x - left.x - left.width / 2 - 18 * scale) < 1 && Math.abs(right.x - right.width / 2 - x - w - 18 * scale) < 1, 'side gaps stay proportional to board');
  sizes.push({ width: s.width, height: s.height, board: s.board, ui: s.ui });
}
try {
  await page.goto(process.argv[2] || 'http://127.0.0.1:8770');
  await page.getByRole('button', { name: '클릭하여 시작' }).click();
  await page.waitForFunction(() => typeof instance !== 'undefined' && instance, { timeout: 180000 });
  await page.waitForTimeout(5000);
  let s = await state(); check(!s.mobile && s.startSelection && s.worldTurns === 0, 'desktop Web starts PC selection with world frozen');
  await capture('01-pc-start');
  s = await clickUi('TraitCard_1'); check(!s.startSelection && s.rewardSelection, 'real mouse selects starting trait');
  s = await clickUi('Details'); check(s.detailOpen && s.rewardSelection && !s.canAct, 'reward details block combat');
  await clickUi('CloseDetails'); await capture('01b-pc-reward-after-detail'); s = await clickUi('UpgradeChoice_1');
  check(!s.rewardSelection && s.canAct && s.worldTurns === 0, 'real mouse reward starts PC combat without turn');
  geometry(s); await capture('02-pc-combat-1920');
  // Web 프레임보다 짧은 keydown/up 자동 입력을 피한다. 게임의 입력 함수를 직접 호출하지 않는다.
  await page.keyboard.press('f', { delay: 100 }); s = await state(); check(!s.ready && s.worldTurns === 0, 'unready F does not consume a turn');
  await page.keyboard.press('a', { delay: 100 }); s = await boardPoint(1, 1); check(s.action === 'Attack' && s.worldTurns === 0, 'A and hover preview do not consume turn');
  await capture('03-pc-attack-preview');
  const box = await page.locator('#game').boundingBox();
  await page.mouse.click(box.x + s.playerX, box.y + s.playerY, { button: 'right', delay: 100 });
  await page.waitForTimeout(500);
  s = await state(); check(s.action === 'None' && s.worldTurns === 0, 'right-click cancels for free');
  const initial = s;
  await page.keyboard.press('s', { delay: 100 }); await page.waitForTimeout(200); s = await boardPoint(0, 1, true);
  s = await waitState(item => item.worldTurns === 1 && item.canAct);
  check(s.x === initial.x && s.y === initial.y + 1 && s.worldTurns === 1, 'S and click move exactly one tile and world turn');
  await clickUi('AttackActionFrame'); s = await boardPoint(1, 0, true);
  s = await waitState(item => item.worldTurns === 2 && item.canAct);
  check(s.worldTurns === 2 && s.canAct, 'attack card and click finish exactly one world turn');
  s = await clickUi('MovementArtDetails'); check(s.detailOpen && !s.canAct && s.worldTurns === 2, 'build detail click blocks board');
  await boardPoint(0, 1, true); s = await state(); check(s.worldTurns === 2, 'detail dialog cannot click through into combat');
  // 상세창의 배경 클릭은 창을 닫을 수 있지만 같은 클릭이 보드 행동이 되어서는 안 된다.
  if (s.detailOpen) await clickUi('CloseDetails');
  await page.keyboard.press('Escape', { delay: 100 }); await page.waitForTimeout(200); s = await state(); check(s.paused && !s.canAct, 'Escape pauses PC');
  s = await clickUi('ContinueButton'); check(!s.paused && s.canAct && s.worldTurns === 2, 'mouse resumes without turn');
  await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: true }); document.dispatchEvent(new Event('visibilitychange')); });
  s = await state(); check(s.paused && s.worldTurns === 2, 'browser background hook pauses PC without a turn');
  await page.evaluate(() => { delete document.hidden; document.dispatchEvent(new Event('visibilitychange')); });
  check((await state()).paused, 'foreground does not resume without consent'); await clickUi('ContinueButton');
  for (const [width, height] of [[1280,720],[1024,768],[2560,1080],[3840,2160],[800,1200],[960,540],[1920,1080]]) {
    await page.setViewportSize({ width, height }); await page.waitForTimeout(800);
    s = await state(); geometry(s); check(s.worldTurns === 2, 'resize does not advance world');
    check(!(await page.locator('#rotate').isVisible()), 'desktop is not blocked by phone rotation overlay');
    await capture(`pc-${width}x${height}`);
    s = await clickUi('AttackActionFrame'); check(s.action === 'Attack', 'resized attack card still clickable');
    s = await clickUi('MoveActionFrame'); check(s.action === 'Move', 'resized move card still clickable');
  }
  check(errors.length === 0, 'no browser/runtime error logged');
} catch (error) {
  await capture('failure'); throw error;
} finally {
  await fs.writeFile(path.join(output, 'pc-browser-log.json'), JSON.stringify({ engine: 'Chrome desktop, SwiftShader', checks, sizes, errors, logs }, null, 2));
  await browser.close();
}
