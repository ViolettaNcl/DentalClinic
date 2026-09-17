import test from 'node:test';
import assert from 'node:assert/strict';
import {
    buildDoctorPayload,
    formatDoctorKnowledgeSummary,
} from '../../wwwroot/assets/js/managers/admin/doctorKnowledgeUtils.js';

test('builds normalized doctor knowledge payload with localized names', () => {
    const result = buildDoctorPayload({
        fullName: '  Dr. Анна Тестова ',
        fullNameEn: ' Dr. Anna Testova ',
        fullNameFr: ' Dr Anna Testova ',
        fullNameEl: ' Δρ. Άννα Τέστοβα ',
        fullNameAr: ' د. آنا تيستوفا ',
        specialization: ' имплантология, хирургия ',
        experienceYears: '12',
        bio: ' Практикующий стоматолог-хирург. ',
    });

    assert.equal(result.ok, true);
    assert.deepEqual(result.payload, {
        fullName: 'Dr. Анна Тестова',
        fullNameEn: 'Dr. Anna Testova',
        fullNameFr: 'Dr Anna Testova',
        fullNameEl: 'Δρ. Άννα Τέστοβα',
        fullNameAr: 'د. آنا تيستوفا',
        specialization: 'имплантология, хирургия',
        experienceYears: 12,
        bio: 'Практикующий стоматолог-хирург.',
    });
});

test('edit payload carries active state, empty localized names and can explicitly clear experience', () => {
    const result = buildDoctorPayload({
        fullName: 'Dr. Test',
        experienceYears: '',
        isActive: false,
    }, { edit: true });

    assert.equal(result.ok, true);
    assert.equal(result.payload.clearExperienceYears, true);
    assert.equal(result.payload.isActive, false);
    assert.equal(result.payload.fullNameEn, '');
    assert.equal(result.payload.fullNameFr, '');
    assert.equal(result.payload.fullNameEl, '');
    assert.equal(result.payload.fullNameAr, '');
    assert.equal('experienceYears' in result.payload, false);
});

test('edit payload preserves a supplied experience value', () => {
    const result = buildDoctorPayload({
        fullName: 'Dr. Test',
        experienceYears: '14',
        isActive: true,
    }, { edit: true });

    assert.equal(result.ok, true);
    assert.equal(result.payload.clearExperienceYears, false);
    assert.equal(result.payload.experienceYears, 14);
});

test('rejects missing name, oversized localized name and invalid experience', () => {
    assert.equal(buildDoctorPayload({ fullName: '', experienceYears: '10' }).ok, false);
    assert.equal(buildDoctorPayload({ fullName: 'Dr. Test', fullNameEn: 'x'.repeat(151) }).ok, false);
    assert.equal(buildDoctorPayload({ fullName: 'Dr. Test', experienceYears: '-1' }).ok, false);
    assert.equal(buildDoctorPayload({ fullName: 'Dr. Test', experienceYears: '81' }).ok, false);
    assert.equal(buildDoctorPayload({ fullName: 'Dr. Test', experienceYears: '1.5' }).ok, false);
});

test('formats knowledge summary without inventing missing data', () => {
    assert.equal(
        formatDoctorKnowledgeSummary({ specialization: 'Ортопедия', experienceYears: 9 }),
        'Ортопедия · стаж 9 лет'
    );
    assert.equal(formatDoctorKnowledgeSummary({}), 'Профиль для Денты не заполнен');
});

test('builds rich public doctor-card fields for admin editor', () => {
    const result = buildDoctorPayload({
        fullName: 'Dr. Rich Profile',
        roleTitle: 'Хирург-имплантолог · Врач высшей категории',
        specialization: 'имплантология, хирургия',
        experienceYears: '8',
        bio: 'Профессиональный профиль врача.',
        education: 'Волгоградский ГМУ\nОрдинатура — хирургия',
        skills: 'Имплантация\nКостная пластика',
        philosophy: 'Точный план лечения и понятная коммуникация.',
        stat2Value: '250+',
        stat2Label: 'Операций',
        stat3Value: '5/5',
        stat3Label: 'Рейтинг',
    });

    assert.equal(result.ok, true);
    assert.equal(result.payload.roleTitle, 'Хирург-имплантолог · Врач высшей категории');
    assert.equal(result.payload.education.includes('Ординатура'), true);
    assert.equal(result.payload.skills.includes('Имплантация'), true);
    assert.equal(result.payload.stat2Value, '250+');
    assert.equal(result.payload.stat3Label, 'Рейтинг');
});
