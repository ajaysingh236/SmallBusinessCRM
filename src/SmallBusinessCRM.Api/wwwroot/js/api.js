/**
 * Centralized API client for SmallBusinessCRM.
 * Handles JWT bearer authentication, 401 session expiration redirects, and conflict detection.
 */

function getAuthToken() {
    return localStorage.getItem('crm_jwt_token');
}

function setAuthSession(token, user) {
    if (token) {
        localStorage.setItem('crm_jwt_token', token);
    }
    if (user) {
        localStorage.setItem('crm_user', JSON.stringify(user));
    }
}

function clearAuthSession() {
    localStorage.removeItem('crm_jwt_token');
    localStorage.removeItem('crm_user');
}

function getCurrentUser() {
    try {
        const stored = localStorage.getItem('crm_user');
        return stored ? JSON.parse(stored) : null;
    } catch {
        return null;
    }
}

async function apiFetch(url, options = {}) {
    const headers = new Headers(options.headers || {});

    // Attach JWT Bearer token if present
    const token = getAuthToken();
    if (token && !headers.has('Authorization')) {
        headers.set('Authorization', `Bearer ${token}`);
    }

    // Default to JSON accept
    if (!headers.has('Accept')) {
        headers.set('Accept', 'application/json, text/plain, */*');
    }

    const config = {
        ...options,
        headers,
        credentials: options.credentials || 'include' // Allow cookie fallback alongside JWT
    };

    try {
        const response = await fetch(url, config);

        if (response.status === 401) {
            clearAuthSession();
            // Redirect to login with expired parameter
            if (!window.location.pathname.endsWith('login.html')) {
                window.location.assign('/login.html?expired=true');
            }
            throw new Error('Unauthorized');
        }

        return response;
    } catch (err) {
        if (err.message === 'Unauthorized') {
            throw err;
        }
        throw err;
    }
}
