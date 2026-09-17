import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';

const ROOT = process.cwd();
const read = rel => fs.readFileSync(path.join(ROOT, rel), 'utf8');

test('service detail pricing cards keep header, price and CTA rows aligned', () => {
    const css = read('wwwroot/assets/css/services/services-all.css');
    assert.match(css, /CONSISTENT SERVICE PRICING CARDS/);
    assert.match(css, /\.service-detail-page \.card--pricing \.card__header\s*\{[\s\S]*min-height:\s*136px/);
    assert.match(css, /\.service-detail-page \.card--pricing \.card__price\s*\{[\s\S]*min-height:\s*108px/);
    assert.match(css, /\.service-detail-page \.card--pricing \.card__price \.amount\s*\{[\s\S]*white-space:\s*nowrap/);
    assert.match(css, /\.service-detail-page \.card--pricing > \.btn-primary\s*\{[\s\S]*margin-top:\s*auto/);
});

test('all service detail pages use the shared pricing grid contract', () => {
    const serviceDir = path.join(ROOT, 'wwwroot/pages/services');
    const pages = fs.readdirSync(serviceDir).filter(name => name.endsWith('.html'));
    for (const page of pages) {
        const html = fs.readFileSync(path.join(serviceDir, page), 'utf8');
        if (!html.includes('card--pricing')) continue;
        assert.match(html, /class="[^"]*service-detail-page[^"]*"/, `${page} must be scoped as a service-detail page`);
        assert.match(html, /class="[^"]*card-grid--pricing[^"]*"/, `${page} must use shared pricing grid`);
    }
});

test('mobile public header anchors hamburger at right with language immediately to its left', () => {
    const css = read('wwwroot/assets/css/global.css');
    assert.match(css, /MOBILE HEADER CONTROL PAIR/);
    assert.match(css, /\.hamburger\s*\{[^}]*position:\s*absolute;[^}]*right:\s*10px;/s);
    assert.match(css, /\.lang-switcher\s*\{[^}]*position:\s*absolute;[^}]*right:\s*58px;/s);
});

test('cosmetic pricing translations do not expose raw br markup', () => {
    for (const lang of ['ru','en','fr','el','ar']) {
        const data = JSON.parse(read(`wwwroot/assets/i18n/${lang}.json`));
        for (const key of ['cos_p1_name','cos_p2_name','cos_p4_name']) {
            assert.doesNotMatch(data[key] || '', /<br\s*\/?\s*>/i, `${lang}:${key}`);
        }
    }
});

test('obsolete root fix reports are removed from the clean distribution', () => {
    const obsolete = [
        'DENTA_UPGRADE_MANIFEST.txt','AI_MAP_3D_SMILE_V8.txt','DASHBOARD_RELIABILITY_FIX_V4.txt',
        'SAFE_VISUAL_PORT_REPORT.md','UX_DOCTOR_MAP_MOBILE_V6.txt','SMART_MAP_INPAGE_V7.txt',
        'DASHBOARD_DATABASE_RELIABILITY_V5.txt','SAFE_AUTH_DB_INTEGRITY_AFTER_UI_FIX.json',
        'SAFE_AUTH_DB_INTEGRITY.json','DENTA_LINK_FIX_V3.txt','DASHBOARD_UI_FIX_REPORT.md'
    ];
    for (const rel of obsolete) assert.equal(fs.existsSync(path.join(ROOT, rel)), false, rel);
});
