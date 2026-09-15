const EMPTY_STATE_CHART_IDS = new Set([
    'chart-doctors',
    'chart-reviews-rating',
    'chart-chat-topics',
]);

let installed = false;
let observer = null;
let chartPatchTimer = null;

function canvasesIn(node) {
    if (!node || node.nodeType !== 1) return [];
    const result = [];
    if (node.matches?.('canvas[id]')) result.push(node);
    result.push(...(node.querySelectorAll?.('canvas[id]') || []));
    return result.filter(canvas => EMPTY_STATE_CHART_IDS.has(canvas.id));
}

function restoreRemovedCanvas(record) {
    const target = record.target;
    if (!target?.classList?.contains('analytics-chart-wrap')) return;
    if (!target.querySelector('.analytics-chart-empty')) return;

    for (const removed of record.removedNodes || []) {
        for (const canvas of canvasesIn(removed)) {
            // AnalyticsManager keeps references to these exact canvas elements.
            // Reattach the same node instead of creating a replacement so a later
            // data refresh can render into the cached reference successfully.
            canvas.hidden = true;
            canvas.style.display = 'none';
            canvas.dataset.emptyStatePreserved = 'true';
            target.appendChild(canvas);
        }
    }
}

function prepareCanvasForChart(input) {
    const canvas = input?.canvas || input;
    if (!canvas || !EMPTY_STATE_CHART_IDS.has(canvas.id)) return;

    const wrap = canvas.closest?.('.analytics-chart-wrap');
    if (!wrap) return;

    wrap.querySelectorAll('.analytics-chart-empty').forEach(empty => empty.remove());
    canvas.hidden = false;
    canvas.style.removeProperty('display');
    delete canvas.dataset.emptyStatePreserved;
}

function patchChartConstructor(attempt = 0) {
    const NativeChart = window.Chart;
    if (!NativeChart) {
        if (attempt >= 80) return;
        chartPatchTimer = window.setTimeout(() => patchChartConstructor(attempt + 1), 100);
        return;
    }
    if (NativeChart.__analyticsCanvasGuard === true) return;

    const GuardedChart = new Proxy(NativeChart, {
        construct(target, args) {
            prepareCanvasForChart(args[0]);
            return Reflect.construct(target, args, target);
        }
    });

    Object.defineProperty(GuardedChart, '__analyticsCanvasGuard', {
        value: true,
        configurable: false,
        enumerable: false,
        writable: false,
    });

    window.Chart = GuardedChart;
}

/**
 * Protects analytics canvases from the legacy empty-state renderer in
 * adminDashboard.js, which replaces the whole wrapper with innerHTML. Without
 * this guard the original canvas is permanently detached and charts cannot
 * recover when data appears later in the same admin session.
 */
export function installAdminAnalyticsCanvasGuard() {
    if (installed || typeof document === 'undefined' || typeof window === 'undefined') return;
    installed = true;

    const target = document.getElementById('section-analytics') || document.body;
    if (target && typeof MutationObserver !== 'undefined') {
        observer = new MutationObserver(records => records.forEach(restoreRemovedCanvas));
        observer.observe(target, { childList: true, subtree: true });
    }

    patchChartConstructor();
}

export function uninstallAdminAnalyticsCanvasGuardForTests() {
    observer?.disconnect();
    observer = null;
    if (chartPatchTimer) window.clearTimeout(chartPatchTimer);
    chartPatchTimer = null;
    installed = false;
}

export { EMPTY_STATE_CHART_IDS, prepareCanvasForChart };
