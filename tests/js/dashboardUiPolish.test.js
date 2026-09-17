import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';

const ROOT = process.cwd();
const dashboardCss = fs.readFileSync(path.join(ROOT, 'wwwroot/assets/css/pages/dashboard.css'), 'utf8');
const patientHtml = fs.readFileSync(path.join(ROOT, 'wwwroot/pages/patient-dashboard.html'), 'utf8');
const patientJs = fs.readFileSync(path.join(ROOT, 'wwwroot/assets/js/managers/patient/patientDashboard.js'), 'utf8');
const patientEntry = fs.readFileSync(path.join(ROOT, 'wwwroot/assets/js/managers/patient/patientDashboardEntry.js'), 'utf8');
const authController = fs.readFileSync(path.join(ROOT, 'Controllers/AuthController.cs'), 'utf8');

test('dashboard login toast is pinned to the visible top-right corner, never the page bottom', () => {
    assert.match(dashboardCss, /\.panel-body \.toast-container,[\s\S]*?\.cabinet-body \.toast-container[\s\S]*?inset:\s*22px 22px auto auto !important/);
    assert.match(dashboardCss, /z-index:\s*10050/);
});

test('patient dashboard includes a responsive overview and a dedicated logout control', () => {
    assert.match(patientHtml, /class="patient-dashboard-overview"/);
    assert.match(patientHtml, /id="patient-overview-active"/);
    assert.match(patientHtml, /id="patient-overview-next"/);
    assert.match(patientHtml, /id="patient-overview-completed"/);
    assert.match(patientHtml, /id="btn-patient-logout"/);
    assert.match(patientEntry, /installPatientLogout/);
    assert.match(patientEntry, /terminateCookieSession/);
});

test('patient appointment rows carry localized mobile labels and become cards only in the patient cabinet', () => {
    assert.match(patientJs, /data-label="\$\{escapeHtml\(t\('table_doctor'/);
    assert.match(patientJs, /data-label="\$\{escapeHtml\(t\('table_status'/);
    assert.match(dashboardCss, /body\.cabinet-body \.panel-table thead\s*\{\s*display:\s*none/);
    assert.match(dashboardCss, /tr\[data-appointment-row\]/);
    assert.match(dashboardCss, /content:\s*attr\(data-label\)/);
});

test('logout is independent of a remote Somee DB round-trip', () => {
    const logoutStart = authController.indexOf('[HttpPost("logout")]');
    const sessionStart = authController.indexOf('[HttpGet("session")]', logoutStart);
    assert.ok(logoutStart >= 0 && sessionStart > logoutStart);
    const logout = authController.slice(logoutStart, sessionStart);
    assert.match(logout, /DeleteSessionCookie\(\);/);
    assert.match(logout, /JwtTokenService\.TokenVersionClaim/);
    assert.match(logout, /_tokenVersionCache\?\.Set/);
    assert.doesNotMatch(logout, /_db\.|FindAsync|SaveChangesAsync|AnyAsync|FirstOrDefaultAsync/);
    assert.match(logout, /return Ok\(new \{ message = "Выход выполнен" \}\)/);
});
