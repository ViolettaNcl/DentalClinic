import { getLang, onLanguageChange } from '../../core/i18n.js';
import {
    bookingLabel,
    doctorExperienceText,
    doctorExperienceYears,
    doctorInitials,
    findDoctorByRenderedName,
    localizedDoctorName,
} from './doctorPublicProfileUtils.js';

const STATIC_NAME_FALLBACKS = Object.freeze({
    doc1_name: 'Dr. Раис Наджиб',
    doc2_name: 'Dr. Лилит Рейнер',
});

let activeDoctors = [];
let installed = false;

function staticAnchorName(card) {
    const nameElement = card.querySelector('.doctor-card__name');
    const rendered = nameElement?.textContent || '';
    const key = nameElement?.dataset.i18n;
    return { rendered, fallback: key ? STATIC_NAME_FALLBACKS[key] || '' : '' };
}

function findDoctorForStaticCard(card) {
    const { rendered, fallback } = staticAnchorName(card);
    return findDoctorByRenderedName(rendered, activeDoctors)
        || findDoctorByRenderedName(fallback, activeDoctors);
}

function hydrateStaticCard(card, doctor, language) {
    card.hidden = false;
    card.dataset.doctorId = String(doctor.id ?? '');

    const name = localizedDoctorName(doctor, language);
    const nameElement = card.querySelector('.doctor-card__name');
    if (nameElement && name) nameElement.textContent = name;

    const image = card.querySelector('.doctor-card__photo img');
    if (image && name) image.alt = name;

    const years = doctorExperienceYears(doctor);
    if (years !== null) {
        const firstStat = card.querySelector('.stat-number');
        if (firstStat) firstStat.textContent = `${years}+`;
    }

    // Specialization and biography currently have a single confirmed source-language
    // value in the database. Keep existing translated curated copy outside Russian
    // rather than showing Russian text under an EN/FR/EL/AR interface.
    if (language === 'ru') {
        const role = card.querySelector('.doctor-card__role');
        const description = card.querySelector('.doctor-card__desc');
        const specialization = String(doctor.specialization || '').trim();
        const bio = String(doctor.bio || '').trim();
        if (role && specialization) role.textContent = specialization;
        if (description && bio) description.textContent = bio;
    }
}

function createDynamicCard(doctor, language, reverse) {
    const card = document.createElement('div');
    card.className = `doctor-card doctor-card--dynamic${reverse ? ' doctor-card--reverse' : ''} in-view`;
    card.dataset.dynamicDoctor = '1';
    card.dataset.doctorId = String(doctor.id ?? '');

    const aside = document.createElement('aside');
    aside.className = 'doctor-card__aside';

    const photo = document.createElement('div');
    photo.className = 'doctor-card__photo doctor-card__photo--fallback';
    const initials = document.createElement('span');
    initials.className = 'doctor-card__initials';
    initials.textContent = doctorInitials(doctor, language);
    photo.append(initials);
    aside.append(photo);

    const experience = doctorExperienceText(doctor, language);
    if (experience) {
        const stats = document.createElement('div');
        stats.className = 'doctor-card__stats doctor-card__stats--compact';
        const item = document.createElement('div');
        item.className = 'stat-item';
        const number = document.createElement('div');
        number.className = 'stat-number';
        number.textContent = `${doctorExperienceYears(doctor)}+`;
        const label = document.createElement('div');
        label.className = 'stat-label';
        label.textContent = experience.replace(/^\d+\+\s*/, '');
        item.append(number, label);
        stats.append(item);
        aside.append(stats);
    }

    const body = document.createElement('div');
    body.className = 'doctor-card__body';

    const name = document.createElement('h2');
    name.className = 'doctor-card__name';
    name.textContent = localizedDoctorName(doctor, language);
    body.append(name);

    if (language === 'ru') {
        const specialization = String(doctor.specialization || '').trim();
        const bio = String(doctor.bio || '').trim();
        if (specialization) {
            const role = document.createElement('p');
            role.className = 'doctor-card__role';
            role.textContent = specialization;
            body.append(role);
        }
        if (bio) {
            const description = document.createElement('p');
            description.className = 'doctor-card__desc';
            description.textContent = bio;
            body.append(description);
        }
    }

    const button = document.createElement('a');
    button.href = '/pages/contact.html';
    button.className = 'btn-primary doctor-card__btn';
    button.textContent = bookingLabel(language);
    body.append(button);

    card.append(aside, body);
    return card;
}

