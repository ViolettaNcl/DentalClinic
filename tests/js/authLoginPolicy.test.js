import test from 'node:test';
import assert from 'node:assert/strict';
import { shouldTryAdminFallback } from '../../wwwroot/assets/js/managers/auth/authLoginPolicy.js';

test('admin fallback is attempted only after an explicit 401', () => {
  assert.equal(shouldTryAdminFallback({ status: 401 }), true);
  assert.equal(shouldTryAdminFallback({ status: 400 }), false);
  assert.equal(shouldTryAdminFallback({ status: 429 }), false);
  assert.equal(shouldTryAdminFallback({ status: 500 }), false);
  assert.equal(shouldTryAdminFallback(new Error('network down')), false);
  assert.equal(shouldTryAdminFallback(null), false);
});
