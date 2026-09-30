/**
 * Dashboard & Visual Analytics Engine using Chart.js.
 * Dynamically renders KPIs, Monthly Deal Velocity, Conversion Funnel, and Activity Breakdown.
 */

let velocityChartInstance = null;
let activityChartInstance = null;
let funnelChartInstance = null;

function isDarkMode() {
    return document.documentElement.classList.contains('dark');
}

function getChartThemeColors() {
    const dark = isDarkMode();
    return {
        text: dark ? '#94a3b8' : '#64748b',
        grid: dark ? 'rgba(51, 65, 85, 0.4)' : 'rgba(226, 232, 240, 0.8)',
        tooltipBg: dark ? '#1e293b' : '#ffffff',
        tooltipText: dark ? '#f8fafc' : '#0f172a',
        tooltipBorder: dark ? '#334155' : '#e2e8f0'
    };
}

function getStageBadge(status) {
    const badges = {
        New: 'bg-blue-100 text-blue-800 dark:bg-blue-950/60 dark:text-blue-300 border-blue-200 dark:border-blue-800',
        Contacted: 'bg-sky-100 text-sky-800 dark:bg-sky-950/60 dark:text-sky-300 border-sky-200 dark:border-sky-800',
        DemoScheduled: 'bg-indigo-100 text-indigo-800 dark:bg-indigo-950/60 dark:text-indigo-300 border-indigo-200 dark:border-indigo-800',
        ProposalSent: 'bg-amber-100 text-amber-800 dark:bg-amber-950/60 dark:text-amber-300 border-amber-200 dark:border-amber-800',
        ClosedWon: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300 border-emerald-200 dark:border-emerald-800',
        ClosedLost: 'bg-rose-100 text-rose-800 dark:bg-rose-950/60 dark:text-rose-300 border-rose-200 dark:border-rose-800'
    };
    const cls = badges[status] || 'bg-slate-100 text-slate-800 dark:bg-slate-800 dark:text-slate-300 border-slate-200';
    return `<span class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${cls}">${escapeHtml(status)}</span>`;
}

function getActivityIcon(type) {
    const icons = {
        Call: `<div class="w-8 h-8 rounded-lg bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center flex-shrink-0"><i class="fa-solid fa-phone text-xs"></i></div>`,
        Email: `<div class="w-8 h-8 rounded-lg bg-blue-100 dark:bg-blue-950/60 text-blue-600 dark:text-blue-400 flex items-center justify-center flex-shrink-0"><i class="fa-solid fa-envelope text-xs"></i></div>`,
        Meeting: `<div class="w-8 h-8 rounded-lg bg-purple-100 dark:bg-purple-950/60 text-purple-600 dark:text-purple-400 flex items-center justify-center flex-shrink-0"><i class="fa-solid fa-handshake text-xs"></i></div>`,
        Note: `<div class="w-8 h-8 rounded-lg bg-amber-100 dark:bg-amber-950/60 text-amber-600 dark:text-amber-400 flex items-center justify-center flex-shrink-0"><i class="fa-solid fa-note-sticky text-xs"></i></div>`,
        StatusChanged: `<div class="w-8 h-8 rounded-lg bg-brand-100 dark:bg-brand-950/60 text-brand-600 dark:text-brand-400 flex items-center justify-center flex-shrink-0"><i class="fa-solid fa-arrows-split-up-and-left text-xs"></i></div>`,
        System: `<div class="w-8 h-8 rounded-lg bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 flex items-center justify-center flex-shrink-0"><i class="fa-solid fa-gear text-xs"></i></div>`
    };
    return icons[type] || icons.System;
}

