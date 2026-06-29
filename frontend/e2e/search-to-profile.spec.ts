import { test, expect } from '@playwright/test';

test.describe('Applicant Lookup — operator journey', () => {
  test('search, open a result, and see the profile with documents', async ({ page }) => {
    await page.goto('/search');

    // The smart search bar is present.
    await expect(page.locator('.searchbar')).toBeVisible();

    // Run a search with no filters (returns the full, paged list).
    await page.locator('form button[type="submit"]').click();

    // Result cards render with at least one card.
    const firstCard = page.locator('.cards .card').first();
    await expect(firstCard).toBeVisible();

    // Grab the CRN from the card, then select it to populate the quick preview.
    const crn = (await firstCard.locator('.card__crn').innerText()).trim();
    expect(crn).not.toEqual('');
    await firstCard.click();
    await expect(page.locator('.preview__name')).toBeVisible();

    // Open the full profile from the preview CTA.
    await page.locator('.preview a.btn-primary').click();

    // We navigated to the profile route for that CRN.
    await expect(page).toHaveURL(new RegExp(`/persons/${crn}$`));

    // Profile shows a name heading and the documents section with rows.
    await expect(page.locator('.summary-card h1')).toBeVisible();
    await expect(page.locator('.documents')).toBeVisible();
    await expect(page.locator('.doc-table tbody tr').first()).toBeVisible();
  });

  test('filtering by nationality returns only matching cards', async ({ page }) => {
    await page.goto('/search');
    await page.locator('select#nationality').selectOption('OMN');
    await page.locator('form button[type="submit"]').click();

    const cards = page.locator('.cards .card');
    await expect(cards.first()).toBeVisible();

    // Every visible card's facts line names the nationality "Oman".
    const count = await cards.count();
    for (let i = 0; i < count; i++) {
      await expect(cards.nth(i).locator('.card__facts')).toContainText('Oman');
    }
  });

  test('the search console shows data immediately, with no search run', async ({ page }) => {
    await page.goto('/search');
    // No filters typed, no submit — the first page of results loads on its own.
    await expect(page.locator('.cards .card').first()).toBeVisible();
    await expect(page.locator('.results-meta__count')).toContainText('matches');
  });

  test('Start New Enrollment from a profile opens the placeholder for that CRN', async ({
    page,
  }) => {
    await page.goto('/persons/63498452');
    await expect(page.locator('.summary-card h1')).toBeVisible();
    await page.locator('.enroll-cta').click();
    await expect(page).toHaveURL(/\/enrollment\/new\?crn=63498452$/);
    await expect(page.locator('.enroll__for')).toContainText('63498452');
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
