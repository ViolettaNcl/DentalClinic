const DENTA_REPLY_AVATAR_URL = '/assets/images/denta-reply-avatar.png';
const DENTA_REPLY_SELECTOR = '.chat-bubble--bot .chat-bubble-avatar';

function collectReplyAvatars(root) {
    const avatars = [];
    if (!root) return avatars;

    if (typeof root.matches === 'function' && root.matches(DENTA_REPLY_SELECTOR)) {
        avatars.push(root);
    }
    if (typeof root.querySelectorAll === 'function') {
        avatars.push(...root.querySelectorAll(DENTA_REPLY_SELECTOR));
    }

    return [...new Set(avatars)];
}

function buildAvatarImage(documentRef) {
    const image = documentRef.createElement('img');
    image.className = 'denta-reply-avatar-image';
    image.src = DENTA_REPLY_AVATAR_URL;
    image.alt = '';
    image.width = 36;
    image.height = 36;
    image.decoding = 'async';
    image.draggable = false;
    image.setAttribute('aria-hidden', 'true');
    return image;
}

export function upgradeDentaReplyAvatars(root = globalThis.document) {
    let upgraded = 0;

    collectReplyAvatars(root).forEach(avatar => {
        if (avatar.querySelector?.('img.denta-reply-avatar-image')) return;

        const documentRef = avatar.ownerDocument || globalThis.document;
        if (!documentRef?.createElement) return;

        avatar.replaceChildren(buildAvatarImage(documentRef));
        avatar.classList.add('chat-bubble-avatar--ready');
        upgraded += 1;
    });

    return upgraded;
}

/**
 * ChatBot builds both streaming and fallback messages dynamically. Watching the
 * message DOM keeps the same real image in every Denta reply without coupling
 * the core chat transport to branding markup.
 */
export function installDentaReplyAvatar(root = globalThis.document) {
    if (!root) return null;

    upgradeDentaReplyAvatars(root);

    const documentRef = root.nodeType === 9
        ? root
        : (root.ownerDocument || globalThis.document);
    const Observer = documentRef?.defaultView?.MutationObserver || globalThis.MutationObserver;
    const target = root.body || root.documentElement || root;

    if (!Observer || !target) return null;

    const observer = new Observer(records => {
        records.forEach(record => {
            record.addedNodes?.forEach(node => {
                if (node?.nodeType === 1) upgradeDentaReplyAvatars(node);
            });
        });
    });

    observer.observe(target, { childList: true, subtree: true });
    return observer;
}

export { DENTA_REPLY_AVATAR_URL };