function ensureStyles() {
    if (document.getElementById('public-doctor-catalog-styles')) return;
    const style = document.createElement('style');
    style.id = 'public-doctor-catalog-styles';
    style.textContent = `
        .doctor-card[hidden],.section-divider[hidden]{display:none!important}
        .doctor-card__photo--fallback{display:flex;align-items:center;justify-content:center;min-height:260px;background:linear-gradient(145deg,#eef8f5,#d8eee8)}
        .doctor-card__initials{font:800 clamp(3rem,7vw,5rem)/1 Manrope,sans-serif;color:#147f70;letter-spacing:.04em}
        .doctor-card__stats--compact{justify-content:center}
    `;
    document.head.append(style);
}

function reconcile(language = getLang()) {
    const container = document.querySelector('.doctors-section .container');
    if (!container || !Array.isArray(activeDoctors)) return;

    container.querySelectorAll('[data-dynamic-doctor="1"], [data-dynamic-doctor-divider="1"]')
        .forEach(element => element.remove());

    const staticCards = [...container.querySelectorAll('.doctor-card:not([data-dynamic-doctor])')];
    const matchedIds = new Set();

    staticCards.forEach(card => {
        const doctor = findDoctorForStaticCard(card);
        if (!doctor) {
            card.hidden = true;
            return;
        }
        matchedIds.add(String(doctor.id));
        hydrateStaticCard(card, doctor, language);
    });

    // The original page has dividers between curated static cards. Hide a divider
    // whenever either side is no longer represented by an active DB doctor.
    container.querySelectorAll('.section-divider:not([data-dynamic-doctor-divider])').forEach(divider => {
        const previousCard = divider.previousElementSibling?.classList.contains('doctor-card')
            ? divider.previousElementSibling
            : null;
        const nextCard = divider.nextElementSibling?.classList.contains('doctor-card')
            ? divider.nextElementSibling
            : null;
        divider.hidden = !previousCard || previousCard.hidden || !nextCard || nextCard.hidden;
    });

    const unmatched = activeDoctors.filter(doctor => !matchedIds.has(String(doctor.id)));
    let visibleCount = staticCards.filter(card => !card.hidden).length;
    unmatched.forEach((doctor, index) => {
        if (visibleCount > 0) {
            const divider = document.createElement('div');
            divider.className = 'section-divider';
            divider.dataset.dynamicDoctorDivider = '1';
            container.append(divider);
        }
        container.append(createDynamicCard(doctor, language, (visibleCount + index) % 2 === 1));
        visibleCount += 1;
    });
}

async function loadDoctors() {
    const container = document.querySelector('.doctors-section .container');
    if (!container) return;

    try {
        const response = await fetch('/api/doctor', { headers: { Accept: 'application/json' } });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const doctors = await response.json();
        if (!Array.isArray(doctors)) throw new Error('Unexpected doctor catalogue response');

        activeDoctors = doctors;
        ensureStyles();
        reconcile(getLang());
    } catch (error) {
        // Preserve the curated static page as the fallback when the database/API is
        // temporarily unavailable. A network failure must not blank the doctors page.
        console.warn('[doctors] Public doctor catalogue unavailable:', error);
    }
}

export function installPublicDoctorCatalogSync() {
    if (installed) return;
    installed = true;

    const start = () => loadDoctors();
    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', start, { once: true });
    else
        start();

    onLanguageChange(language => {
        if (!activeDoctors.length) return;
        // LanguageSwitcher updates data-i18n DOM nodes after setLang resolves. Run
        // after that update so DB-localized names remain the final visible value.
        setTimeout(() => reconcile(language), 0);
    });
}