function renderVelocityChart(data) {
    const ctx = document.getElementById('velocityChart')?.getContext('2d');
    if (!ctx || !data || !Array.isArray(data)) return;

    if (velocityChartInstance) {
        velocityChartInstance.destroy();
    }

    const theme = getChartThemeColors();
    const labels = data.map(x => x.month);
    const created = data.map(x => x.createdCount);
    const won = data.map(x => x.wonCount);

    velocityChartInstance = new Chart(ctx, {
        type: 'bar',
        data: {
            labels,
            datasets: [
                {
                    label: 'Deals Created',
                    data: created,
                    backgroundColor: 'rgba(99, 102, 241, 0.75)',
                    borderColor: '#6366f1',
                    borderWidth: 1.5,
                    borderRadius: 6,
                    maxBarThickness: 32
                },
                {
                    label: 'Deals Won',
                    data: won,
                    backgroundColor: 'rgba(16, 185, 129, 0.85)',
                    borderColor: '#10b981',
                    borderWidth: 1.5,
                    borderRadius: 6,
                    maxBarThickness: 32
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'top',
                    labels: { color: theme.text, font: { size: 11, family: 'Inter' }, usePointStyle: true, boxWidth: 6 }
                },
                tooltip: {
                    backgroundColor: theme.tooltipBg,
                    titleColor: theme.tooltipText,
                    bodyColor: theme.tooltipText,
                    borderColor: theme.tooltipBorder,
                    borderWidth: 1,
                    padding: 10,
                    cornerRadius: 8
                }
            },
            scales: {
                x: {
                    grid: { display: false },
                    ticks: { color: theme.text, font: { size: 10, family: 'Inter' } }
                },
                y: {
                    grid: { color: theme.grid },
                    ticks: { color: theme.text, stepSize: 1, font: { size: 10, family: 'Inter' } },
                    beginAtZero: true
                }
            }
        }
    });
}

function renderActivityChart(data) {
    const ctx = document.getElementById('activityChart')?.getContext('2d');
    if (!ctx || !data || !Array.isArray(data)) return;

    if (activityChartInstance) {
        activityChartInstance.destroy();
    }

    const theme = getChartThemeColors();
    const labels = data.map(x => x.type);
    const counts = data.map(x => x.count);

    const colors = [
        '#6366f1', // Indigo
        '#10b981', // Emerald
        '#38bdf8', // Sky
        '#f59e0b', // Amber
        '#8b5cf6', // Purple
        '#94a3b8'  // Slate
    ];

    activityChartInstance = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels,
            datasets: [{
                data: counts,
                backgroundColor: colors.slice(0, counts.length),
                borderColor: isDarkMode() ? '#1e293b' : '#ffffff',
                borderWidth: 2,
                hoverOffset: 4
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: { color: theme.text, font: { size: 10, family: 'Inter' }, usePointStyle: true, boxWidth: 6, padding: 12 }
                },
                tooltip: {
                    backgroundColor: theme.tooltipBg,
                    titleColor: theme.tooltipText,
                    bodyColor: theme.tooltipText,
                    borderColor: theme.tooltipBorder,
                    borderWidth: 1,
                    padding: 10,
                    cornerRadius: 8
                }
            },
            cutout: '68%'
        }
    });
}

function renderFunnelChart(data) {
    const ctx = document.getElementById('funnelChart')?.getContext('2d');
    if (!ctx || !data || !Array.isArray(data)) return;

    if (funnelChartInstance) {
        funnelChartInstance.destroy();
    }

    const theme = getChartThemeColors();
    const labels = data.map(x => x.stage.replace(/([A-Z])/g, ' $1').trim());
    const counts = data.map(x => x.count);
    const values = data.map(x => x.value);

    funnelChartInstance = new Chart(ctx, {
        type: 'bar',
        data: {
            labels,
            datasets: [{
                label: 'Deals in Stage',
                data: counts,
                backgroundColor: [
                    'rgba(59, 130, 246, 0.85)',
                    'rgba(14, 165, 233, 0.85)',
                    'rgba(99, 102, 241, 0.85)',
                    'rgba(245, 158, 11, 0.85)',
                    'rgba(16, 185, 129, 0.85)'
                ],
                borderRadius: 6,
                maxBarThickness: 28
            }]
        },
        options: {
            indexAxis: 'y',
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: {
                    backgroundColor: theme.tooltipBg,
                    titleColor: theme.tooltipText,
                    bodyColor: theme.tooltipText,
                    borderColor: theme.tooltipBorder,
                    borderWidth: 1,
                    padding: 10,
                    cornerRadius: 8,
                    callbacks: {
                        afterLabel: (ctx) => `Value: ${money(values[ctx.dataIndex])}`
                    }
                }
            },
            scales: {
                x: {
                    grid: { color: theme.grid },
                    ticks: { color: theme.text, stepSize: 1, font: { size: 10, family: 'Inter' } },
                    beginAtZero: true
                },
                y: {
                    grid: { display: false },
                    ticks: { color: theme.text, font: { size: 11, family: 'Inter', weight: 500 } }
                }
            }
        }
    });
}

