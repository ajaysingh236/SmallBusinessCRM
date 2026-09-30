/**
 * SortableJS Kanban Pipeline with Live Deal Value Totals, Overdue Badges,
 * Optimistic Updates, Concurrency Rollback, and SignalR Real-Time Synchronization.
 */

const STAGES = [
    { key: 'New', label: 'New Lead', color: 'bg-blue-500', headerBg: 'border-t-blue-500' },
    { key: 'Contacted', label: 'Contacted', color: 'bg-sky-500', headerBg: 'border-t-sky-500' },
    { key: 'DemoScheduled', label: 'Demo Scheduled', color: 'bg-indigo-500', headerBg: 'border-t-indigo-500' },
    { key: 'ProposalSent', label: 'Proposal Sent', color: 'bg-amber-500', headerBg: 'border-t-amber-500' },
    { key: 'ClosedWon', label: 'Closed Won', color: 'bg-emerald-500', headerBg: 'border-t-emerald-500' },
    { key: 'ClosedLost', label: 'Closed Lost', color: 'bg-rose-500', headerBg: 'border-t-rose-500' }
];

let allLeads = [];
let sortableInstances = [];
let pendingMoveLead = null;

function hasOverdueTasks(lead) {
    if (!lead.tasks || !Array.isArray(lead.tasks) || lead.tasks.length === 0) return false;
    const now = new Date();
    return lead.tasks.some(t => !t.isCompleted && new Date(t.dueDateUtc) < now);
}

function getPriorityBadgeClass(score) {
    if (score >= 70) return 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-300 border-emerald-300 dark:border-emerald-800';
    if (score >= 40) return 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300 border-amber-300 dark:border-amber-800';
    return 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300 border-slate-200 dark:border-slate-700';
}

function createLeadCardElement(lead) {
    const isOverdue = hasOverdueTasks(lead);
    const card = document.createElement('article');
    card.className = 'lead-card relative group focus:outline-none';
    card.setAttribute('tabindex', '0');
    card.setAttribute('role', 'listitem');
    card.dataset.id = lead.id;
    card.dataset.value = lead.value || 0;
    card.dataset.stage = lead.status;
    card.setAttribute('aria-label', `${lead.title}, Company: ${lead.companyName}, Value: ${money(lead.value)}, Stage: ${lead.status}`);

    card.innerHTML = `
        <div class="flex items-start justify-between gap-2 mb-1.5">
            <span class="text-xs font-semibold tracking-wide text-brand-600 dark:text-brand-400 truncate max-w-[170px]" title="${escapeHtml(lead.companyName)}">
                ${escapeHtml(lead.companyName)}
            </span>
            <button type="button" class="stage-move-btn opacity-0 group-hover:opacity-100 group-focus:opacity-100 transition-opacity p-1 text-slate-400 hover:text-brand-500 rounded" title="Move deal stage" aria-label="Change stage for ${escapeHtml(lead.companyName)}">
                <i class="fa-solid fa-ellipsis-vertical text-xs"></i>
            </button>
        </div>

        <h3 class="text-sm font-bold text-slate-900 dark:text-white leading-snug mb-2 line-clamp-2">
            ${escapeHtml(lead.title)}
        </h3>

        <div class="flex items-center justify-between text-xs mb-3">
            <span class="font-extrabold text-emerald-600 dark:text-emerald-400 text-sm">
                ${money(lead.value)}
            </span>
            <span class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${getPriorityBadgeClass(lead.priorityScore)}">
                <i class="fa-solid fa-bolt-lightning text-[10px] mr-1"></i>Score ${lead.priorityScore}
            </span>
        </div>

        ${isOverdue ? `
            <div class="mb-2.5 inline-flex items-center px-2 py-0.5 rounded-md text-[11px] font-medium bg-rose-50 text-rose-700 dark:bg-rose-950/60 dark:text-rose-300 border border-rose-200 dark:border-rose-800">
                <i class="fa-solid fa-clock-rotate-left mr-1.5 text-rose-500 animate-pulse"></i>Overdue Follow-up
            </div>
        ` : ''}

        <div class="pt-2 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between text-[11px] text-slate-500 dark:text-slate-400">
            <span class="truncate max-w-[130px] flex items-center">
                <i class="fa-regular fa-user mr-1.5 text-slate-400"></i>${escapeHtml(lead.contactName || 'No contact')}
            </span>
            <span class="truncate max-w-[110px] text-right font-medium">
                ${escapeHtml(lead.assignedUser?.fullName || 'Unassigned')}
            </span>
        </div>
    `;

    // Keyboard support and move button
    const moveBtn = card.querySelector('.stage-move-btn');
    const openModal = (e) => {
        e.stopPropagation();
        openStageModal(lead);
    };
    moveBtn?.addEventListener('click', openModal);

    card.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            openStageModal(lead);
        }
    });

    return card;
}

