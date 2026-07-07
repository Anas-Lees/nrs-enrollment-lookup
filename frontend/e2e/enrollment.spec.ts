import { test, expect } from '@playwright/test';

test.describe('Enrollment — create and queue', () => {
  test('the queue screen loads', async ({ page }) => {
    await page.goto('/enrollment/queue');
    await expect(page.getByRole('heading', { name: 'My Queue' })).toBeVisible();
  });

  test('operator creates a new enrollment and it appears in the queue', async ({ page }) => {
    await page.goto('/enrollment/new');
    await expect(page.getByRole('heading', { name: 'New Enrollment' })).toBeVisible();

    // Unique family name so we can find this run's enrollment in the queue.
    const family = `Playwright${Date.now().toString().slice(-6)}`;

    await page.locator('#firstNameEn').fill('Test');
    await page.locator('#familyNameEn').fill(family);
    await page.locator('#firstNameAr').fill('اختبار');
    await page.locator('#familyNameAr').fill('تجريبي');
    await page.locator('#gender').selectOption('M');

    // Full-record capture: the biographic, address and contact essentials are required.
    await page.locator('#placeOfBirthEn').fill('Muscat');
    await page.locator('#placeOfBirthAr').fill('مسقط');
    await page.locator('#motherNameEn').fill('Fatma Al-Saadi');
    await page.locator('#motherNameAr').fill('فاطمة السعدي');
    await page.locator('#governorate').selectOption('Muscat');
    await page.locator('#wilayat').fill('Seeb');
    await page.locator('#mobile').fill('+96891234567');

    // Custom date picker: open, step back one month (a guaranteed past date), pick the 15th.
    await page.locator('.df__input').click();
    await page.locator('.df__navbtn[aria-label="previous month"]').click();
    await page.locator('.df__pop').getByText('15', { exact: true }).click();

    await page.locator('button[type="submit"]').click();

    // Submitting navigates to the queue, where the new enrollment is listed.
    await expect(page).toHaveURL(/\/enrollment\/queue/);
    await expect(page.locator('.q-list')).toContainText(family);
  });
});