async function loadDashboard() {
    try {
        const [dashRes, analyticsRes, leadsRes, feedRes] = await Promise.all([
            apiFetch('/api/reports/dashboard'),
            apiFetch('/api/reports/analytics'),
            apiFetch('/api/leads'),
            apiFetch('/api/activityfeed/recent')
        ]);

        const [report, analytics, leads, feed] = await Promise.all([
            dashRes.json(),
            analyticsRes.json(),
            leadsRes.json(),
            feedRes.json()
        ]);

        // 1. KPI Cards
        const totalLeadsEl = document.getElementById('metricTotalLeads');
        const openPipelineEl = document.getElementById('metricOpenPipeline');
        const conversionEl = document.getElementById('metricConversion');
        const overdueEl = document.getElementById('metricOverdue');
        const overdueBadge = document.getElementById('overdueBadge');

        if (totalLeadsEl) totalLeadsEl.textContent = report.totalLeads ?? 0;
        if (openPipelineEl) openPipelineEl.textContent = money(report.openPipeline);
        if (conversionEl) conversionEl.textContent = `${report.conversionRate ?? 0}%`;
        if (overdueEl) overdueEl.textContent = report.overdueTasks ?? 0;

        if (overdueBadge) {
            if ((report.overdueTasks || 0) > 0) {
                overdueBadge.textContent = 'Action Required';
                overdueBadge.className = 'text-xs font-semibold text-rose-700 dark:text-rose-300 bg-rose-100 dark:bg-rose-950/60 px-2 py-0.5 rounded-full border border-rose-300 dark:border-rose-800 animate-pulse';
            } else {
                overdueBadge.textContent = 'Clear';
                overdueBadge.className = 'text-xs font-medium text-emerald-700 dark:text-emerald-300 bg-emerald-100 dark:bg-emerald-950/60 px-2 py-0.5 rounded-full';
            }
        }

        // 2. Charts
        renderVelocityChart(analytics.monthlyDealVelocity);
        renderActivityChart(analytics.salesActivityDistribution);
        renderFunnelChart(analytics.leadConversionFunnel);

        // 3. Prioritized Leads Table
        const tbody = document.getElementById('leadsTableBody');
        if (tbody) {
            if (leads.length === 0) {
                tbody.innerHTML = `<tr><td colspan="5" class="py-8 text-center text-slate-400">No leads found in pipeline.</td></tr>`;
            } else {
                tbody.innerHTML = leads.slice(0, 10).map(lead => {
                    const initials = (lead.contactName || lead.companyName || 'C')
                        .split(' ')
                        .map(n => n[0])
                        .slice(0, 2)
                        .join('')
                        .toUpperCase();

                    return `
                        <tr class="hover:bg-slate-50/80 dark:hover:bg-slate-800/40 transition-colors">
                            <td class="py-3 px-4 font-semibold text-slate-900 dark:text-white">
                                <div class="font-bold text-sm">${escapeHtml(lead.companyName)}</div>
                                <div class="text-[11px] text-slate-500 dark:text-slate-400 font-normal">${escapeHtml(lead.title)}</div>
                            </td>
                            <td class="py-3 px-4">
                                <div class="flex items-center space-x-2">
                                    <div class="w-6 h-6 rounded-full bg-brand-100 dark:bg-brand-900/60 text-brand-700 dark:text-brand-300 text-[10px] font-bold flex items-center justify-center">
                                        ${initials}
                                    </div>
                                    <span class="truncate max-w-[120px]">${escapeHtml(lead.contactName || 'N/A')}</span>
                                </div>
                            </td>
                            <td class="py-3 px-4 font-bold text-emerald-600 dark:text-emerald-400">
                                ${money(lead.value)}
                            </td>
                            <td class="py-3 px-4">
                                ${getStageBadge(lead.status)}
                            </td>
                            <td class="py-3 px-4 text-center">
                                <div class="inline-flex items-center space-x-1.5">
                                    <div class="w-12 bg-slate-200 dark:bg-slate-700 rounded-full h-1.5 overflow-hidden">
                                        <div class="h-full rounded-full ${lead.priorityScore >= 70 ? 'bg-emerald-500' : lead.priorityScore >= 40 ? 'bg-amber-500' : 'bg-slate-400'}" style="width: ${lead.priorityScore}%"></div>
                                    </div>
                                    <span class="font-extrabold text-xs text-brand-600 dark:text-brand-400">${lead.priorityScore}</span>
                                </div>
                            </td>
                        </tr>
                    `;
                }).join('');
            }
        }

        // 4. Activity Stream
        const feedContainer = document.getElementById('activityFeed');
        if (feedContainer) {
            const feedItems = feed.data || [];
            if (feedItems.length === 0) {
                feedContainer.innerHTML = `<p class="text-xs text-slate-400 py-6 text-center">No recent interactions recorded.</p>`;
            } else {
                feedContainer.innerHTML = feedItems.map(item => `
                    <div class="flex items-start space-x-3 text-xs pb-3 border-b border-slate-100 dark:border-slate-800/80 last:border-0 last:pb-0">
                        ${getActivityIcon(item.type)}
                        <div class="flex-1 min-w-0">
                            <div class="flex items-baseline justify-between mb-0.5">
                                <span class="font-bold text-slate-900 dark:text-white truncate">${escapeHtml(item.lead || 'General')}</span>
                                <time class="text-[10px] text-slate-400 flex-shrink-0" datetime="${item.occurredUtc}">
                                    ${new Date(item.occurredUtc).toLocaleDateString([], { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })}
                                </time>
                            </div>
                            <p class="text-slate-600 dark:text-slate-300 leading-snug line-clamp-2">${escapeHtml(item.message)}</p>
                            <span class="text-[10px] text-slate-400 mt-1 inline-block">By: ${escapeHtml(item.user || 'System')}</span>
                        </div>
                    </div>
                `).join('');
            }
        }

    } catch (err) {
        if (err.message !== 'Unauthorized') {
            showToast('Unable to refresh dashboard analytics.', 'error');
        }
    }
}

