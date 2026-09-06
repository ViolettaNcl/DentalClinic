import { getLang, onLanguageChange, ready } from '../../core/i18n.js';
import {
    bookingLabel,
    doctorExperienceText,
    doctorInitials,
    findDoctorByRenderedName,
    localizedDoctorName,
} from './doctorPublicProfileUtils.js';

const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
const isFinePointer = window.matchMedia('(pointer: fine)').matches;
let activeDoctors = [];
let observer = null;

function initObserver() {
    if (observer || !('IntersectionObserver' in window)) return;
    observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (!entry.isIntersecting) return;
            const card = entry.target;
            card.classList.add('in-view');
            animateStats(card);
            observer.unobserve(card);
        });
    }, { threshold: 0.25 });
}

function bindCardInteractions(card) {
    if (!card || card.dataset.doctorInteractionsBound === '1') return;
    card.dataset.doctorInteractionsBound = '1';

    if (isFinePointer && !prefersReducedMotion) {
        const maxTilt = 4;
        card.addEventListener('mousemove', (event) => {
            const rect = card.getBoundingClientRect();
            const x = (event.clientX - rect.left) / rect.width;
            const y = (event.clientY - rect.top) / rect.height;
            const rotateY = (x - 0.5) * maxTilt * 2;
            const rotateX = (0.5 - y) * maxTilt * 2;
            card.style.transform = `perspective(1200px) rotateX(${rotateX}deg) rotateY(${rotateY}deg)`;
        });
        card.addEventListener('mouseleave', () => {
            card.style.transform = 'perspective(1200px) rotateX(0deg) rotateY(0deg)';
        });
    }

    initObserver();
    if (observer) observer.observe(card);
    else {
        card.classList.add('in-view');
        animateStats(card);
    }
}

function animateStats(card) {
    card.querySelectorAll('.stat-number').forEach(element => {
        if (element.dataset.animated === '1') return;
        const raw = element.textContent.trim();
        const match = raw.match(/^(\d+)(.*)$/);
        if (!match) return;

        element.dataset.animated = '1';
        const target = Number.parseInt(match[1], 10);
        const suffix = match[2] || '';
        if (prefersReducedMotion) {
            element.textContent = target + suffix;
            return;
        }

        const duration = 1200;
        const start = performance.now();
        function tick(now) {
            const progress = Math.min((now - start) / duration, 1);
            const eased = 1 - Math.pow(1 - progress, 3);
            element.textContent = Math.round(target * eased) + suffix;
            if (progress < 1) requestAnimationFrame(tick);
        }
        requestAnimationFrame(tick);
    });
}

function hydrateStaticCard(card, doctor, language) {
    card.hidden = false;
    card.dataset.doctorId = String(doctor.id ?? '');

    const name = localizedDoctorName(doctor, language);
    const nameElement = card.querySelector('.doctor-card__name');
    if (nameElement && name) nameElement.textContent = name;

    const image = card.querySelector('.doctor-card__photo img');
    if (image && name) image.alt = name;

    if (Number.isInteger(Number(doctor.experienceYears)) && Number(doctor.experienceYears) >= 0) {
        const firstStat = card.querySelector('.stat-number');
        if (firstStat) {
            firstStat.textContent = `${Number(doctor.experienceYears)}+`;
            firstStat.dataset.animated = '0';
        }
    }

    // Specialization and bio are currently stored as one confirmed source-language
    // value. Do not leak Russian copy into another selected language; localized names
    // and numeric experience remain safe to synchronize everywhere.
    if (language === 'ru') {
        const role = card.querySelector('.doctor-card__role');
        const description = card.querySelector('.doctor-card__desc');
        if (role && String(doctor.specialization || '').trim())
            role.textContent = String(doctor.specialization).trim();
        if (description && String(doctor.bio || '').trim())
            description.textContent = String(doctor.bio).trim();
    }
}

