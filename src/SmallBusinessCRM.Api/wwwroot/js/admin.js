/**
 * Administration Controller Script
 * Handles CSV Import/Export, User Provisioning, Trash Restoration, and Audit Log Rendering.
 */

async function loadAdminData() {
    try {
        const [auditRes, trashRes] = await Promise.all([
            apiFetch('/api/admin/audit-logs'),
            apiFetch('/api/admin/trash')
        ]);

        const [auditLogs, trashItems] = await Promise.all([
            auditRes.json(),
            trashRes.json()
        ]);

        // Render Audit Logs
        const auditTbody = document.getElementById('auditTableBody');
        if (auditTbody) {
            if (auditLogs.length === 0) {
                auditTbody.innerHTML = `<tr><td colspan="5" class="py-6 text-center text-slate-400">No audit records found.</td></tr>`;
            } else {
                auditTbody.innerHTML = auditLogs.map(log => {
                    const changes = log.newValuesJson ? log.newValuesJson.replace(/[{}"\\]/g, ' ').trim() : 'N/A';
                    return `
                        <tr class="hover:bg-slate-50/60 dark:hover:bg-slate-800/40 transition-colors">
                            <td class="py-3 px-4 text-slate-500 dark:text-slate-400 font-mono text-[11px]">
                                ${new Date(log.occurredUtc).toLocaleString()}
                            </td>
                            <td class="py-3 px-4 font-semibold text-slate-800 dark:text-slate-200">
                                ${escapeHtml(log.actor || 'System')}
                            </td>
                            <td class="py-3 px-4">
                                <span class="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-brand-50 text-brand-700 dark:bg-brand-950/60 dark:text-brand-300 border border-brand-200 dark:border-brand-800">
                                    ${escapeHtml(log.action)}
                                </span>
                            </td>
                            <td class="py-3 px-4 font-medium text-slate-700 dark:text-slate-300">
                                ${escapeHtml(log.entityName)} <span class="text-slate-400">#${escapeHtml(log.entityId)}</span>
                            </td>
                            <td class="py-3 px-4 text-slate-500 dark:text-slate-400 text-[11px] max-w-xs truncate" title="${escapeHtml(changes)}">
                                ${escapeHtml(changes)}
                            </td>
                        </tr>
                    `;
                }).join('');
            }
        }

        // Render Trash Bin
        const trashContainer = document.getElementById('trashList');
        const trashCount = document.getElementById('trashCount');
        if (trashCount) trashCount.textContent = `${trashItems.length} deleted`;

        if (trashContainer) {
            if (trashItems.length === 0) {
                trashContainer.innerHTML = `<div class="py-6 text-center text-xs text-slate-400 bg-slate-50 dark:bg-slate-900/40 rounded-xl border border-dashed border-slate-200 dark:border-slate-800">No deleted leads in trash.</div>`;
            } else {
                trashContainer.innerHTML = trashItems.map(lead => `
                    <div class="flex items-center justify-between p-3 rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-sm">
                        <div>
                            <span class="font-bold text-xs text-slate-800 dark:text-slate-200 block">${escapeHtml(lead.companyName)}</span>
                            <span class="text-[11px] text-slate-500 dark:text-slate-400">${escapeHtml(lead.title)} &middot; Deleted ${lead.deletedAtUtc ? new Date(lead.deletedAtUtc).toLocaleDateString() : ''}</span>
                        </div>
                        <button type="button" onclick="restoreLead(${lead.id})" class="inline-flex items-center px-3 py-1.5 rounded-lg text-xs font-semibold bg-emerald-50 text-emerald-700 hover:bg-emerald-100 dark:bg-emerald-950/60 dark:text-emerald-300 dark:hover:bg-emerald-900/60 border border-emerald-200 dark:border-emerald-800 transition-colors">
                            <i class="fa-solid fa-trash-arrow-up mr-1.5"></i>Restore
                        </button>
                    </div>
                `).join('');
            }
        }

    } catch (err) {
        if (err.message !== 'Unauthorized') {
            showToast('Unable to load admin data.', 'error');
        }
    }
}

window.restoreLead = async function(leadId) {
    try {
        const res = await apiFetch(`/api/leads/${leadId}/restore`, { method: 'POST' });
        if (res.ok) {
            showToast('Lead successfully restored to pipeline!', 'success');
            await loadAdminData();
        } else {
            showToast('Unable to restore lead.', 'error');
        }
    } catch {
        showToast('Network error during lead restore.', 'error');
    }
};

// CSV Import Handler
const importBtn = document.getElementById('import');
if (importBtn) {
    importBtn.addEventListener('click', async () => {
        const fileInput = document.getElementById('csv');
        const file = fileInput?.files[0];
        const resultEl = document.getElementById('importResult');

        if (!file) {
            showToast('Please select a .csv file first.', 'warning');
            return;
        }

        if (file.size > 5 * 1024 * 1024) {
            showToast('File exceeds maximum size limit of 5 MB.', 'error');
            return;
        }

        if (resultEl) {
            resultEl.textContent = 'Processing and ingesting CSV...';
            resultEl.className = 'text-xs min-h-[18px] text-brand-600 dark:text-brand-400 font-medium';
        }

        const formData = new FormData();
        formData.append('file', file);

        try {
            const res = await apiFetch('/api/data/leads/import', { method: 'POST', body: formData });
            if (res.ok) {
                const data = await res.json();
                showToast(`Imported ${data.imported} leads (${data.skipped} skipped)`, 'success');
                if (resultEl) {
                    resultEl.textContent = `Completed: ${data.imported} imported, ${data.skipped} skipped.`;
                    resultEl.className = 'text-xs min-h-[18px] text-emerald-600 dark:text-emerald-400 font-medium';
                }
                if (fileInput) fileInput.value = '';
                await loadAdminData();
            } else {
                const err = await res.json().catch(() => ({ message: 'Import failed' }));
                showToast(err.message || 'Import failed.', 'error');
                if (resultEl) {
                    resultEl.textContent = `Error: ${err.message || 'Import failed'}`;
                    resultEl.className = 'text-xs min-h-[18px] text-rose-600 dark:text-rose-400 font-medium';
                }
            }
        } catch {
            showToast('Network error during CSV upload.', 'error');
            if (resultEl) {
                resultEl.textContent = 'Network error during upload.';
                resultEl.className = 'text-xs min-h-[18px] text-rose-600 dark:text-rose-400 font-medium';
            }
        }
    });
}

// User Provisioning Form Handler
const userForm = document.getElementById('userForm');
if (userForm) {
    userForm.addEventListener('submit', async (e) => {
        e.preventDefault();
        const f = new FormData(e.currentTarget);
        const data = Object.fromEntries(f);
        const resultEl = document.getElementById('userResult');

        try {
            const res = await apiFetch('/api/admin/users', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data)
            });

            if (res.ok) {
                showToast(`User ${data.email} created successfully!`, 'success');
                if (resultEl) {
                    resultEl.textContent = 'Account created successfully.';
                    resultEl.className = 'text-xs font-semibold text-emerald-600 dark:text-emerald-400';
                }
                e.currentTarget.reset();
                await loadAdminData();
            } else {
                const text = await res.text();
                showToast('Failed to create user account.', 'error');
                if (resultEl) {
                    resultEl.textContent = 'Failed to create user.';
                    resultEl.className = 'text-xs font-semibold text-rose-600 dark:text-rose-400';
                }
            }
        } catch {
            showToast('Network error during user creation.', 'error');
            if (resultEl) {
                resultEl.textContent = 'Network error creating account.';
                resultEl.className = 'text-xs font-semibold text-rose-600 dark:text-rose-400';
            }
        }
    });
}

document.getElementById('refreshAuditBtn')?.addEventListener('click', loadAdminData);

// Initial boot
loadAdminData();
