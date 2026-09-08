#!/usr/bin/env node

import { pathToFileURL } from 'node:url';

const API_BASE = 'https://api.vercel.com';

function asPositiveInteger(value, fallback, name) {
    if (value == null || value === '') return fallback;
    const parsed = Number.parseInt(String(value), 10);
    if (!Number.isInteger(parsed) || parsed < 1)
        throw new Error(`${name} must be a positive integer`);
    return parsed;
}

export function imageTags(image) {
    return Array.isArray(image?.tags)
        ? image.tags.filter(tag => typeof tag === 'string' && tag.trim()).map(tag => tag.trim())
        : [];
}

export function isCommitTag(tag) {
    return /^[0-9a-f]{7,40}$/i.test(String(tag || ''));
}

export function tagMatchesCommit(tag, sha) {
    const normalizedTag = String(tag || '').toLowerCase();
    const normalizedSha = String(sha || '').toLowerCase();
    if (!isCommitTag(normalizedTag) || !/^[0-9a-f]{40}$/i.test(normalizedSha)) return false;
    return normalizedSha.startsWith(normalizedTag) || normalizedTag.startsWith(normalizedSha);
}

function createdAtMillis(image) {
    const value = Date.parse(image?.createdAt || '');
    return Number.isFinite(value) ? value : 0;
}

function imageIsPreparing(image) {
    const status = String(image?.status || '').toLowerCase();
    return status && !['ready', 'available', 'complete', 'completed'].includes(status);
}

export function planRetention(
    images,
    productionShas,
    { keepNewest = 4, targetCount = 5 } = {}) {
    const normalized = Array.isArray(images) ? images.filter(image => image?.id) : [];
    const shas = (Array.isArray(productionShas) ? productionShas : [])
        .map(sha => String(sha || '').toLowerCase())
        .filter(sha => /^[0-9a-f]{40}$/.test(sha));

    if (targetCount < keepNewest)
        throw new Error('targetCount cannot be smaller than keepNewest');

    const newestFirst = [...normalized].sort((a, b) => createdAtMillis(b) - createdAtMillis(a));
    const protectedIds = new Set(newestFirst.slice(0, keepNewest).map(image => image.id));
    const reasons = new Map();

    for (const image of newestFirst.slice(0, keepNewest))
        reasons.set(image.id, 'newest rollback reserve');

    for (const image of normalized) {
        const tags = imageTags(image);

        if (imageIsPreparing(image)) {
            protectedIds.add(image.id);
            reasons.set(image.id, 'image is not in a stable ready state');
            continue;
        }

        if (tags.some(tag => shas.some(sha => tagMatchesCommit(tag, sha)))) {
            protectedIds.add(image.id);
            reasons.set(image.id, 'READY production deployment');
            continue;
        }

        // A non-commit tag is assumed to be operator-managed (for example stable,
        // latest, or a named rollback). Never delete those automatically.
        if (tags.some(tag => !isCommitTag(tag))) {
            protectedIds.add(image.id);
            reasons.set(image.id, 'operator-managed tag');
        }
    }

    const candidates = normalized
        .filter(image => !protectedIds.has(image.id))
        .filter(image => {
            const tags = imageTags(image);
            return tags.length === 0 || tags.every(isCommitTag);
        })
        .sort((a, b) => createdAtMillis(a) - createdAtMillis(b));

    const deleteCount = Math.max(0, normalized.length - targetCount);
    const toDelete = candidates.slice(0, deleteCount);
    const projectedCount = normalized.length - toDelete.length;

    return {
        total: normalized.length,
        keepNewest,
        targetCount,
        protectedIds: [...protectedIds],
        protectedReasons: Object.fromEntries(reasons),
        toDelete,
        projectedCount,
        blocked: projectedCount > targetCount
    };
}

function parseArgs(argv) {
    const args = {
        repository: process.env.VERCEL_VCR_REPOSITORY || 'web',
        projectId: process.env.VERCEL_PROJECT_ID || '',
        teamId: process.env.VERCEL_TEAM_ID || '',
        keepNewest: 4,
        targetCount: 5,
        dryRun: false
    };

    for (let i = 0; i < argv.length; i += 1) {
        const arg = argv[i];
        if (arg === '--dry-run') args.dryRun = true;
        else if (arg === '--repository') args.repository = argv[++i];
        else if (arg === '--project-id') args.projectId = argv[++i];
        else if (arg === '--team-id') args.teamId = argv[++i];
        else if (arg === '--keep-newest') args.keepNewest = asPositiveInteger(argv[++i], 4, 'keepNewest');
        else if (arg === '--target-count') args.targetCount = asPositiveInteger(argv[++i], 5, 'targetCount');
        else if (arg === '--help') {
            args.help = true;
        } else {
            throw new Error(`Unknown argument: ${arg}`);
        }
    }

    return args;
}

