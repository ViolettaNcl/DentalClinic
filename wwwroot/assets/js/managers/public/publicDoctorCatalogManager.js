import { getLang, onLanguageChange } from '../../core/i18n.js';
import {
    bookingLabel,
    doctorExperienceYears,
    doctorInitials,
    findDoctorByRenderedName,
    localizedDoctorName,
    normalizeDoctorName,
} from './doctorPublicProfileUtils.js';

const STATIC_NAME_FALLBACKS = Object.freeze({ doc1_name: 'Dr. Раис Наджиб', doc2_name: 'Dr. Лилит Рейнер' });
const LABELS = Object.freeze({
    ru: { edu: 'Образование', skills: 'Специализация', philosophy: 'Философия:', years: 'Лет опыта' },
    en: { edu: 'Education', skills: 'Specialization', philosophy: 'Philosophy:', years: 'Years experience' },
    fr: { edu: 'Formation', skills: 'Spécialisation', philosophy: 'Philosophie :', years: "Ans d'expérience" },
    el: { edu: 'Εκπαίδευση', skills: 'Εξειδίκευση', philosophy: 'Φιλοσοφία:', years: 'Χρόνια εμπειρίας' },
    ar: { edu: 'التعليم', skills: 'التخصص', philosophy: 'الفلسفة:', years: 'سنوات الخبرة' },
});

let activeDoctors = [];
let installed = false;

function textLines(value) {
    return String(value || '').split(/\r?\n|\s*;\s*/).map(x => x.trim()).filter(Boolean);
}

function staticAnchorName(card) {
    const nameElement = card.querySelector('.doctor-card__name');
    const rendered = nameElement?.textContent || '';
    const key = nameElement?.dataset.i18n;
    return { rendered, fallback: key ? STATIC_NAME_FALLBACKS[key] || '' : '' };
}

function normalizedNameSignature(value) {
    return normalizeDoctorName(value).split(/\s+/).filter(Boolean).sort((a, b) => a.localeCompare(b, 'ru')).join('|');
}

function findDoctorByNameIgnoringOrder(name, doctors = []) {
    const wanted = normalizedNameSignature(name);
    if (!wanted) return null;
    const fields = ['fullName', 'fullNameEn', 'fullNameFr', 'fullNameEl', 'fullNameAr'];
    return doctors.find(doctor => fields.some(field => normalizedNameSignature(doctor?.[field]) === wanted)) || null;
}

function findDoctorForStaticCard(card) {
    const { rendered, fallback } = staticAnchorName(card);
    return findDoctorByRenderedName(rendered, activeDoctors)
        || findDoctorByRenderedName(fallback, activeDoctors)
        || findDoctorByNameIgnoringOrder(rendered, activeDoctors)
        || findDoctorByNameIgnoringOrder(fallback, activeDoctors);
}

function setDatabaseText(element, value) {
    const text = String(value || '').trim();
    if (!element || !text) return;
    element.textContent = text;
    element.removeAttribute('data-i18n');
}

function renderEducation(container, value, language) {
    const lines = textLines(value);
    if (!container || !lines.length) return;
    const title = container.querySelector('h4');
    if (title) { title.textContent = (LABELS[language] || LABELS.ru).edu; title.removeAttribute('data-i18n'); }
    const list = container.querySelector('.edu-list') || document.createElement('ul');
    list.className = 'edu-list';
    list.replaceChildren(...lines.map(line => {
        const li = document.createElement('li'); li.textContent = line; return li;
    }));
    if (!list.parentNode) container.append(list);
}

function renderSkills(container, value, language) {
    const lines = textLines(value);
    if (!container || !lines.length) return;
    const title = container.querySelector('h4');
    if (title) { title.textContent = (LABELS[language] || LABELS.ru).skills; title.removeAttribute('data-i18n'); }
    const grid = container.querySelector('.skills-grid') || document.createElement('div');
    grid.className = 'skills-grid';
    grid.replaceChildren(...lines.map(line => {
        const item = document.createElement('div'); item.className = 'skill-item'; item.textContent = line; return item;
    }));
    if (!grid.parentNode) container.append(grid);
}