function recalculateColumnTotals() {
    let grandTotal = 0;
    let totalDeals = 0;

    STAGES.forEach(stage => {
        const dropZone = document.getElementById(`drop-${stage.key}`);
        if (!dropZone) return;

        const cards = Array.from(dropZone.querySelectorAll('.lead-card'));
        const count = cards.length;
        const totalValue = cards.reduce((sum, el) => sum + Number(el.dataset.value || 0), 0);

        grandTotal += totalValue;
        totalDeals += count;

        const countBadge = document.getElementById(`count-${stage.key}`);
        const sumBadge = document.getElementById(`sum-${stage.key}`);

        if (countBadge) countBadge.textContent = count;
        if (sumBadge) sumBadge.textContent = money(totalValue);
    });

    const grandTotalEl = document.getElementById('totalPipelineSum');
    const totalCountEl = document.getElementById('totalDealsCount');
    if (grandTotalEl) grandTotalEl.textContent = money(grandTotal);
    if (totalCountEl) totalCountEl.textContent = `${totalDeals} deal${totalDeals === 1 ? '' : 's'}`;
}

async function handleStageChange(leadId, targetStage, originalDropZone, originalIndex, cardElement) {
    try {
        const response = await apiFetch(`/api/leads/${leadId}/status`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ status: targetStage })
        });

        if (response.ok) {
            cardElement.dataset.stage = targetStage;
            recalculateColumnTotals();
            showToast(`Deal moved to ${targetStage}`, 'success');
        } else if (response.status === 409) {
            // Concurrency conflict: rollback
            showToast('Conflict: Lead was modified concurrently. Reverting position...', 'warning');
            rollbackCard(cardElement, originalDropZone, originalIndex);
            await loadPipeline();
        } else {
            showToast('Failed to update stage on server. Reverting...', 'error');
            rollbackCard(cardElement, originalDropZone, originalIndex);
        }
    } catch (err) {
        showToast('Network error updating stage. Reverting...', 'error');
        rollbackCard(cardElement, originalDropZone, originalIndex);
    }
}

function rollbackCard(cardElement, originalDropZone, originalIndex) {
    if (!originalDropZone || !cardElement) return;
    const existingCards = Array.from(originalDropZone.children);
    if (originalIndex >= existingCards.length) {
        originalDropZone.appendChild(cardElement);
    } else {
        originalDropZone.insertBefore(cardElement, existingCards[originalIndex]);
    }
    recalculateColumnTotals();
}

