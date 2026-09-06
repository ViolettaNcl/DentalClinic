import test from 'node:test';
import assert from 'node:assert/strict';
import {
    buildDoctorPayload,
    formatDoctorKnowledgeSummary,
} from '../../wwwroot/assets/js/managers/admin/doctorKnowledgeUtils.js';

test('builds normalized doctor knowledge payload', () => {
    const result = buildDoctorPayload({
        fullName: '  Dr. Анна Тестова ',
        specialization: ' имплантология, хирургия ',
        experienceYears: '12',
        bio: ' Практикующий стоматолог-хирург. ',
    });

    assert.equal(result.ok, true);
    assert.deepEqual(result.payload, {
        fullName: 'Dr. Анна Тестова',
        specialization: 'имплантология, хирургия',
        experienceYears: 12,
        bio: 'Практикующий стоматолог-хирург.',
    });
});

test('edit payload carries active state and can explicitly clear experience', () => {
    const result = buildDoctorPayload({
        fullName: 'Dr. Test',
        experienceYears: '',
        isActive: false,
    }, { edit: true });

    assert.equal(result.ok, true);
    assert.equal(result.payload.clearExperienceYears, true);
    assert.equal(result.payload.isActive, false);
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

test('rejects missing name and invalid experience', () => {
    assert.equal(buildDoctorPayload({ fullName: '', experienceYears: '10' }).ok, false);
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
