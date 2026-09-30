const money = v => new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(v);
const escapeHtml = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '>': '&gt;', '<': '&lt;', '"': '&quot;', "'": '&#39;' }[c]));

async function load() {
    try {
        const [reportRes, leadsRes, feedRes] = await Promise.all([
            apiFetch('/api/reports/dashboard'),
            apiFetch('/api/leads'),
            apiFetch('/api/activityfeed/recent')
        ]);

        const [report, leads, feed] = await Promise.all([
            reportRes.json(),
            leadsRes.json(),
            feedRes.json()
        ]);

        const metricsElem = document.querySelector('#metrics');
        if (metricsElem) {
            metricsElem.innerHTML = [
                ['Total leads', report.totalLeads],
                ['Open pipeline', money(report.openPipeline)],
                ['Conversion', (report.conversionRate || 0) + '%'],
                ['Overdue tasks', report.overdueTasks]
            ].map(x => `<div class="col-sm-6 col-xl-3"><div class="card metric shadow-sm"><div class="card-body"><div class="text-muted small">${x[0]}</div><div class="value">${x[1]}</div></div></div></div>`).join('');
        }

        const leadsElem = document.querySelector('#leads');
        if (leadsElem) {
            leadsElem.innerHTML = leads.map(x => `<tr>
                <td>${escapeHtml(x.companyName)}</td>
                <td>${escapeHtml(x.contactName)}</td>
                <td>${money(x.value)}</td>
                <td><span class="badge text-bg-secondary">${escapeHtml(x.status)}</span></td>
                <td class="score text-primary fw-bold">${x.priorityScore}</td>
            </tr>`).join('');
        }

        const feedElem = document.querySelector('#feed');
        if (feedElem) {
            feedElem.innerHTML = feed.data && feed.data.length ? feed.data.map(x =>
                `<article class="timeline-item mb-2 pb-2 border-bottom">
                    <div class="fw-semibold">${escapeHtml(x.lead)} <span class="badge text-bg-light">${escapeHtml(x.type)}</span></div>
                    <div>${escapeHtml(x.message)}</div>
                    <div class="feed-time small text-muted">${new Date(x.occurredUtc).toLocaleString()} · ${escapeHtml(x.user)}</div>
                </article>`
            ).join('') : '<p class="text-muted mb-0">No activity yet.</p>';
        }
    } catch (err) {
        if (err.message !== 'Unauthorized') {
            console.error('Error loading dashboard data:', err);
        }
    }
}

const recalcBtn = document.querySelector('#recalculate');
if (recalcBtn) {
    recalcBtn.addEventListener('click', async e => {
        const button = e.currentTarget;
        button.disabled = true;
        try {
            await apiFetch('/api/leadscoring/recalculate-all', { method: 'POST' });
            await load();
        } catch (err) {
            console.error('Error recalculating scores:', err);
        } finally {
            button.disabled = false;
        }
    });
}

load();
