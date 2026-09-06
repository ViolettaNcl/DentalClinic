import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile, stat } from 'node:fs/promises';

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

test('Denta launcher and header use the dedicated logo asset', async () => {
    const brandCss = await source('wwwroot/assets/css/components/denta-brand.css');
    const logo = await source('wwwroot/assets/images/denta-logo.svg');

    assert.match(brandCss, /chat-toggle::after[\s\S]*denta-logo\.svg/);
    assert.match(brandCss, /\.chat-avatar[\s\S]*denta-logo\.svg/);
    assert.match(logo, /Denta AI assistant logo/);
});

test('bot replies use the tooth-only Denta mascot asset', async () => {
    const chatBot = await source('wwwroot/assets/js/core/chatBot.js');
    const brandCss = await source('wwwroot/assets/css/components/denta-brand.css');
    const mascot = await stat(new URL('wwwroot/assets/images/denta-reply-avatar.png', root));

    // Keep the text fallback in both streaming and non-streaming paths, while the
    // branding stylesheet visually replaces it with the new tooth-only mascot.
    const fallbackAvatars = chatBot.match(/class="chat-bubble-avatar">🦷<\/span>/g) || [];
    assert.ok(fallbackAvatars.length >= 2);
    assert.match(brandCss, /\.chat-bubble-avatar[\s\S]*denta-reply-avatar\.png/);
    assert.match(brandCss, /\.chat-bubble-avatar[\s\S]*font-size:\s*0/);
    assert.ok(mascot.size > 1000, 'reply mascot asset should be a real image, not an empty placeholder');
});