// Recalculate AI Lead Scores handler
const recalcBtn = document.getElementById('recalculate');
if (recalcBtn) {
    recalcBtn.addEventListener('click', async (e) => {
        const btn = e.currentTarget;
        const originalHtml = btn.innerHTML;
        btn.disabled = true;
        btn.innerHTML = `<i class="fa-solid fa-circle-notch fa-spin mr-2"></i>Recalculating...`;

        try {
            const res = await apiFetch('/api/leadscoring/recalculate-all', { method: 'POST' });
            if (res.ok) {
                showToast('AI Lead Scores successfully recalculated!', 'success');
                await loadDashboard();
            } else {
                showToast('Failed to recalculate scores.', 'error');
            }
        } catch {
            showToast('Network error during recalculation.', 'error');
        } finally {
            btn.disabled = false;
            btn.innerHTML = originalHtml;
        }
    });
}

// Listen for theme toggle to update Chart.js colors
window.addEventListener('themeChanged', () => {
    // Reload charts with new colors
    apiFetch('/api/reports/analytics')
        .then(res => res.json())
        .then(analytics => {
            renderVelocityChart(analytics.monthlyDealVelocity);
            renderActivityChart(analytics.salesActivityDistribution);
            renderFunnelChart(analytics.leadConversionFunnel);
        })
        .catch(() => {});
});

// Initial boot
loadDashboard();
