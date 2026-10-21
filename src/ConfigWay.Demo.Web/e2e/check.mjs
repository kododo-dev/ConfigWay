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

// Feature flags (Kododo.ConfigWay.FeatureManagement): the demo's flags come from appsettings. Screenshots go to
// $SHOTS when it is set.
{
  const { context, page, errors } = await open({ viewport: { width: 1280, height: 800 } });
  const shot = (name) => process.env.SHOTS ? page.screenshot({ path: `${process.env.SHOTS}/${which}-${name}.png` }) : null;
  const flagsLoaded = () => page.waitForResponse((r) => r.url().endsWith('/config/api/GetFeatureFlags'));
  const homeFlag = async (name, user = '') => {
    const html = await (await fetch(`${base}/?user=${user}`)).text();
    return new RegExp(`>${name}</td><td><span[^>]*>✓ on`).test(html);
  };

  // Overrides left by an earlier run would change what the scenarios see.
  for (const id of ['BetaBanner', 'NewCheckout', 'E2eFlag'])
    await fetch(`${base}/config/api/ResetFeatureFlag`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id }),
    });

  await page.goto(`${base}/config/`);
  const menu = page.getByTestId('way-menu');
  record('flags: in the menu', await visible(menu.getByText('Feature flags')));
  await menu.getByText('Feature flags').click();
  record('flags: list', (await visible(page.getByTestId('flag-row-BetaBanner'))) && page.url().endsWith('#/flags'), page.url());
  record('flags: .NET schema flag listed', await visible(page.getByTestId('flag-row-ReadOnlyMode').getByText('.NET schema')));
  record('flags: variants shown', await visible(page.getByTestId('flag-row-SearchEngine').getByText('variants')));
  await shot('flags-list');

  // A switch saves at once; Undo puts the flag back.
  const beta = page.getByRole('switch', { name: 'BetaBanner' });
  // The list is drawn again after the response that reloads it; wait for the switch rather than read it at once.
  const becomes = (checked) => beta.evaluate((e, c) => new Promise((resolve) => {
    const check = () => (e.checked === c ? resolve(true) : setTimeout(check, 50));
    check();
  }), checked).then(() => true);
  await Promise.all([flagsLoaded(), beta.click()]);
  record('flags: switch turns a flag off', (await becomes(false)) && !(await homeFlag('BetaBanner')));
  record('flags: the switch is an override', await visible(page.getByTestId('flag-row-BetaBanner').getByText('overridden')));
  await Promise.all([flagsLoaded(), page.getByRole('button', { name: 'Undo' }).click()]);
  record('flags: undo', (await becomes(true)) && (await homeFlag('BetaBanner')));
  record('flags: undo removes the override it made',
    await page.getByTestId('flag-row-BetaBanner').getByText('overridden').waitFor({ state: 'detached', timeout: 5000 }).then(() => true, () => false));

  // The targeting editor, and the check through the app's feature manager.
  await page.getByTestId('flag-row-NewCheckout').click();
  record('flags: editor opens', (await visible(page.getByRole('heading', { name: 'NewCheckout' }))) && page.url().endsWith('#/flag/NewCheckout'), page.url());
  record('flags: targeting form with its users', await visible(page.getByRole('button', { name: 'alice' })));
  await page.getByRole('textbox', { name: 'User', exact: true }).fill('bob');
  await page.getByLabel('Groups (comma-separated)').fill('beta-testers');
  await page.getByRole('button', { name: 'Check', exact: true }).click();
  record('flags: check for a user in a targeted group', await visible(page.getByText('On for this user')));
  await page.getByRole('textbox', { name: 'User', exact: true }).fill('carol');
  await page.getByLabel('Groups (comma-separated)').fill('');
  await page.getByRole('button', { name: 'Check', exact: true }).click();
  record('flags: check for another user', await visible(page.getByText('Off for this user')));
  await shot('flag-targeting');

  // Override, then reset to the configuration.
  await page.getByLabel('Enabled').click();
  await Promise.all([flagsLoaded(), page.getByRole('button', { name: 'Save' }).click()]);
  record('flags: saved override', (await visible(page.getByText('Overrides the flag defined'))) && !(await homeFlag('NewCheckout', 'alice')));
  await page.getByRole('button', { name: 'Reset to configuration' }).click();
  await Promise.all([flagsLoaded(), page.getByRole('dialog').getByRole('button', { name: 'Reset to configuration' }).click()]);
  record('flags: reset to configuration', (await visible(page.getByText('Defined in the application'))) && (await homeFlag('NewCheckout', 'alice')));

  // A new flag: the API turns down a wrong percentage, then the flag is added and deleted.
  await page.goto(`${base}/config/#/flags/new`);
  await page.getByTestId('flag-id').fill('E2eFlag');
  await page.getByRole('button', { name: 'Add condition' }).click();
  await page.getByRole('menuitem', { name: 'Percentage' }).click();
  await page.getByLabel('%', { exact: true }).fill('150');
  await page.getByRole('button', { name: 'Save' }).click();
  record('flags: error on the condition', await visible(page.getByText('The percentage must be between 0 and 100.')));
  await shot('flag-new-error');
  await page.getByLabel('%', { exact: true }).fill('100');
  await Promise.all([flagsLoaded(), page.getByRole('button', { name: 'Save' }).click()]);
  await page.waitForURL(/#\/flag\/E2eFlag$/, { timeout: 5000 }).catch(() => {});
  record('flags: new flag saved', page.url().endsWith('#/flag/E2eFlag') && (await homeFlag('E2eFlag')), page.url());
  await page.getByRole('button', { name: 'Delete flag' }).click();
  await Promise.all([flagsLoaded(), page.getByRole('dialog').getByRole('button', { name: 'Delete flag' }).click()]);
  await page.waitForURL(/#\/flags$/, { timeout: 5000 }).catch(() => {});
  record('flags: deleted', page.url().endsWith('#/flags') && !(await homeFlag('E2eFlag')), page.url());

  // Errors the API was meant to answer with (the 400 of the wrong percentage) are not page errors.
  const unexpected = errors.filter((e) => !/^400 .*\/SaveFeatureFlag$/.test(e));
  record('flags: no errors', unexpected.length === 0, unexpected.join(' | '));
  await context.close();
}

await browser.close();
const failed = results.filter((r) => !r.ok);
for (const r of results) console.log(`${r.ok ? 'ok  ' : 'FAIL'} ${r.name}${r.detail ? ` — ${r.detail}` : ''}`);
console.log(`\n${which} ${base}: ${results.length - failed.length}/${results.length} ok`);
process.exit(failed.length ? 1 : 0);
