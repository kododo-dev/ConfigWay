// Thorough browser scenarios of the feature flags view, against the running demo (its flags come from appsettings):
//   node flags.mjs http://localhost:5089 [chrome|msedge|firefox|webkit]
// Needs the playwright package (npm i --no-save playwright). Every scenario puts the flags it changes back.
import { chromium, firefox, webkit } from 'playwright';

const base = process.argv[2].replace(/\/$/, '');
const which = process.argv[3] ?? 'chrome';
const browser = which === 'firefox' ? await firefox.launch() : which === 'webkit' ? await webkit.launch() : await chromium.launch({ channel: which });
const results = [];
const record = (name, ok, detail = '') => results.push({ name, ok, detail });
const visible = (l, timeout = 10000) => l.first().waitFor({ timeout }).then(() => true, () => false);
const gone = (l, timeout = 5000) => l.first().waitFor({ state: 'detached', timeout }).then(() => true, () => false);

// ── API and the demo's home page ─────────────────────────────────────────────

const api = async (request, body = {}) => {
  const response = await fetch(`${base}/config/api/${request}`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
  });
  return response.status === 204 ? null : response.json();
};
const flagItem = async (id) => (await api('GetFeatureFlags')).flags.find((f) => f.id === id);
const resetAll = async () => {
  for (const item of (await api('GetFeatureFlags')).flags)
    if (item.hasOverride) await api('ResetFeatureFlag', { id: item.id });
};
/** The flag as the demo's home page shows it: { on, variant }, or null when the page does not list it. */
const home = async (name, user = '') => {
  const html = await (await fetch(`${base}/?user=${user}`)).text();
  const match = new RegExp(`>${name.replace(/[.*+?^${}()|[\]\\/]/g, '\\$&')}</td><td>(<span[^>]*>[^<]*</span>)(?: <span[^>]*>· ([^<]*)</span>)?`).exec(html);
  return match ? { on: match[1].includes('✓ on'), variant: match[2] ?? null } : null;
};

// ── Browser ──────────────────────────────────────────────────────────────────

async function open(options = {}) {
  const context = await browser.newContext({ locale: 'en-US', colorScheme: 'light', viewport: { width: 1280, height: 900 }, ...options });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  page.on('console', (m) => { if (m.type() === 'error' && !m.text().startsWith('Failed to load resource')) errors.push(m.text()); });
  page.on('response', (r) => {
    // The 400s are the answers the scenarios ask for; anything else is an error.
    if (r.status() >= 400 && !r.url().endsWith('/favicon.ico') && !(r.status() === 400 && r.url().endsWith('/SaveFeatureFlag')))
      errors.push(`${r.status()} ${r.url()}`);
  });
  return { context, page, errors };
}

const flagsLoaded = (page) => page.waitForResponse((r) => r.url().endsWith('/config/api/GetFeatureFlags'));
const save = async (page) => {
  await Promise.all([flagsLoaded(page), page.getByRole('button', { name: 'Save' }).click()]);
};
const openFlag = async (page, id) => {
  await page.goto(`${base}/config/#/flag/${encodeURIComponent(id)}`);
  await page.getByRole('heading', { name: id, exact: true }).waitFor();
};
const check = async (page, user, groups = '') => {
  await page.getByRole('textbox', { name: 'User', exact: true }).fill(user);
  await page.getByLabel('Groups (comma-separated)').fill(groups);
  // Editing the user clears the last result, so the one that shows up is the answer to this check.
  const result = page.getByTestId('flag-check-result');
  await gone(result);
  await page.getByRole('button', { name: 'Check', exact: true }).click();
  await result.waitFor();
  return (await result.textContent()).includes('On for this user');
};
const addChip = async (page, label, value) => {
  const input = page.getByRole('combobox', { name: label, exact: true });
  await input.click();
  await input.fill(value);
  await input.press('Enter');
};
const pad = (n) => String(n).padStart(2, '0');
const localInput = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;

await resetAll();

