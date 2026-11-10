import { expect, test } from '@playwright/test';

for (const width of [1440, 1024, 768, 390, 320]) {
  test(`authentication layout and branding at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    const errors: string[] = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto('/login');
    const logo = page.getByRole('img', { name: 'Логотип Дальреммаш' });
    await expect(logo).toBeVisible();
    await expect.poll(() => logo.evaluate((image: HTMLImageElement) => image.complete && image.naturalWidth > 0)).toBeTruthy();
    await expect(page.getByText(/КУБ/i)).toHaveCount(0);
    await page.getByRole('button', { name: 'Войти', exact: true }).click();
    await expect(page.getByText('Введите логин', { exact: true })).toBeVisible();
    await expect(page.getByText('Введите пароль', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Заявка на регистрацию', exact: true }).click();
    await expect(page.getByRole('textbox', { name: 'Организация', exact: true })).toBeVisible();
    await expect(page.getByRole('img', { name: 'Логотип Дальреммаш' })).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
    await page.getByRole('button', { name: 'Вернуться ко входу', exact: true }).click();
    await page.getByRole('button', { name: 'Забыли пароль?', exact: true }).click();
    await expect(page.getByRole('textbox', { name: 'Email', exact: true })).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
    expect(errors).toEqual([]);
  });
}