function createDynamicCard(doctor, language, index) {
    const card = document.createElement('div');
    card.className = `doctor-card doctor-card--dynamic${index % 2 ? ' doctor-card--reverse' : ''}`;
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
        number.textContent = `${Number(doctor.experienceYears)}+`;
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

    if (language === 'ru' && String(doctor.specialization || '').trim()) {
        const role = document.createElement('p');
        role.className = 'doctor-card__role';
        role.textContent = String(doctor.specialization).trim();
        body.append(role);
    }
    if (language === 'ru' && String(doctor.bio || '').trim()) {
        const description = document.createElement('p');
        description.className = 'doctor-card__desc';
        description.textContent = String(doctor.bio).trim();
        body.append(description);
    }

    const button = document.createElement('a');
    button.href = '/pages/contact.html';
    button.className = 'btn-primary doctor-card__btn';
    button.textContent = bookingLabel(language);
    body.append(button);

    card.append(aside, body);
    return card;
}

function ensureDynamicStyles() {
    if (document.getElementById('dynamic-doctor-styles')) return;
    const style = document.createElement('style');
    style.id = 'dynamic-doctor-styles';
    style.textContent = `
        .doctor-card__photo--fallback{display:flex;align-items:center;justify-content:center;min-height:260px;background:linear-gradient(145deg,#eef8f5,#d8eee8)}
        .doctor-card__initials{font:800 clamp(3rem,7vw,5rem)/1 Manrope,sans-serif;color:#147f70;letter-spacing:.04em}
        .doctor-card__stats--compact{justify-content:center}
        .doctor-card[hidden],.section-divider[hidden]{display:none!important}
    `;
    document.head.append(style);
}

function reconcileDoctorCards(language = getLang()) {
    if (!activeDoctors.length) return;
    const container = document.querySelector('.doctors-section .container');
    if (!container) return;

    container.querySelectorAll('[data-dynamic-doctor="1"], [data-dynamic-doctor-divider="1"]')
        .forEach(element => element.remove());

    const matchedIds = new Set();
    const staticCards = [...container.querySelectorAll('.doctor-card:not([data-dynamic-doctor])')];
    staticCards.forEach(card => {
        const anchorName = card.dataset.doctorFallbackName || card.querySelector('.doctor-card__name')?.textContent || '';
        const doctor = findDoctorByRenderedName(anchorName, activeDoctors);
        if (!doctor) {
            card.hidden = true;
            return;
        }
        matchedIds.add(String(doctor.id));
        hydrateStaticCard(card, doctor, language);
        bindCardInteractions(card);
    });

    const unmatched = activeDoctors.filter(doctor => !matchedIds.has(String(doctor.id)));
    unmatched.forEach((doctor, index) => {
        const divider = document.createElement('div');
        divider.className = 'section-divider';
        divider.dataset.dynamicDoctorDivider = '1';
        container.append(divider);
        const card = createDynamicCard(doctor, language, index);
        container.append(card);
        bindCardInteractions(card);
    });

    const visibleCards = [...container.querySelectorAll('.doctor-card')].filter(card => !card.hidden);
    container.querySelectorAll('.section-divider:not([data-dynamic-doctor-divider])').forEach(divider => {
        const before = staticCards.find(card => card.compareDocumentPosition(divider) & Node.DOCUMENT_POSITION_FOLLOWING && !card.hidden);
        const after = staticCards.find(card => divider.compareDocumentPosition(card) & Node.DOCUMENT_POSITION_FOLLOWING && !card.hidden);
        divider.hidden = !before || !after || visibleCards.length < 2;
    });
}

async function syncPublicDoctors() {
    try {
        const response = await fetch('/api/doctor', { headers: { Accept: 'application/json' } });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const doctors = await response.json();
        if (!Array.isArray(doctors)) throw new Error('Unexpected doctor catalogue response');
        activeDoctors = doctors;
        ensureDynamicStyles();
        reconcileDoctorCards(getLang());
    } catch (error) {
        // Static curated cards remain as a resilient fallback when the API/database
        // is temporarily unavailable. Never hide content merely because fetch failed.
        console.warn('[doctors] Public catalogue sync unavailable:', error);
    }
}

async function init() {
    document.querySelectorAll('.doctor-card').forEach(bindCardInteractions);
    await ready;
    await syncPublicDoctors();
    onLanguageChange(language => {
        if (activeDoctors.length) queueMicrotask(() => reconcileDoctorCards(language));
    });
}

if (document.readyState === 'loading')
    document.addEventListener('DOMContentLoaded', init, { once: true });
else
    init();
