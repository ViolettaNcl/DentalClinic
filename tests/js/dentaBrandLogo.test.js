import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);

async function source(path) {
    return readFile(new URL(path, root), 'utf8');
}

test('Denta brand stylesheet is loaded after the base chat styles', async () => {
    const globalCss = await source('wwwroot/assets/css/global.css');
    const baseIndex = globalCss.indexOf("components/chat-bot.css");
    const brandIndex = globalCss.indexOf("components/denta-brand.css");

    assert.ok(baseIndex >= 0);
    assert.ok(brandIndex > baseIndex);
});

test('Denta launcher and header use the dedicated full logo asset', async () => {
    const brandCss = await source('wwwroot/assets/css/components/denta-brand.css');
    const logo = await source('wwwroot/assets/images/denta-logo.svg');

    assert.match(brandCss, /chat-toggle::after[\s\S]*denta-logo\.svg/);
    assert.match(brandCss, /\.chat-avatar[\s\S]*denta-logo\.svg/);
    assert.match(logo, /Denta AI assistant logo/);
});

test('bot replies use the dedicated mascot avatar without a wordmark', async () => {
    const brandCss = await source('wwwroot/assets/css/components/denta-brand.css');
    const avatar = await source('wwwroot/assets/images/denta-avatar.svg');
    const chatBot = await source('wwwroot/assets/js/core/chatBot.js');

    assert.match(brandCss, /\.chat-bubble--bot\s+\.chat-bubble-avatar[\s\S]*denta-avatar\.svg/);
    assert.match(avatar, /Denta reply avatar/);
    assert.doesNotMatch(avatar, />\s*Denta\s*</i);

    const toothFallbacks = chatBot.match(/class="chat-bubble-avatar">🦷<\/span>/g) || [];
    assert.ok(toothFallbacks.length >= 2);
});