function renderStats(stats, doctor, language) {
    if (!stats) return;
    const years = doctorExperienceYears(doctor);
    const items = [];
    if (years !== null) items.push({ value: `${years}+`, label: (LABELS[language] || LABELS.ru).years });
    if (doctor.stat2Value && doctor.stat2Label) items.push({ value: doctor.stat2Value, label: doctor.stat2Label });
    if (doctor.stat3Value && doctor.stat3Label) items.push({ value: doctor.stat3Value, label: doctor.stat3Label });
    if (!items.length) return;

    stats.replaceChildren(...items.slice(0, 3).map(item => {
        const wrap = document.createElement('div'); wrap.className = 'stat-item';
        const icon = document.createElement('span'); icon.className = 'stat-icon';
        const number = document.createElement('div'); number.className = 'stat-number'; number.textContent = item.value;
        const label = document.createElement('div'); label.className = 'stat-label'; label.textContent = item.label;
        wrap.append(icon, number, label); return wrap;
    }));
}


function hydrateStaticStats(stats, doctor, language) {
    if (!stats) return;
    const items = [...stats.querySelectorAll('.stat-item')];
    const years = doctorExperienceYears(doctor);
    const update = (index, value, label) => {
        const item = items[index];
        if (!item || !value) return;
        const number = item.querySelector('.stat-number');
        const labelEl = item.querySelector('.stat-label');
        if (number) number.textContent = value;
        if (label && labelEl) { labelEl.textContent = label; labelEl.removeAttribute('data-i18n'); }
    };
    if (years !== null) update(0, `${years}+`, (LABELS[language] || LABELS.ru).years);
    if (doctor.stat2Value) update(1, doctor.stat2Value, doctor.stat2Label || null);
    if (doctor.stat3Value) update(2, doctor.stat3Value, doctor.stat3Label || null);
}

function hydrateStaticCard(card, doctor, language) {
    card.hidden = false;
    card.dataset.doctorId = String(doctor.id ?? '');

    const image = card.querySelector('.doctor-card__photo img');
    if (doctor.photoUrl && image) {
        image.src = doctor.photoUrl;
        image.removeAttribute('data-i18n-src');
    }
    if (image) image.alt = card.querySelector('.doctor-card__name')?.textContent?.trim() || doctor.fullName;

    setDatabaseText(card.querySelector('.doctor-card__role'), doctor.roleTitle);
    setDatabaseText(card.querySelector('.doctor-card__desc'), doctor.bio);
    renderEducation(card.querySelector('.doctor-card__edu'), doctor.education, language);
    renderSkills(card.querySelector('.doctor-card__skills'), doctor.skills, language);
    hydrateStaticStats(card.querySelector('.doctor-card__stats'), doctor, language);

    if (doctor.philosophy) {
        const quote = card.querySelector('.doctor-card__quote');
        if (quote) {
            quote.replaceChildren();
            const strong = document.createElement('strong'); strong.textContent = (LABELS[language] || LABELS.ru).philosophy;
            quote.append(strong, document.createTextNode(` ${doctor.philosophy}`));
        }
    }
}

