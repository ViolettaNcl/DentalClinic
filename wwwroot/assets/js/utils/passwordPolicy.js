/**
 * Shared password policy rules for frontend validation.
 * Keeps password requirements consistent across patient/admin flows.
 */

export const PASSWORD_POLICY = {
    minLength: 8,
    requireUppercase: true,
    requireLowercase: true,
    requireNumber: true,
    requireSpecial: true,
};

export function validatePassword(password = '') {
    const value = String(password);
    const errors = [];

    if (value.length < PASSWORD_POLICY.minLength) {
        errors.push('password_min_length');
    }
    if (PASSWORD_POLICY.requireUppercase && !/[A-Z]/.test(value)) {
        errors.push('password_uppercase');
    }
    if (PASSWORD_POLICY.requireLowercase && !/[a-z]/.test(value)) {
        errors.push('password_lowercase');
    }
    if (PASSWORD_POLICY.requireNumber && !/\d/.test(value)) {
        errors.push('password_number');
    }
    if (PASSWORD_POLICY.requireSpecial && !/[^A-Za-z0-9]/.test(value)) {
        errors.push('password_special');
    }

    return {
        valid: errors.length === 0,
        errors,
    };
}