function helpText() {
    return `Usage: node scripts/vercel-vcr-retention.mjs [options]\n\n` +
        `Required environment: VERCEL_TOKEN\n` +
        `Options:\n` +
        `  --repository <name>     VCR repository (default: web)\n` +
        `  --project-id <id>       Vercel project ID\n` +
        `  --team-id <id>          Vercel team ID\n` +
        `  --keep-newest <n>       Always preserve newest N images (default: 4)\n` +
        `  --target-count <n>      Reduce total images to N when safe (default: 5)\n` +
        `  --dry-run               Print plan without deleting images\n`;
}

async function vercelRequest(path, { token, method = 'GET' } = {}) {
    const response = await fetch(`${API_BASE}${path}`, {
        method,
        headers: {
            Authorization: `Bearer ${token}`,
            Accept: 'application/json',
            'Content-Type': 'application/json'
        }
    });

    const raw = await response.text();
    let body = null;
    if (raw) {
        try { body = JSON.parse(raw); }
        catch { body = raw; }
    }

    if (!response.ok) {
        const detail = typeof body === 'string'
            ? body
            : body?.error?.message || body?.message || JSON.stringify(body);
        throw new Error(`Vercel API ${method} ${path} failed (${response.status}): ${detail}`);
    }

    return body;
}

async function listAllImages({ token, projectId, teamId, repository }) {
    const images = [];
    let cursor = null;

    do {
        const params = new URLSearchParams({ projectId, teamId, limit: '100' });
        if (cursor) params.set('cursor', cursor);
        const result = await vercelRequest(
            `/v1/vcr/repository/${encodeURIComponent(repository)}/images?${params}`,
            { token }
        );
        images.push(...(Array.isArray(result?.images) ? result.images : []));
        cursor = result?.nextCursor || null;
    } while (cursor);

    return images;
}

async function listReadyProductionShas({ token, projectId, teamId }) {
    const params = new URLSearchParams({
        projectId,
        teamId,
        target: 'production',
        state: 'READY',
        limit: '20'
    });
    const result = await vercelRequest(`/v7/deployments?${params}`, { token });
    return (Array.isArray(result?.deployments) ? result.deployments : [])
        .map(deployment => deployment?.meta?.githubCommitSha)
        .filter(sha => typeof sha === 'string' && /^[0-9a-f]{40}$/i.test(sha));
}

async function deleteImage({ token, projectId, teamId, repository, imageId }) {
    const params = new URLSearchParams({ projectId, teamId });
    await vercelRequest(
        `/v1/vcr/repository/${encodeURIComponent(repository)}/images/${encodeURIComponent(imageId)}?${params}`,
        { token, method: 'DELETE' }
    );
}

export async function runRetention({
    token,
    projectId,
    teamId,
    repository = 'web',
    keepNewest = 4,
    targetCount = 5,
    dryRun = false
}) {
    if (!token) throw new Error('VERCEL_TOKEN is required');
    if (!projectId) throw new Error('VERCEL_PROJECT_ID is required');
    if (!teamId) throw new Error('VERCEL_TEAM_ID is required');
    if (!repository) throw new Error('VCR repository name is required');

    const [images, productionShas] = await Promise.all([
        listAllImages({ token, projectId, teamId, repository }),
        listReadyProductionShas({ token, projectId, teamId })
    ]);

    // Fail closed: without a known READY production commit we cannot prove that an
    // older image is safe to remove from rollback/restart history.
    if (images.length > targetCount && productionShas.length === 0) {
        throw new Error(
            'Refusing cleanup: no READY production Git commit could be identified. Review Vercel deployment state manually.'
        );
    }

    const plan = planRetention(images, productionShas, { keepNewest, targetCount });
    console.log(`VCR ${repository}: ${plan.total} images; target ${targetCount}; ${plan.toDelete.length} safe deletion(s).`);

    for (const image of plan.toDelete) {
        const tags = imageTags(image);
        console.log(`${dryRun ? '[dry-run] would delete' : 'deleting'} ${image.id} ${tags.length ? `tags=${tags.join(',')}` : 'untagged'} created=${image.createdAt || 'unknown'}`);
        if (!dryRun)
            await deleteImage({ token, projectId, teamId, repository, imageId: image.id });
    }

    if (plan.blocked) {
        throw new Error(
            `Retention stopped safely at projected ${plan.projectedCount} images; target ${targetCount} cannot be reached without deleting protected/operator-managed images.`
        );
    }

    return plan;
}

async function main() {
    const args = parseArgs(process.argv.slice(2));
    if (args.help) {
        console.log(helpText());
        return;
    }

    await runRetention({
        token: process.env.VERCEL_TOKEN,
        projectId: args.projectId,
        teamId: args.teamId,
        repository: args.repository,
        keepNewest: args.keepNewest,
        targetCount: args.targetCount,
        dryRun: args.dryRun
    });
}

const invokedPath = process.argv[1] ? pathToFileURL(process.argv[1]).href : null;
if (invokedPath === import.meta.url) {
    main().catch(error => {
        console.error(`VCR retention failed: ${error?.message || error}`);
        process.exitCode = 1;
    });
}
