import test from 'node:test';
import assert from 'node:assert/strict';
import {
    buildClinicKnowledgePayload,
    CLINIC_KNOWLEDGE_LIMITS,
} from '../../wwwroot/assets/js/managers/admin/clinicKnowledgeUtils.js';

test('builds normalized clinic knowledge payload', () => {
    const result = buildClinicKnowledgePayload({
        category: '  Оплата ',
        title: ' Способы оплаты ',
        content: ' Подтверждённый текст ',
        keywords: ' карта, наличные ',
        sortOrder: '3',
    });

    assert.equal(result.ok, true);
    assert.deepEqual(result.payload, {
        category: 'Оплата',
        title: 'Способы оплаты',
        content: 'Подтверждённый текст',
        keywords: 'карта, наличные',
        sortOrder: 3,
        isActive: true,
    });
});

test('edit payload preserves explicit active state', () => {
    const result = buildClinicKnowledgePayload({
        category: 'FAQ',
        title: 'Вопрос',
        content: 'Ответ',
        sortOrder: '0',
        isActive: false,
    }, { edit: true });

    assert.equal(result.ok, true);
    assert.equal(result.payload.isActive, false);
});

test('rejects missing required fields and invalid sort order', () => {
    assert.equal(buildClinicKnowledgePayload({ category: '', title: 'A', content: 'B' }).ok, false);
    assert.equal(buildClinicKnowledgePayload({ category: 'A', title: '', content: 'B' }).ok, false);
    assert.equal(buildClinicKnowledgePayload({ category: 'A', title: 'B', content: '' }).ok, false);
    assert.equal(buildClinicKnowledgePayload({ category: 'A', title: 'B', content: 'C', sortOrder: '-1' }).ok, false);
    assert.equal(buildClinicKnowledgePayload({ category: 'A', title: 'B', content: 'C', sortOrder: '1.5' }).ok, false);
});

test('rejects values above server persistence limits', () => {
    const base = { category: 'A', title: 'B', content: 'C' };
    for (const [field, max] of Object.entries(CLINIC_KNOWLEDGE_LIMITS)) {
        const result = buildClinicKnowledgePayload({ ...base, [field]: 'x'.repeat(max + 1) });
        assert.equal(result.ok, false, `${field} should reject more than ${max} chars`);
    }
});
