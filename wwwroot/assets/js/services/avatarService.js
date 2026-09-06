import { showSuccess, showError } from './ui.js';
import { t, onLanguageChange } from '../core/i18n.js';

const AVATAR_ENDPOINT = '/api/avatar';
const MAX_SIZE = 3 * 1024 * 1024; // 3 МБ

async function parseJsonOrThrow(response) {
    if (!response.ok) {
        const error = await response.json().catch(() => ({}));
        throw new Error(error.message || `Ошибка ${response.status}`);
    }
    return response.json();
}

/**
 * Загружает файл аватара на сервер (multipart/form-data — поэтому не через
 * apiFetch, который всегда шлёт Content-Type: application/json).
 * Авторизация выполняется только через HttpOnly dc_auth cookie: браузер
 * отправляет её на same-origin запрос автоматически, а JavaScript не имеет
 * доступа к JWT.
 */
export async function uploadAvatar(file) {
    const formData = new FormData();
    formData.append('file', file);
    const response = await fetch(AVATAR_ENDPOINT, {
        method: 'POST',
        credentials: 'same-origin',
        body: formData
    });
    return parseJsonOrThrow(response);
}

export async function deleteAvatar() {
    const response = await fetch(AVATAR_ENDPOINT, {
        method: 'DELETE',
        credentials: 'same-origin'
    });
    return parseJsonOrThrow(response);
}

function createAvatarVisual(url, fallbackIcon) {
    if (url) {
        const img = document.createElement('img');
        img.src = url;
        img.alt = t('avatar_alt', 'Аватар');
        return img;
    }

    const fallback = document.createElement('span');
    fallback.className = 'avatar-fallback';
    fallback.textContent = fallbackIcon;
    return fallback;
}

/**
 * Ставит фото (или запасной эмодзи) во все элементы с классом .panel-user-avatar
 * на странице — используется, чтобы обновить аватар в сайдбаре сразу после
 * загрузки/удаления в карточке профиля.
 *
 * Не собираем <img> через innerHTML: AvatarUrl приходит с сервера, но DOM API
 * сохраняет границу безопасной даже если источник URL когда-нибудь изменится.
 */
export function paintAvatarEverywhere(url, fallbackIcon = '👤') {
    document.querySelectorAll('.panel-user-avatar').forEach(el => {
        el.replaceChildren(createAvatarVisual(url, fallbackIcon));
    });
}

/**
 * Строит внутри контейнера #rootId полноценный виджет загрузки аватара:
 * круглое превью с оверлеем-камерой, drag&drop, спиннер во время загрузки,
 * кнопки "Изменить фото" / "Удалить".
 */
export function initAvatarUploader({ rootId, initialUrl, fallbackIcon = '👤', onChange } = {}) {
    const root = document.getElementById(rootId);
    if (!root) return null;

    let currentUrl = initialUrl;
    let circle, input, changeBtn, removeBtn, spinner;

    const replaceCircleVisual = () => {
        if (!circle) return;
        circle.querySelectorAll('img, .avatar-fallback').forEach(el => el.remove());
        circle.prepend(createAvatarVisual(currentUrl, fallbackIcon));
    };

    const render = () => {
        // Only static/trusted UI copy is placed into the template. The server-provided
        // avatar URL and fallback content are attached below via DOM properties instead
        // of being interpolated into HTML/attributes.
        root.innerHTML = `
            <div class="avatar-uploader">
                <div class="avatar-circle" id="${rootId}-circle" tabindex="0" role="button" aria-label="${t('avatar_upload_aria', 'Загрузить фото')}">
                    <div class="avatar-overlay">
                        <span class="avatar-overlay-icon">📷</span>
                        <span class="avatar-overlay-text">${t('avatar_overlay_text', 'Изменить')}</span>
                    </div>
                    <div class="avatar-spinner hidden"></div>
                </div>
                <input type="file" id="${rootId}-input" accept="image/png, image/jpeg, image/webp" class="hidden">
                <div class="avatar-actions">
                    <button type="button" class="avatar-btn-change" id="${rootId}-change">📷 ${t('avatar_btn_change', 'Изменить фото')}</button>
                    <button type="button" class="avatar-btn-remove${currentUrl ? '' : ' hidden'}" id="${rootId}-remove">🗑 ${t('avatar_btn_remove', 'Удалить')}</button>
                </div>
                <p class="avatar-hint">${t('avatar_hint', 'JPG, PNG или WEBP, до 3 МБ')}</p>
            </div>`;

        circle = root.querySelector(`#${rootId}-circle`);
        input = root.querySelector(`#${rootId}-input`);
        changeBtn = root.querySelector(`#${rootId}-change`);
        removeBtn = root.querySelector(`#${rootId}-remove`);
        spinner = root.querySelector('.avatar-spinner');
        replaceCircleVisual();
        _wireEvents();
    };

    const setImage = (url) => {
        currentUrl = url;
        replaceCircleVisual();
        removeBtn.classList.toggle('hidden', !url);
        paintAvatarEverywhere(url, fallbackIcon);
        onChange?.(url);
    };

    const setBusy = (busy) => {
        spinner.classList.toggle('hidden', !busy);
        circle.classList.toggle('avatar-loading', busy);
    };

    const handleFile = async (file) => {
        if (!file) return;
        if (!file.type.startsWith('image/')) { showError(t('avatar_err_not_image', 'Выберите файл изображения')); return; }
        if (file.size > MAX_SIZE) { showError(t('avatar_err_too_big', 'Файл слишком большой (максимум 3 МБ)')); return; }

        setBusy(true);
        try {
            const { avatarUrl } = await uploadAvatar(file);
            setImage(avatarUrl);
            showSuccess(t('avatar_uploaded', 'Аватар обновлён'));
        } catch (err) {
            showError(err.message || t('avatar_upload_error', 'Не удалось загрузить аватар'));
        } finally {
            setBusy(false);
            input.value = '';
        }
    };

    function _wireEvents() {
        changeBtn.addEventListener('click', () => input.click());
        circle.addEventListener('click', () => input.click());
        circle.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); input.click(); }
        });
        input.addEventListener('change', () => handleFile(input.files?.[0]));

        removeBtn.addEventListener('click', async () => {
            setBusy(true);
            try {
                await deleteAvatar();
                setImage(null);
                showSuccess(t('avatar_removed', 'Аватар удалён'));
            } catch (err) {
                showError(err.message || t('avatar_remove_error', 'Не удалось удалить аватар'));
            } finally {
                setBusy(false);
            }
        });

        ['dragover', 'dragleave', 'drop'].forEach(evt => circle.addEventListener(evt, e => e.preventDefault()));
        circle.addEventListener('dragover', () => circle.classList.add('avatar-drag'));
        circle.addEventListener('dragleave', () => circle.classList.remove('avatar-drag'));
        circle.addEventListener('drop', (e) => {
            circle.classList.remove('avatar-drag');
            handleFile(e.dataTransfer.files?.[0]);
        });
    }

    render();

    // При смене языка перерисовываем виджет целиком (кнопки, подсказки,
    // aria-label), сохраняя текущее фото и все обработчики событий.
    onLanguageChange(() => render());

    return { setImage };
}