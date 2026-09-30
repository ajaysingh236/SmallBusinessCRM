/**
 * Centralized API & UI Utilities for SmallBusinessCRM.
 * Handles JWT bearer authentication, session expiration, theme toggling, and accessible notifications.
 */

function getAuthToken() {
    return localStorage.getItem('crm_jwt_token');
}

function setAuthSession(token, user) {
    if (token) localStorage.setItem('crm_jwt_token', token);
    if (user) localStorage.setItem('crm_user', JSON.stringify(user));
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
    const token = getAuthToken();
    if (token && !headers.has('Authorization')) {
        headers.set('Authorization', `Bearer ${token}`);
    }
    if (!headers.has('Accept')) {
        headers.set('Accept', 'application/json, text/plain, */*');
    }

    const config = {
        ...options,
        headers,
        credentials: options.credentials || 'include'
    };

    try {
        const response = await fetch(url, config);

        if (response.status === 401) {
            clearAuthSession();
            if (!window.location.pathname.endsWith('login.html')) {
                window.location.assign('/login.html?expired=true');
            }
            throw new Error('Unauthorized');
        }

        return response;
    } catch (err) {
        if (err.message === 'Unauthorized') throw err;
        throw err;
    }
}

const money = n => new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(n || 0);

const escapeHtml = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '>': '&gt;', '<': '&lt;', '"': '&quot;', "'": '&#39;' }[c]));

/**
 * Toast notification dispatcher
 */
function showToast(message, type = 'info', title = '') {
    let container = document.getElementById('toast-container');
    if (!container) {
        container = document.createElement('div');
        container.id = 'toast-container';
        container.className = 'fixed bottom-5 right-5 z-50 flex flex-col space-y-3 max-w-sm w-full pointer-events-none';
        container.setAttribute('aria-live', 'polite');
        container.setAttribute('aria-atomic', 'true');
        document.body.appendChild(container);
    }

    const colorClasses = {
        success: 'bg-emerald-50 text-emerald-900 border-emerald-300 dark:bg-emerald-950/80 dark:text-emerald-200 dark:border-emerald-800',
        error: 'bg-rose-50 text-rose-900 border-rose-300 dark:bg-rose-950/80 dark:text-rose-200 dark:border-rose-800',
        warning: 'bg-amber-50 text-amber-900 border-amber-300 dark:bg-amber-950/80 dark:text-amber-200 dark:border-amber-800',
        info: 'bg-indigo-50 text-indigo-900 border-indigo-300 dark:bg-indigo-950/80 dark:text-indigo-200 dark:border-indigo-800'
    }[type] || 'bg-slate-50 text-slate-900 border-slate-300 dark:bg-slate-900 dark:text-slate-100 dark:border-slate-700';

    const icons = {
        success: `<svg class="w-5 h-5 text-emerald-500 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd"/></svg>`,
        error: `<svg class="w-5 h-5 text-rose-500 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd"/></svg>`,
        warning: `<svg class="w-5 h-5 text-amber-500 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clip-rule="evenodd"/></svg>`,
        info: `<svg class="w-5 h-5 text-indigo-500 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd"/></svg>`
    }[type] || '';

    const toast = document.createElement('div');
    toast.className = `toast-notice pointer-events-auto p-4 rounded-xl shadow-lg border backdrop-blur-md flex items-start space-x-3 transition-all duration-300 ${colorClasses}`;
    toast.innerHTML = `
        ${icons}
        <div class="flex-1 min-w-0">
            ${title ? `<p class="text-sm font-semibold mb-0.5">${escapeHtml(title)}</p>` : ''}
            <p class="text-xs leading-relaxed opacity-90">${escapeHtml(message)}</p>
        </div>
        <button type="button" class="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 transition-colors p-1" aria-label="Close">
            <svg class="w-4 h-4" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd"/></svg>
        </button>
    `;

    toast.querySelector('button').addEventListener('click', () => {
        toast.remove();
    });

    container.appendChild(toast);
    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transform = 'translateY(10px)';
        setTimeout(() => toast.remove(), 300);
    }, 4500);
}

// Backwards compatibility alias
const toast = (text) => showToast(text, 'info');

/**
 * Initializes Dark Mode Toggle and theme persistence
 */
function initThemeToggle() {
    const toggleBtn = document.getElementById('themeToggle');
    if (!toggleBtn) return;

    function renderThemeIcon(isDark) {
        toggleBtn.innerHTML = isDark
            ? `<svg class="w-5 h-5 text-amber-400" fill="currentColor" viewBox="0 0 20 20" aria-hidden="true"><path fill-rule="evenodd" d="M10 2a1 1 0 011 1v1a1 1 0 11-2 0V3a1 1 0 011-1zm4 8a4 4 0 11-8 0 4 4 0 018 0zm-.464 4.95l.707.707a1 1 0 001.414-1.414l-.707-.707a1 1 0 00-1.414 1.414zm2.12-10.607a1 1 0 010 1.414l-.706.707a1 1 0 11-1.414-1.414l.707-.707a1 1 0 011.414 0zM17 11a1 1 0 100-2h-1a1 1 0 100 2h1zm-7 4a1 1 0 011 1v1a1 1 0 11-2 0v-1a1 1 0 011-1zM5.05 6.464A1 1 0 106.465 5.05l-.708-.707a1 1 0 00-1.414 1.414l.707.707zm1.414 8.486l-.707.707a1 1 0 01-1.414-1.414l.707-.707a1 1 0 011.414 1.414zM4 11a1 1 0 100-2H3a1 1 0 000 2h1z" clip-rule="evenodd"/></svg>`
            : `<svg class="w-5 h-5 text-slate-600 dark:text-slate-300" fill="currentColor" viewBox="0 0 20 20" aria-hidden="true"><path d="M17.293 13.293A8 8 0 016.707 2.707a8.001 8.001 0 1010.586 10.586z"/></svg>`;
        toggleBtn.setAttribute('title', isDark ? 'Switch to Light Mode' : 'Switch to Dark Mode');
        toggleBtn.setAttribute('aria-label', isDark ? 'Switch to Light Mode' : 'Switch to Dark Mode');
    }

    const currentDark = document.documentElement.classList.contains('dark');
    renderThemeIcon(currentDark);

    toggleBtn.addEventListener('click', () => {
        const nextDark = document.documentElement.classList.toggle('dark');
        localStorage.setItem('crm_theme', nextDark ? 'dark' : 'light');
        renderThemeIcon(nextDark);
        window.dispatchEvent(new CustomEvent('themeChanged', { detail: { isDark: nextDark } }));
    });
}

document.addEventListener('DOMContentLoaded', () => {
    initThemeToggle();
    const logoutBtn = document.getElementById('logoutBtn');
    if (logoutBtn) {
        logoutBtn.addEventListener('click', async (e) => {
            e.preventDefault();
            try {
                await apiFetch('/account/logout', { method: 'POST' });
            } catch {}
            clearAuthSession();
            window.location.assign('/login.html');
        });
    }
});
