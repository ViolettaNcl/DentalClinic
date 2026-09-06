import test from 'node:test';
import assert from 'node:assert/strict';
import {
    bookingLabel,
    doctorExperienceText,
    doctorInitials,
    findDoctorByRenderedName,
    localizedDoctorName,
    normalizeDoctorName,
} from '../../wwwroot/assets/js/managers/public/doctorPublicProfileUtils.js';

const doctor = {
    id: 7,
    fullName: 'Dr. Раис Наджиб',
    fullNameEn: 'Dr. Rais Najib',
    fullNameFr: 'Dr Rais Najib',
    fullNameEl: 'Δρ. Rais Najib',
    fullNameAr: 'د. رايس نجيب',
    experienceYears: 12,
};

test('localizedDoctorName uses selected language and falls back safely', () => {
    assert.equal(localizedDoctorName(doctor, 'en'), 'Dr. Rais Najib');
    assert.equal(localizedDoctorName(doctor, 'ar'), 'د. رايس نجيب');
    assert.equal(localizedDoctorName({ fullName: 'Доктор Тест' }, 'fr'), 'Доктор Тест');
});

test('normalizeDoctorName ignores common localized doctor prefixes and punctuation', () => {
    assert.equal(normalizeDoctorName('Dr. Rais Najib'), 'rais najib');
    assert.equal(normalizeDoctorName('д-р Раис Наджиб'), 'раис наджиб');
    assert.equal(normalizeDoctorName('Δρ. Rais Najib'), 'rais najib');
    assert.equal(normalizeDoctorName('د. رايس نجيب'), 'رايس نجيب');
});

test('findDoctorByRenderedName matches any stored localized doctor name', () => {
    assert.equal(findDoctorByRenderedName('Dr Rais Najib', [doctor])?.id, 7);
    assert.equal(findDoctorByRenderedName('Δρ Rais Najib', [doctor])?.id, 7);
    assert.equal(findDoctorByRenderedName('Unknown', [doctor]), null);
});

test('experience and booking labels are localized without inventing clinical data', () => {
    assert.equal(doctorExperienceText(doctor, 'en'), '12+ years of experience');
    assert.equal(doctorExperienceText({ experienceYears: null }, 'ru'), '');
    assert.equal(bookingLabel('fr'), 'Prendre rendez-vous');
    assert.equal(bookingLabel('ar'), 'احجز موعدًا');
});

test('doctorInitials uses localized visible name', () => {
    assert.equal(doctorInitials(doctor, 'en'), 'DR');
    assert.equal(doctorInitials({ fullName: 'Анна Тестова' }, 'ru'), 'АТ');
});
