// Browser scenarios of the editor in Way.Shell, against the running demo (Production, AllowAnonymous):
//   node check.mjs http://localhost:5089 [chrome|msedge|firefox|webkit]
// Needs the playwright package (npm i --no-save playwright); firefox and webkit need `npx playwright install`.
import { chromium, firefox, webkit } from 'playwright';

const base = process.argv[2].replace(/\/$/, '');
const which = process.argv[3] ?? 'chrome';
const browser = which === 'firefox' ? await firefox.launch() : which === 'webkit' ? await webkit.launch() : await chromium.launch({ channel: which });
const results = [];
const record = (name, ok, detail = '') => results.push({ name, ok, detail });
const visible = (l) => l.first().waitFor({ timeout: 10000 }).then(() => true, () => false);

async function open(options = {}) {
  const context = await browser.newContext({ locale: 'en-US', colorScheme: 'light', ...options });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  page.on('console', (m) => { if (m.type() === 'error' && !m.text().startsWith('Failed to load resource')) errors.push(m.text()); });
  page.on('response', (r) => { if (r.status() >= 400 && !r.url().endsWith('/favicon.ico')) errors.push(`${r.status()} ${r.url()}`); });
  return { context, page, errors };
}

{
  const { context, page, errors } = await open();
  await page.goto(`${base}/config/`);
  record('editor opens in the shell', await visible(page.getByRole('heading', { name: 'All Settings' })));
  record('title from the Way', (await page.title()) === 'Configuration', await page.title());
  const menu = page.getByTestId('way-menu');
  record('sections in the shell menu', await visible(menu.getByText('Branding')));

  const font = await page.locator('main h1').first().evaluate((e) => getComputedStyle(e).fontFamily); // Typography h1, no font of its own
  record("ConfigWay's monospace theme in its area", /IBM Plex Mono/.test(font), font);
  const shellTitleFont = await page.locator('nav, [data-testid="way-menu"]').first().evaluate(() =>
    getComputedStyle(document.querySelector('.MuiDrawer-paper p')).fontFamily);
  record("shell's own font in the shell", !/IBM Plex Mono/.test(shellTitleFont), shellTitleFont);

  await menu.getByText('Branding').click();
  record('section from the menu', (await visible(page.getByRole('heading', { name: 'Branding' }))) && page.url().endsWith('#/section/Branding'), page.url());
  await menu.getByText('Smtp').click();
  record('second section from the menu (paths from the Way root)', page.url().endsWith('#/section/Smtp'), page.url());
  await menu.getByText('Branding').click();
  await page.getByRole('heading', { name: 'Branding' }).waitFor();

  // The fields are textareas; find Company Name by its value.
  const fieldByValue = async (value) => {
    await page.locator('main textarea').first().waitFor();
    const index = await page.locator('main textarea').evaluateAll((els, v) => els.findIndex((e) => e.value === v), value);
    return index >= 0 ? page.locator('main textarea').nth(index) : null;
  };
  const field = await fieldByValue('Acme Corp');
  record('field with its value', !!field);
  if (field) {
    await field.fill('Acme Pilot');
    await Promise.all([
      page.waitForResponse((r) => r.url().endsWith('/config/api/UpdateConfiguration')),
      // The editor loads the settings again after saving; reloading before that answers aborts it (WebKit reports it).
      page.waitForResponse((r) => r.url().endsWith('/config/api/GetConfiguration')),
      page.getByRole('button', { name: 'Save' }).click(),
    ]);
    await page.reload();
    const saved = await fieldByValue('Acme Pilot');
    record('saving through the shell API client', !!saved);
    if (saved) {
      await saved.fill('Acme Corp');
      await Promise.all([
        page.waitForResponse((r) => r.url().endsWith('/config/api/UpdateConfiguration')),
        page.getByRole('button', { name: 'Save' }).click(),
      ]);
    }
  }

  await page.goto(`${base}/config/#/section/Nope`);
  await page.waitForTimeout(500);
  record('unknown section goes to the Way root', page.url().endsWith('/config/#/'), page.url());

  await page.getByRole('button', { name: 'Language' }).click();
  await page.getByRole('menuitem', { name: 'Polski' }).click();
  record('language from the shell', await visible(page.getByRole('heading', { name: 'Wszystkie ustawienia' })));

  await page.getByRole('button', { name: 'Ciemny motyw' }).click();
  const header = await page.locator('main header').first().evaluate((e) => getComputedStyle(e).backgroundColor);
  record("ConfigWay's dark paper in its area", header === 'rgb(36, 36, 36)', header);
  record('no errors', errors.length === 0, errors.join(' | '));
  await context.close();
}

{
  const { context, page } = await open({ viewport: { width: 390, height: 800 } });
  await page.goto(`${base}/config/`);
  await visible(page.getByRole('heading', { name: 'All Settings' }));
  await page.getByRole('button', { name: 'Open menu' }).click();
  await page.getByTestId('way-menu').getByText('Smtp').click();
  const closed = await page.getByTestId('way-menu').waitFor({ state: 'hidden', timeout: 5000 }).then(() => true, () => false);
  record('phone: a section from the menu closes it', closed && page.url().endsWith('#/section/Smtp'), page.url());
  await context.close();
}

await browser.close();
const failed = results.filter((r) => !r.ok);
for (const r of results) console.log(`${r.ok ? 'ok  ' : 'FAIL'} ${r.name}${r.detail ? ` — ${r.detail}` : ''}`);
console.log(`\n${which} ${base}: ${results.length - failed.length}/${results.length} ok`);
process.exit(failed.length ? 1 : 0);