function renderBoard() {
    const board = document.getElementById('kanbanBoard');
    if (!board) return;

    // Destroy previous sortable instances to prevent leaks
    sortableInstances.forEach(s => s.destroy());
    sortableInstances = [];

    board.innerHTML = STAGES.map(stage => `
        <section class="kanban-col border-t-4 ${stage.headerBg} glass-panel flex flex-col" aria-labelledby="header-${stage.key}">
            <div class="p-3.5 border-b border-slate-200/80 dark:border-slate-800/80 flex items-center justify-between">
                <div class="flex items-center space-x-2">
                    <span class="w-2.5 h-2.5 rounded-full ${stage.color}"></span>
                    <h2 id="header-${stage.key}" class="text-xs font-bold uppercase tracking-wider text-slate-700 dark:text-slate-200">${stage.label}</h2>
                    <span id="count-${stage.key}" class="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-slate-200/70 dark:bg-slate-800 text-slate-700 dark:text-slate-300">0</span>
                </div>
                <div id="sum-${stage.key}" class="text-xs font-bold text-slate-600 dark:text-slate-300">₹0</div>
            </div>
            <div id="drop-${stage.key}" class="kanban-drop-zone" data-stage="${stage.key}" role="list"></div>
        </section>
    `).join('');

    // Populate cards
    STAGES.forEach(stage => {
        const dropZone = document.getElementById(`drop-${stage.key}`);
        if (!dropZone) return;

        const stageLeads = allLeads.filter(l => l.status === stage.key);
        stageLeads.forEach(lead => {
            dropZone.appendChild(createLeadCardElement(lead));
        });

        // Initialize SortableJS
        const sortable = new Sortable(dropZone, {
            group: 'crm-pipeline-stages',
            animation: 180,
            ghostClass: 'sortable-ghost',
            chosenClass: 'sortable-chosen',
            dragClass: 'sortable-drag',
            fallbackOnBody: true,
            swapThreshold: 0.65,
            onStart: (evt) => {
                evt.item.dataset.originalStage = evt.from.dataset.stage;
                evt.item.dataset.originalIndex = evt.oldIndex;
            },
            onEnd: async (evt) => {
                const targetStage = evt.to.dataset.stage;
                const sourceStage = evt.item.dataset.originalStage;
                const leadId = evt.item.dataset.id;

                if (targetStage === sourceStage) {
                    return; // Position only changed in same column
                }

                // Live calculate UI totals immediately
                recalculateColumnTotals();

                // Persist to API with rollback on failure
                await handleStageChange(
                    leadId,
                    targetStage,
                    evt.from,
                    parseInt(evt.item.dataset.originalIndex, 10),
                    evt.item
                );
            }
        });

        sortableInstances.push(sortable);
    });

    recalculateColumnTotals();
}

async function loadPipeline() {
    try {
        const response = await apiFetch('/api/leads');
        allLeads = await response.json();
        renderBoard();
        filterLeads();
    } catch (err) {
        if (err.message !== 'Unauthorized') {
            showToast('Unable to load pipeline leads.', 'error');
        }
    }
}

function filterLeads() {
    const searchInput = document.getElementById('searchInput');
    const query = (searchInput?.value || '').trim().toLowerCase();

    document.querySelectorAll('.lead-card').forEach(card => {
        if (!query) {
            card.classList.remove('hidden');
            return;
        }
        const text = card.textContent.toLowerCase();
        if (text.includes(query)) {
            card.classList.remove('hidden');
        } else {
            card.classList.add('hidden');
        }
    });

    recalculateColumnTotals();
}

/* Stage Modal for Keyboard Move */
function openStageModal(lead) {
    pendingMoveLead = lead;
    const modal = document.getElementById('moveModal');
    const leadNameEl = document.getElementById('moveModalLeadName');
    const listEl = document.getElementById('stageSelectList');
    if (!modal || !listEl) return;

    leadNameEl.textContent = `${lead.companyName} (${lead.title})`;
    listEl.innerHTML = STAGES.map(s => `
        <button type="button" class="w-full text-left px-3 py-2 rounded-lg text-xs font-semibold flex items-center justify-between border transition-all ${
            s.key === lead.status
                ? 'bg-brand-50 border-brand-500 text-brand-700 dark:bg-brand-950/60 dark:text-brand-300'
                : 'border-slate-200 dark:border-slate-800 text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800'
        }" data-stage="${s.key}">
            <span class="flex items-center gap-2">
                <span class="w-2.5 h-2.5 rounded-full ${s.color}"></span>
                ${s.label}
            </span>
            ${s.key === lead.status ? '<span class="text-xs text-brand-600 font-bold">Current</span>' : ''}
        </button>
    `).join('');

    listEl.querySelectorAll('button').forEach(btn => {
        btn.addEventListener('click', async () => {
            const targetStage = btn.dataset.stage;
            if (targetStage !== lead.status) {
                const card = document.querySelector(`.lead-card[data-id="${lead.id}"]`);
                const targetZone = document.getElementById(`drop-${targetStage}`);
                if (card && targetZone) {
                    const originalZone = card.parentElement;
                    const originalIndex = Array.from(originalZone.children).indexOf(card);
                    targetZone.appendChild(card);
                    closeModal();
                    await handleStageChange(lead.id, targetStage, originalZone, originalIndex, card);
                }
            } else {
                closeModal();
            }
        });
    });

    modal.classList.remove('hidden');
}