// ── Navigation ───────────────────────────────────────────────────────────────
{
  const { context, page, errors } = await open();

  await page.goto(`${base}/config/#/flag/NewCheckout`);
  record('deep link to a flag on a fresh load', await visible(page.getByRole('heading', { name: 'NewCheckout', exact: true })));

  await page.goto(`${base}/config/#/flag/Nope`);
  await page.waitForURL(/#\/flags$/, { timeout: 5000 }).catch(() => {});
  record('unknown flag goes to the list', page.url().endsWith('#/flags'), page.url());

  await page.getByPlaceholder('Search flags…').fill('check');
  const rows = page.locator('[data-testid^="flag-row-"]');
  await gone(page.getByTestId('flag-row-BetaBanner'));
  record('search by id', (await rows.count()) === 1 && await visible(page.getByTestId('flag-row-NewCheckout')), String(await rows.count()));
  await page.getByPlaceholder('Search flags…').fill('festive');
  record('search by description', await visible(page.getByTestId('flag-row-HolidayTheme')) && (await rows.count()) === 1);
  await page.getByPlaceholder('Search flags…').fill('');

  record('navigation: no errors', errors.length === 0, errors.join(' | '));
  await context.close();
}

// ── Time window: local times in, UTC stored, the same local times back ─────────
{
  const { context, page, errors } = await open();
  await openFlag(page, 'HolidayTheme');
  record('time window: not started yet', await visible(page.getByText('not started yet')));

  const start = new Date(); start.setDate(start.getDate() - 1); start.setHours(10, 30, 0, 0);
  const end = new Date(); end.setFullYear(end.getFullYear() + 1); end.setHours(18, 45, 0, 0);
  await page.getByLabel('Start').fill(localInput(start));
  await page.getByLabel('End').fill(localInput(end));
  record('time window: active now', await visible(page.getByText('active now')));
  await save(page);

  const stored = (await flagItem('HolidayTheme')).flag.filters[0].parameters;
  record('time window: stored in UTC', new Date(stored.Start).getTime() === start.getTime() && new Date(stored.End).getTime() === end.getTime(),
    `${stored.Start} / ${stored.End}`);
  record('time window: the app sees it on', (await home('HolidayTheme'))?.on === true);

  await page.reload();
  await page.getByLabel('Start').waitFor();
  record('time window: the same local times after a reload',
    (await page.getByLabel('Start').inputValue()) === localInput(start) && (await page.getByLabel('End').inputValue()) === localInput(end),
    `${await page.getByLabel('Start').inputValue()} / ${await page.getByLabel('End').inputValue()}`);

  await page.getByLabel('End').fill('');
  await save(page);
  const open_ = (await flagItem('HolidayTheme')).flag.filters[0].parameters;
  record('time window: open end', open_.End === undefined && (await home('HolidayTheme'))?.on === true, JSON.stringify(open_));

  await page.getByLabel('Start').fill('');
  await page.getByRole('button', { name: 'Save' }).click();
  record('time window: needs a start or an end', await visible(page.getByText('The time window needs a start, an end or both.')));

  await api('ResetFeatureFlag', { id: 'HolidayTheme' });
  record('time window: no errors', errors.length === 0, errors.join(' | '));
  await context.close();
}

// ── Targeting: users, groups and exclusions ───────────────────────────────────
{
  const { context, page, errors } = await open();
  await openFlag(page, 'NewCheckout');

  await addChip(page, 'Users', 'carol');
  await page.getByRole('button', { name: 'alice' }).locator('.MuiChip-deleteIcon').click();
  await page.getByRole('button', { name: 'Add group' }).click();
  await page.getByLabel('Group', { exact: true }).last().fill('qa');
  await page.getByRole('spinbutton', { name: '% of the group' }).last().fill('100');
  await addChip(page, 'Excluded groups', 'contractors');
  await save(page);

  const audience = (await flagItem('NewCheckout')).flag.filters[0].parameters.Audience;
  record('targeting: stored', JSON.stringify(audience.Users) === '["carol"]'
    && audience.Groups.map((g) => `${g.Name}:${g.RolloutPercentage}`).join() === 'beta-testers:100,qa:100'
    && JSON.stringify(audience.Exclusion.Groups) === '["contractors"]', JSON.stringify(audience));

  record('targeting: added user is on', await check(page, 'carol'));
  record('targeting: removed user is off', !(await check(page, 'alice')) && (await home('NewCheckout', 'alice'))?.on === false);
  record('targeting: added group is on', await check(page, 'dave', 'qa'));
  record('targeting: excluded group wins', !(await check(page, 'erin', 'qa, contractors')));
  record('targeting: excluded user wins', !(await check(page, 'mallory', 'beta-testers')));

  await page.getByRole('button', { name: 'Reset to configuration' }).click();
  await Promise.all([flagsLoaded(page), page.getByRole('dialog').getByRole('button', { name: 'Reset to configuration' }).click()]);
  record('targeting: reset brings alice back', await visible(page.getByRole('button', { name: 'alice' })) && (await home('NewCheckout', 'alice'))?.on === true);
  record('targeting: no errors', errors.length === 0, errors.join(' | '));
  await context.close();
}

// ── Any or all of two conditions ──────────────────────────────────────────────
{
  const { context, page, errors } = await open();
  await openFlag(page, 'BetaBanner');

  await page.getByRole('button', { name: 'Add condition' }).click();
  await page.getByRole('menuitem', { name: 'Targeting' }).click();
  await addChip(page, 'Users', 'bob');
  await page.getByRole('button', { name: 'Add condition' }).click();
  await page.getByRole('menuitem', { name: 'Time window' }).click();
  const future = new Date(); future.setFullYear(future.getFullYear() + 2); future.setHours(12, 0, 0, 0);
  await page.getByLabel('Start').fill(localInput(future));
  await save(page);
  record('any: one condition is enough', (await home('BetaBanner', 'bob'))?.on === true && (await home('BetaBanner'))?.on === false);

  await page.getByRole('button', { name: 'all', exact: true }).click();
  await save(page);
  record('all: every condition is needed', (await home('BetaBanner', 'bob'))?.on === false
    && (await flagItem('BetaBanner')).flag.requirementType === 'All');
  record('all: shown in the list', (await (await fetch(`${base}/config/api/GetFeatureFlags`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' })).json())
    .flags.find((f) => f.id === 'BetaBanner').flag.filters.length === 2);

  await page.getByRole('button', { name: 'Remove condition' }).first().click();
  await page.getByRole('button', { name: 'Remove condition' }).first().click();
  record('no conditions: on for everyone', await visible(page.getByText('No conditions: when enabled')));
  await save(page);
  record('no conditions: saved', (await home('BetaBanner'))?.on === true && (await flagItem('BetaBanner')).flag.filters.length === 0);

  await api('ResetFeatureFlag', { id: 'BetaBanner' });
  record('any/all: no errors', errors.length === 0, errors.join(' | '));
  await context.close();
}

// ── A new flag: odd id, custom filter, duplicate id ───────────────────────────
{
  const { context, page, errors } = await open();
  const id = 'Team.Billing/V2';

  await page.goto(`${base}/config/#/flags/new`);
  await page.getByTestId('flag-id').fill(id);
  await page.getByRole('button', { name: 'Add condition' }).click();
  await page.getByRole('menuitem', { name: 'Custom filter' }).click();
  await page.getByRole('combobox', { name: 'Filter name' }).fill('Acme.Browser');
  record('custom filter: warns about an unregistered filter', await visible(page.getByText('has not registered a filter with this name')));
  await page.getByLabel('Parameters (JSON)').fill('{ "Allowed": [');
  record('custom filter: invalid JSON blocks the save',
    await visible(page.getByText('Not a valid JSON object.')) && await page.getByRole('button', { name: 'Save' }).isDisabled());
  await page.getByLabel('Parameters (JSON)').fill('{ "Allowed": ["Edge"] }');
  await page.getByRole('button', { name: 'Save' }).click();
  record('custom filter: unregistered filter refused on the condition', await visible(page.getByText("has not registered a filter named 'Acme.Browser'")));
  record('custom filter: nothing saved', (await flagItem(id)) === undefined);

  await page.getByRole('button', { name: 'Remove condition' }).click();
  await page.getByRole('button', { name: 'Add condition' }).click();
  await page.getByRole('menuitem', { name: 'Percentage' }).click();
  await page.getByLabel('%', { exact: true }).fill('0');
  await save(page);
  await page.waitForURL(/#\/flag\/Team\.Billing%2FV2$/, { timeout: 5000 }).catch(() => {});
  record('odd id: saved and opened', page.url().endsWith('#/flag/Team.Billing%2FV2') && (await home(id))?.on === false, page.url());
  await page.reload();
  record('odd id: deep link', await visible(page.getByRole('heading', { name: id, exact: true })));

  await page.goto(`${base}/config/#/flags/new`);
  await page.getByTestId('flag-id').fill('betabanner');
  await page.getByRole('button', { name: 'Save' }).click();
  record('duplicate id ignoring case', await visible(page.getByText("A flag named 'betabanner' already exists.")));
  await page.getByTestId('flag-id').fill('Bad:Id');
  await page.getByRole('button', { name: 'Save' }).click();
  record('id with a colon', await visible(page.getByText("The id cannot contain ':'.")));

  await api('ResetFeatureFlag', { id });
  record('new flag: no errors', errors.length === 0, errors.join(' | '));
  await context.close();
}

// ── Discard, the .NET schema and variants ─────────────────────────────────────
{
  const { context, page, errors } = await open();

  await openFlag(page, 'MaintenanceMode');
  await page.getByLabel('Enabled').click();
  await page.getByLabel('Description').fill('changed');
  await page.getByRole('button', { name: 'Discard' }).click();
  record('discard', !(await page.getByLabel('Enabled').isChecked())
    && (await page.getByLabel('Description').inputValue()) === 'Short-circuits the site into a maintenance page.'
    && await page.getByRole('button', { name: 'Save' }).isDisabled());

  await page.goto(`${base}/config/#/flags`);
  await Promise.all([flagsLoaded(page), page.getByRole('switch', { name: 'ReadOnlyMode' }).click()]);
  record('.NET schema: overridden from the list', (await home('ReadOnlyMode'))?.on === true);
  await openFlag(page, 'ReadOnlyMode');
  record('.NET schema: explained in the editor', await visible(page.getByText('older .NET schema')));
  await page.getByRole('button', { name: 'Reset to configuration' }).click();
  await Promise.all([flagsLoaded(page), page.getByRole('dialog').getByRole('button', { name: 'Reset to configuration' }).click()]);
  record('.NET schema: reset', (await home('ReadOnlyMode'))?.on === false);

  record('variants: before', (await home('SearchEngine', 'alice'))?.variant === 'Semantic' && (await home('SearchEngine', 'bob'))?.variant === 'Classic');
  await openFlag(page, 'SearchEngine');
  record('variants: explained in the editor', await visible(page.getByText('This flag has variants')));
  await page.getByLabel('Description').fill('Which search engine answers.');
  await save(page);
  record('variants: kept after a save', (await home('SearchEngine', 'alice'))?.variant === 'Semantic');
  record('variants: tester shows the variant', (await check(page, 'alice')) && await visible(page.getByText('variant Semantic')));

  await api('ResetFeatureFlag', { id: 'SearchEngine' });
  record('discard/.NET/variants: no errors', errors.length === 0, errors.join(' | '));
  await context.close();
}

// ── Saving settings keeps the flags ───────────────────────────────────────────
{
  const { context, page, errors } = await open();
  await page.goto(`${base}/config/#/flags`);
  await Promise.all([flagsLoaded(page), page.getByRole('switch', { name: 'BetaBanner' }).click()]);

  await page.getByTestId('way-menu').getByText('Branding').click();
  await page.locator('main textarea').first().waitFor();
  const index = await page.locator('main textarea').evaluateAll((els) => els.findIndex((e) => e.value === 'Acme Corp'));
  const field = page.locator('main textarea').nth(index);
  await field.fill('Acme Flags');
  await Promise.all([
    page.waitForResponse((r) => r.url().endsWith('/config/api/UpdateConfiguration')),
    page.waitForResponse((r) => r.url().endsWith('/config/api/GetConfiguration')),
    page.getByRole('button', { name: 'Save' }).click(),
  ]);
  record('settings save keeps the flag overrides', (await home('BetaBanner'))?.on === false && (await flagItem('BetaBanner')).hasOverride);
  await field.fill('Acme Corp');
  await Promise.all([
    page.waitForResponse((r) => r.url().endsWith('/config/api/GetConfiguration')),
    page.getByRole('button', { name: 'Save' }).click(),
  ]);

  await api('ResetFeatureFlag', { id: 'BetaBanner' });
  record('settings: no errors', errors.length === 0, errors.join(' | '));
  await context.close();
}

// ── Phone and Polish ──────────────────────────────────────────────────────────
{
  const { context, page, errors } = await open({ viewport: { width: 390, height: 800 } });
  const fits = () => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1);

  await page.goto(`${base}/config/#/flags`);
  await page.getByTestId('flag-row-NewCheckout').waitFor();
  record('phone: list fits', await fits());
  await page.getByTestId('flag-row-NewCheckout').click();
  await page.getByRole('heading', { name: 'NewCheckout', exact: true }).waitFor();
  record('phone: editor fits', await fits());
  record('phone: no errors', errors.length === 0, errors.join(' | '));
  await context.close();
}
{
  const { context, page, errors } = await open({ locale: 'pl-PL' });
  await page.goto(`${base}/config/#/flags`);
  record('polish: list', await visible(page.getByRole('heading', { name: 'Flagi funkcji' })) && await visible(page.getByRole('button', { name: 'Nowa flaga' })));
  await page.getByTestId('flag-row-NewCheckout').click();
  record('polish: editor', await visible(page.getByText('Targetowanie')) && await visible(page.getByText('Sprawdź dla użytkownika')));
  record('polish: no errors', errors.length === 0, errors.join(' | '));
  await context.close();
}

await resetAll();
await browser.close();
const failed = results.filter((r) => !r.ok);
for (const r of results) console.log(`${r.ok ? 'ok  ' : 'FAIL'} ${r.name}${r.detail ? ` — ${r.detail}` : ''}`);
console.log(`\n${which} ${base}: ${results.length - failed.length}/${results.length} ok`);
process.exit(failed.length ? 1 : 0);
