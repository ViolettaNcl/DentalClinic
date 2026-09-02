import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test('production health and canonical HTTPS are healthy', async ({ page, request }) => {
  const health = await request.get('/health');
  expect(health.ok()).toBeTruthy();
  const body = await health.json();
  expect(body.status).toBe('Healthy');

  await page.goto('/');
  await expect(page.locator('link[rel="canonical"]'))
    .toHaveAttribute('href', 'https://dental-clinic-vn.vercel.app/');
});

test('public UI has accessible names for visible controls', async ({ page }) => {
  await page.goto('/');
  await page.waitForLoadState('domcontentloaded');

  const controls = page.locator('button:visible,input:visible,textarea:visible,select:visible');
  const count = await controls.count();
  for (let i = 0; i < count; i++) {
    const el = controls.nth(i);
    const name = (await el.getAttribute('aria-label'))
      || (await el.getAttribute('title'))
      || (await el.getAttribute('placeholder'))
      || (await el.textContent())
      || '';
    expect(name.trim().length, `visible control ${i} should have an accessible label`).toBeGreaterThan(0);
  }
});

test('home has no serious or critical automated accessibility violations', async ({ page }) => {
  await page.goto('/');
  await page.waitForLoadState('networkidle');

  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();

  const blocking = results.violations.filter(v => v.impact === 'serious' || v.impact === 'critical');
  expect(blocking, JSON.stringify(blocking, null, 2)).toEqual([]);
});

test('mobile home does not create page-level horizontal overflow', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'mobile', 'mobile-only');
  await page.goto('/');
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(2);
});

test('all supported localization dictionaries are available', async ({ request }) => {
  for (const lang of ['ru', 'en', 'fr', 'el', 'ar']) {
    const res = await request.get(`/assets/i18n/${lang}.json`);
    expect(res.ok(), `${lang}.json`).toBeTruthy();
    const data = await res.json();
    expect(Object.keys(data).length).toBeGreaterThan(10);
  }
});

test('language switcher applies all supported languages and Arabic RTL', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('#lang-btn')).toBeVisible();

  for (const lang of ['en', 'fr', 'el', 'ar', 'ru']) {
    await page.locator('#lang-btn').click();
    await page.locator(`#lang-dropdown .lang-option[data-lang="${lang}"]`).click();
    await expect(page.locator('html')).toHaveAttribute('lang', lang);
    await expect(page.locator('html')).toHaveAttribute('dir', lang === 'ar' ? 'rtl' : 'ltr');
  }
});

test('restored tooth chart assets are published', async ({ request }) => {
  for (const path of [
    '/assets/css/components/tooth-chart.css',
    '/assets/js/managers/public/toothChart.js'
  ]) {
    const response = await request.get(path);
    expect(response.ok(), path).toBeTruthy();
  }
});

test('chat widget can open', async ({ page }) => {
  await page.goto('/');
  const toggle = page.locator('#chat-toggle');
  await expect(toggle).toBeVisible();
  await toggle.click();
  await expect(page.locator('#chat-widget')).toBeVisible();
});
