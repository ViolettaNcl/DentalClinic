import test from 'node:test';
import assert from 'node:assert/strict';
import {
    imageTags,
    isCommitTag,
    tagMatchesCommit,
    planRetention
} from '../../scripts/vercel-vcr-retention.mjs';

function image(id, createdAt, tags = [], status = 'ready') {
    return { id, createdAt, tags, status };
}

test('commit tag helpers accept deployment tags and reject named operator tags', () => {
    const sha = '87a493a7c5985f900e32f85fdbdbb08ce1cb88e4';

    assert.equal(isCommitTag('87a493a7c598'), true);
    assert.equal(isCommitTag('production'), false);
    assert.equal(tagMatchesCommit('87a493a7c598', sha), true);
    assert.equal(tagMatchesCommit('deadbeef', sha), false);
    assert.deepEqual(imageTags({ tags: [' aabbccd ', '', null, 'stable'] }), ['aabbccd', 'stable']);
});

test('retention protects READY production image even when it is older than rollback reserve', () => {
    const productionSha = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';
    const images = [
        image('prod', '2026-09-01T00:00:00Z', ['aaaaaaaaaaaa']),
        image('1', '2026-09-02T00:00:00Z', ['111111111111']),
        image('2', '2026-09-03T00:00:00Z', ['222222222222']),
        image('3', '2026-09-04T00:00:00Z', ['333333333333']),
        image('4', '2026-09-05T00:00:00Z', ['444444444444']),
        image('5', '2026-09-06T00:00:00Z', ['555555555555']),
        image('6', '2026-09-07T00:00:00Z', ['666666666666'])
    ];

    const plan = planRetention(images, [productionSha], { keepNewest: 3, targetCount: 4 });

    assert.equal(plan.blocked, false);
    assert.equal(plan.projectedCount, 4);
    assert.equal(plan.protectedIds.includes('prod'), true);
    assert.equal(plan.toDelete.some(item => item.id === 'prod'), false);
    assert.deepEqual(plan.toDelete.map(item => item.id), ['1', '2', '3']);
});

test('retention never automatically deletes operator-named tags or preparing images', () => {
    const images = [
        image('manual', '2026-08-01T00:00:00Z', ['stable']),
        image('preparing', '2026-08-02T00:00:00Z', [], 'preparing'),
        image('old-commit', '2026-08-03T00:00:00Z', ['abcdef123456']),
        image('new-1', '2026-09-01T00:00:00Z', ['111111111111']),
        image('new-2', '2026-09-02T00:00:00Z', ['222222222222'])
    ];

    const plan = planRetention(images, [], { keepNewest: 1, targetCount: 2 });

    assert.deepEqual(plan.toDelete.map(item => item.id), ['old-commit', 'new-1']);
    assert.equal(plan.projectedCount, 3);
    assert.equal(plan.blocked, true);
    assert.equal(plan.protectedIds.includes('manual'), true);
    assert.equal(plan.protectedIds.includes('preparing'), true);
});

test('retention deletes oldest safe deployment/untagged records first', () => {
    const images = [
        image('untagged-old', '2026-08-01T00:00:00Z'),
        image('commit-old', '2026-08-02T00:00:00Z', ['abcdef123456']),
        image('new-1', '2026-09-01T00:00:00Z', ['111111111111']),
        image('new-2', '2026-09-02T00:00:00Z', ['222222222222']),
        image('new-3', '2026-09-03T00:00:00Z', ['333333333333'])
    ];

    const plan = planRetention(images, [], { keepNewest: 2, targetCount: 3 });

    assert.equal(plan.blocked, false);
    assert.deepEqual(plan.toDelete.map(item => item.id), ['untagged-old', 'commit-old']);
});

test('retention rejects impossible target smaller than newest rollback reserve', () => {
    assert.throws(
        () => planRetention([], [], { keepNewest: 4, targetCount: 3 }),
        /targetCount cannot be smaller than keepNewest/
    );
});
