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

test('bot replies use the generated Denta operator portrait in both render paths', async () => {
    const chatBot = await source('wwwroot/assets/js/core/chatBot.js');
    const brandCss = await source('wwwroot/assets/css/components/denta-brand.css');
    const avatarModule = await source('wwwroot/assets/js/core/dentaReplyAvatar.js');
    const main = await source('wwwroot/assets/js/main.js');
    const portrait = await stat(new URL('wwwroot/assets/images/denta-reply-avatar.png', root));

    // ChatBot still carries the lightweight emoji fallback in both streaming and
    // non-streaming replies. Branding upgrades those exact slots to a real image.
    const fallbackAvatars = chatBot.match(/class="chat-bubble-avatar">🦷<\/span>/g) || [];
    assert.ok(fallbackAvatars.length >= 2, 'streaming and fallback replies must both expose an avatar slot');

    assert.match(brandCss, /\.chat-bubble--bot\s+\.chat-bubble-avatar[\s\S]*denta-reply-avatar\.png/);
    assert.match(brandCss, /min-width:\s*38px/);
    assert.match(brandCss, /\.denta-reply-avatar-image[\s\S]*object-fit:\s*contain/);

    assert.match(avatarModule, /DENTA_REPLY_AVATAR_URL\s*=\s*['"]\/assets\/images\/denta-reply-avatar\.png['"]/);
    assert.match(avatarModule, /replaceChildren\(buildAvatarImage\(documentRef\)\)/);
    assert.match(avatarModule, /new Observer\(/);

    assert.match(main, /import \{ installDentaReplyAvatar \} from ['"]\/assets\/js\/core\/dentaReplyAvatar\.js['"]/);
    assert.match(main, /installDentaReplyAvatar\(\);[\s\S]*new ChatBot\(\)/);

    assert.ok(portrait.size > 10000, 'generated reply portrait should be a real non-empty image asset');
});
