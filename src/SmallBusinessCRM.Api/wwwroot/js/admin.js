const q = s => document.querySelector(s);
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '>': '&gt;', '<': '&lt;', '"': '&quot;', "'": '&#39;' }[c]));

async function refresh() {
    try {
        const [auditRes, trashRes] = await Promise.all([
            apiFetch('/api/admin/audit-logs'),
            apiFetch('/api/admin/trash')
        ]);

        const [audit, trash] = await Promise.all([
            auditRes.json(),
            trashRes.json()
        ]);

        q('#audit').innerHTML = audit.map(x =>
            `<tr>
                <td>${new Date(x.occurredUtc).toLocaleString()}</td>
                <td>${esc(x.actor)}</td>
                <td>${esc(x.action)}</td>
                <td>${esc(x.entityName)} #${esc(x.entityId)}</td>
            </tr>`
        ).join('');

        q('#trash').innerHTML = trash.length ? trash.map(x =>
            `<div class="border-bottom py-2 d-flex justify-content-between align-items-center">
                <span>${esc(x.title)}</span>
                <button class="btn btn-sm btn-outline-primary" onclick="restoreLead(${x.id})">Restore</button>
            </div>`
        ).join('') : '<div class="text-muted">No deleted leads.</div>';
    } catch (err) {
        if (err.message !== 'Unauthorized') {
            console.error('Error refreshing admin panel:', err);
        }
    }
}

window.restoreLead = async function(id) {
    try {
        const r = await apiFetch(`/api/leads/${id}/restore`, { method: 'POST' });
        if (r.ok) {
            await refresh();
        } else {
            alert('Unable to restore lead.');
        }
    } catch (err) {
        console.error('Failed to restore lead:', err);
    }
};

const importBtn = q('#import');
if (importBtn) {
    importBtn.onclick = async () => {
        const fileInput = q('#csv');
        const f = fileInput.files[0];
        if (!f) {
            alert('Please choose a CSV file first.');
            return;
        }

        if (f.size > 5 * 1024 * 1024) {
            alert('File exceeds 5MB size limit.');
            return;
        }

        q('#importResult').textContent = 'Importing…';
        const fd = new FormData();
        fd.append('file', f);

        try {
            const r = await apiFetch('/api/data/leads/import', { method: 'POST', body: fd });
            if (r.ok) {
                const res = await r.json();
                q('#importResult').textContent = `Successfully imported ${res.imported} leads (${res.skipped} skipped).`;
                fileInput.value = '';
                await refresh();
            } else {
                const err = await r.json().catch(() => ({ message: 'Import failed.' }));
                q('#importResult').textContent = `Import failed: ${err.message || 'Unknown error'}`;
            }
        } catch {
            q('#importResult').textContent = 'Network error during import.';
        }
    };
}

const userForm = q('#userForm');
if (userForm) {
    userForm.onsubmit = async e => {
        e.preventDefault();
        const f = new FormData(e.currentTarget);
        const data = Object.fromEntries(f);
        try {
            const r = await apiFetch('/api/admin/users', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data)
            });

            if (r.ok) {
                q('#userResult').className = 'small mt-2 text-success';
                q('#userResult').textContent = 'User created successfully.';
                e.currentTarget.reset();
                await refresh();
            } else {
                q('#userResult').className = 'small mt-2 text-danger';
                q('#userResult').textContent = `Unable to create user: ${await r.text()}`;
            }
        } catch {
            q('#userResult').className = 'small mt-2 text-danger';
            q('#userResult').textContent = 'Network error while creating user.';
        }
    };
}

refresh();