function closeModal() {
    const modal = document.getElementById('moveModal');
    if (modal) modal.classList.add('hidden');
    pendingMoveLead = null;
}

document.getElementById('closeMoveModal')?.addEventListener('click', closeModal);
document.getElementById('moveModal')?.addEventListener('click', (e) => {
    if (e.target.id === 'moveModal') closeModal();
});

// Search input
document.getElementById('searchInput')?.addEventListener('input', filterLeads);
document.getElementById('refreshBtn')?.addEventListener('click', loadPipeline);

// SignalR Real-Time Synchronization
const hubConnection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/crm', {
        accessTokenFactory: () => getAuthToken() || ''
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .build();

const realtimeIndicator = document.getElementById('realtimeStatus');
const realtimeText = document.getElementById('realtimeStatusText');

function setRealtimeStatus(status, isOk = true) {
    if (!realtimeIndicator || !realtimeText) return;
    realtimeText.textContent = status;
    if (isOk) {
        realtimeIndicator.className = 'flex items-center space-x-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800';
    } else {
        realtimeIndicator.className = 'flex items-center space-x-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-amber-50 text-amber-700 dark:bg-amber-950/60 dark:text-amber-300 border border-amber-200 dark:border-amber-800';
    }
}

hubConnection.onreconnecting(() => setRealtimeStatus('Reconnecting...', false));
hubConnection.onreconnected(() => {
    setRealtimeStatus('Live Sync', true);
    showToast('Real-time connection restored', 'info');
    loadPipeline();
});
hubConnection.onclose(() => setRealtimeStatus('Offline', false));

// Event: Another browser or background worker changed a lead stage
hubConnection.on('leadStageChanged', (data) => {
    const leadId = String(data.leadId);
    const newStage = data.newStatus;
    const card = document.querySelector(`.lead-card[data-id="${leadId}"]`);
    const targetDrop = document.getElementById(`drop-${newStage}`);

    if (card && targetDrop && card.parentElement !== targetDrop) {
        // Only move if not currently dragged
        if (!card.classList.contains('sortable-chosen')) {
            targetDrop.appendChild(card);
            card.dataset.stage = newStage;
            recalculateColumnTotals();

            // Glow animation
            card.classList.add('ring-2', 'ring-brand-500');
            setTimeout(() => card.classList.remove('ring-2', 'ring-brand-500'), 1500);

            showToast(`Stage updated to ${newStage} by ${data.updatedBy || 'team member'}`, 'info', data.companyName);
        }
    } else if (!card) {
        // If it was a newly created lead not yet in DOM, reload
        loadPipeline();
    }
});

// Event: Lead priority score recalculated
hubConnection.on('leadScored', (data) => {
    const leadId = String(data.leadId);
    const card = document.querySelector(`.lead-card[data-id="${leadId}"]`);
    if (card) {
        const scoreBadge = card.querySelector('.fa-bolt-lightning')?.parentElement;
        if (scoreBadge) {
            scoreBadge.innerHTML = `<i class="fa-solid fa-bolt-lightning text-[10px] mr-1"></i>Score ${data.priorityScore}`;
        }
    }
});

// Event: Lead assigned
hubConnection.on('leadAssigned', (data) => {
    showToast(data.message || 'Deal reassigned', 'info', data.title);
    loadPipeline();
});

async function startRealtime() {
    try {
        await hubConnection.start();
        setRealtimeStatus('Live Sync', true);
    } catch {
        setRealtimeStatus('Disconnected', false);
    }
}

// Initial bootstrap
startRealtime();
loadPipeline();
