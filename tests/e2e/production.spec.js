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

test('all sitemap public routes return HTML with matching canonical URLs', async ({ request }) => {
  const sitemap = await request.get('/sitemap.xml');
  expect(sitemap.ok()).toBeTruthy();
  const xml = await sitemap.text();

  const urls = [...xml.matchAll(/<loc>([^<]+)<\/loc>/g)].map(match => match[1]);
  expect(urls.length, 'sitemap should contain the complete public route set').toBeGreaterThanOrEqual(13);

  for (const absoluteUrl of urls) {
    const url = new URL(absoluteUrl);
    expect(url.origin).toBe('https://dental-clinic-vn.vercel.app');

    const response = await request.get(`${url.pathname}${url.search}`);
    expect(response.ok(), `${url.pathname} should return 2xx`).toBeTruthy();
    expect(response.headers()['content-type'] || '', `${url.pathname} should return HTML`).toContain('text/html');

    const html = await response.text();
    const escaped = absoluteUrl.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    expect(html, `${url.pathname} should publish its canonical URL`)
      .toMatch(new RegExp(`<link\\s+rel=["']canonical["']\\s+href=["']${escaped}["']`, 'i'));
  }
});

test('home loads without browser page errors', async ({ page }) => {
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto('/');
  await page.waitForLoadState('networkidle');
  expect(errors, errors.join('\n')).toEqual([]);
});

test('public UI has accessible names for visible controls', async ({ page }) => {
  await page.goto('/');
  await page.waitForLoadState('networkidle');

  // Snapshot the currently visible controls in one browser-side pass. The page
  // injects header/chat/review controls dynamically, so iterating a live :visible
  // locator by count/nth can race with DOM/visibility changes and time out on an
  // index that no longer exists. This still validates the same accessibility
  // requirement, while reporting concrete details if a real unnamed control exists.
  const unnamed = await page.evaluate(() => {
    const controls = [...document.querySelectorAll('button,input,textarea,select')]
      .filter(el => {
        const style = getComputedStyle(el);
        const rect = el.getBoundingClientRect();
        return style.display !== 'none'
          && style.visibility !== 'hidden'
          && rect.width > 0
          && rect.height > 0;
      });

    return controls
      .map((el, index) => {
        const name = el.getAttribute('aria-label')
          || el.getAttribute('title')
          || el.getAttribute('placeholder')
          || el.textContent
          || '';

        if (name.trim()) return null;
        return {
          index,
          tag: el.tagName.toLowerCase(),
          id: el.id || null,
          className: typeof el.className === 'string' ? el.className : null,
        };
      })
      .filter(Boolean);
  });

  expect(unnamed, JSON.stringify(unnamed, null, 2)).toEqual([]);
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

  // The launcher intentionally has a continuous floating transform animation.
  // Playwright's normal click waits for geometric stability and can therefore
  // time out on mobile even though a real tap works. Dispatch the click event
  // directly, then assert the same application-visible result.
  await toggle.dispatchEvent('click');
  await expect(page.locator('#chat-window')).toHaveClass(/chat-window--visible/);
});