function createDynamicCard(doctor, language, reverse) {
    const labels = LABELS[language] || LABELS.ru;
    const card = document.createElement('div');
    card.className = `doctor-card doctor-card--dynamic${reverse ? ' doctor-card--reverse' : ''} in-view`;
    card.dataset.dynamicDoctor = '1';
    card.dataset.doctorId = String(doctor.id ?? '');

    const aside = document.createElement('aside'); aside.className = 'doctor-card__aside';
    const photo = document.createElement('div'); photo.className = 'doctor-card__photo';
    if (doctor.photoUrl) {
        const img = document.createElement('img'); img.loading = 'lazy'; img.src = doctor.photoUrl; img.alt = localizedDoctorName(doctor, language); photo.append(img);
    } else {
        photo.classList.add('doctor-card__photo--fallback');
        const initials = document.createElement('span'); initials.className = 'doctor-card__initials'; initials.textContent = doctorInitials(doctor, language); photo.append(initials);
    }
    aside.append(photo);

    const stats = document.createElement('div'); stats.className = 'doctor-card__stats';
    renderStats(stats, doctor, language);
    if (stats.children.length) aside.append(stats);

    const body = document.createElement('div'); body.className = 'doctor-card__body';
    const name = document.createElement('h2'); name.className = 'doctor-card__name'; name.textContent = localizedDoctorName(doctor, language); body.append(name);

    const roleText = String(doctor.roleTitle || doctor.specialization || '').trim();
    if (roleText) { const role = document.createElement('p'); role.className = 'doctor-card__role'; role.textContent = roleText; body.append(role); }
    if (doctor.bio) { const desc = document.createElement('p'); desc.className = 'doctor-card__desc'; desc.textContent = doctor.bio; body.append(desc); }

    const education = textLines(doctor.education);
    if (education.length) {
        const wrap = document.createElement('div'); wrap.className = 'doctor-card__edu';
        const h = document.createElement('h4'); h.textContent = labels.edu;
        const ul = document.createElement('ul'); ul.className = 'edu-list';
        education.forEach(line => { const li = document.createElement('li'); li.textContent = line; ul.append(li); });
        wrap.append(h, ul); body.append(wrap);
    }

    const skills = textLines(doctor.skills);
    if (skills.length) {
        const wrap = document.createElement('div'); wrap.className = 'doctor-card__skills';
        const h = document.createElement('h4'); h.textContent = labels.skills;
        const grid = document.createElement('div'); grid.className = 'skills-grid';
        skills.forEach(line => { const item = document.createElement('div'); item.className = 'skill-item'; item.textContent = line; grid.append(item); });
        wrap.append(h, grid); body.append(wrap);
    }

    if (doctor.philosophy) {
        const quote = document.createElement('p'); quote.className = 'doctor-card__quote';
        const strong = document.createElement('strong'); strong.textContent = labels.philosophy;
        quote.append(strong, document.createTextNode(` ${doctor.philosophy}`)); body.append(quote);
    }

    const button = document.createElement('a');
    button.href = `/pages/contact.html?doctorId=${encodeURIComponent(doctor.id)}`;
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
        .doctor-card--dynamic .doctor-card__body{min-height:100%}
        .doctor-card--dynamic .doctor-card__btn{margin-top:auto}
        .doctor-card--dynamic .edu-list li{line-height:1.55}
    `;
    document.head.append(style);
}

function reconcile(language = getLang()) {
    const container = document.querySelector('.doctors-section .container');
    if (!container || !Array.isArray(activeDoctors)) return;

    container.querySelectorAll('[data-dynamic-doctor="1"], [data-dynamic-doctor-divider="1"]').forEach(element => element.remove());
    const staticCards = [...container.querySelectorAll('.doctor-card:not([data-dynamic-doctor])')];
    const matchedIds = new Set();

    staticCards.forEach(card => {
        const doctor = findDoctorForStaticCard(card);
        if (!doctor) { card.hidden = true; return; }
        matchedIds.add(String(doctor.id));
        hydrateStaticCard(card, doctor, language);
    });

    container.querySelectorAll('.section-divider:not([data-dynamic-doctor-divider])').forEach(divider => {
        const previousCard = divider.previousElementSibling?.classList.contains('doctor-card') ? divider.previousElementSibling : null;
        const nextCard = divider.nextElementSibling?.classList.contains('doctor-card') ? divider.nextElementSibling : null;
        divider.hidden = !previousCard || previousCard.hidden || !nextCard || nextCard.hidden;
    });

    const unmatched = activeDoctors.filter(doctor => !matchedIds.has(String(doctor.id)));
    let visibleCount = staticCards.filter(card => !card.hidden).length;
    unmatched.forEach((doctor, index) => {
        if (visibleCount > 0) {
            const divider = document.createElement('div'); divider.className = 'section-divider'; divider.dataset.dynamicDoctorDivider = '1'; container.append(divider);
        }
        container.append(createDynamicCard(doctor, language, (visibleCount + index) % 2 === 1));
        visibleCount += 1;
    });
}

async function loadDoctors() {
    const container = document.querySelector('.doctors-section .container');
    if (!container) return;
    try {
        const response = await fetch('/api/doctor', { headers: { Accept: 'application/json' }, credentials: 'same-origin' });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const doctors = await response.json();
        if (!Array.isArray(doctors)) throw new Error('Unexpected doctor catalogue response');
        activeDoctors = doctors; ensureStyles(); reconcile(getLang());
    } catch (error) {
        console.warn('[doctors] Public doctor catalogue unavailable:', error);
    }
}

export function installPublicDoctorCatalogSync() {
    if (installed) return;
    installed = true;
    const start = () => loadDoctors();
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start, { once: true }); else start();
    onLanguageChange(language => { if (activeDoctors.length) setTimeout(() => reconcile(language), 0); });
}
