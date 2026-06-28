import { test, expect } from '@playwright/test';

test.describe('Applicant Lookup — operator journey', () => {
  test('search, open a result, and see the profile with documents', async ({ page }) => {
    await page.goto('/search');

    // The search form is present.
    await expect(page.locator('form.search-form')).toBeVisible();

    // Run a search with no filters (returns the full, paged list).
    await page.locator('form button[type="submit"]').click();

    // Results table renders with at least one row.
    const firstRow = page.locator('table.results tbody tr').first();
    await expect(firstRow).toBeVisible();

    // Grab the CRN from the first cell, then open that person's profile.
    const crn = (await firstRow.locator('td').first().innerText()).trim();
    expect(crn).not.toEqual('');
    await firstRow.click();

    // We navigated to the profile route for that CRN.
    await expect(page).toHaveURL(new RegExp(`/persons/${crn}$`));

    // Profile shows a name heading and the documents section with rows.
    await expect(page.locator('.card h1')).toBeVisible();
    await expect(page.locator('.documents')).toBeVisible();
    await expect(page.locator('.doc-table tbody tr').first()).toBeVisible();
  });

  test('filtering by nationality returns only matching rows', async ({ page }) => {
    await page.goto('/search');
    await page.locator('select#nationality').selectOption('OMN');
    await page.locator('form button[type="submit"]').click();

    const rows = page.locator('table.results tbody tr');
    await expect(rows.first()).toBeVisible();

    // Every visible row's nationality cell reads "Oman" (pipe-translated OMN).
    const count = await rows.count();
    for (let i = 0; i < count; i++) {
      await expect(rows.nth(i).locator('td').nth(5)).toHaveText('Oman');
    }
  });

  test('language toggle switches the UI to Arabic and flips to RTL', async ({ page }) => {
    await page.goto('/search');

    // Toggle to Arabic (the button shows the language you switch TO).
    await page.getByRole('button', { name: 'العربية' }).click();

    // The document direction flips to RTL and the search button is now Arabic.
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.locator('form button[type="submit"]')).toHaveText('بحث');
  });
});
