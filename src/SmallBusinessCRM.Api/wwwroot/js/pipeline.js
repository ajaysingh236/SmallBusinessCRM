const stages = ['New', 'Contacted', 'DemoScheduled', 'ProposalSent', 'ClosedWon', 'ClosedLost'];
const label = s => s.replace(/([A-Z])/g, ' $1').trim();
const money = n => new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(n);
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '>': '&gt;', '<': '&lt;', '"': '&quot;', "'": '&#39;' }[c]));

function toast(text) {
    const area = document.querySelector('#toastArea');
    if (!area) return;
    area.insertAdjacentHTML('beforeend',
        `<div class="toast show text-bg-primary mb-2 shadow" role="alert" aria-live="assertive" aria-atomic="true">
            <div class="d-flex">
                <div class="toast-body">${esc(text)}</div>
                <button class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
            </div>
        </div>`
    );
    // Auto-remove toast after 4 seconds
    const newest = area.lastElementChild;
    setTimeout(() => newest?.remove(), 4000);
}

function card(lead) {
    return `<article class="card lead-card shadow-sm p-2 mb-2 bg-white" draggable="true" data-id="${lead.id}">
        <div class="small text-muted">${esc(lead.companyName)}</div>
        <div class="fw-semibold">${esc(lead.title)}</div>
        <div class="lead-value text-success">${money(lead.value)}</div>
        <div class="d-flex justify-content-between align-items-center mt-1">
            <span class="badge bg-secondary">Score: ${lead.priorityScore}</span>
            <span class="small text-muted">${esc(lead.assignedUser?.fullName ?? 'Unassigned')}</span>
        </div>
    </article>`;
}

async function load() {
    try {
        const response = await apiFetch('/api/leads');
        const leads = await response.json();

        document.querySelector('#kanban').innerHTML = stages.map(s =>
            `<section class="kanban-column">
                <h2 class="h6 px-2 py-1 bg-light border-bottom mb-2 text-uppercase text-secondary">${label(s)}</h2>
                <div class="kanban-drop" data-stage="${s}">
                    ${leads.filter(l => l.status === s).map(card).join('')}
                </div>
            </section>`
        ).join('');

        document.querySelectorAll('.lead-card').forEach(x => {
            x.addEventListener('dragstart', e => {
                x.classList.add('dragging');
                e.dataTransfer.setData('leadId', x.dataset.id);
            });
            x.addEventListener('dragend', () => x.classList.remove('dragging'));
        });

        document.querySelectorAll('.kanban-drop').forEach(dropZone => {
            dropZone.addEventListener('dragover', e => {
                e.preventDefault();
                dropZone.classList.add('drag-over');
            });
            dropZone.addEventListener('dragleave', () => dropZone.classList.remove('drag-over'));
            dropZone.addEventListener('drop', async e => {
                e.preventDefault();
                dropZone.classList.remove('drag-over');
                const id = e.dataTransfer.getData('leadId');
                const status = dropZone.dataset.stage;
                const draggedCard = document.querySelector(`[data-id="${id}"]`);

                try {
                    const response = await apiFetch(`/api/leads/${id}/status`, {
                        method: 'PATCH',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ status })
                    });

                    if (response.ok) {
                        if (draggedCard) dropZone.append(draggedCard);
                        toast(`Lead moved to ${label(status)}`);
                    } else if (response.status === 409) {
                        toast('Conflict: Lead was modified concurrently by another user. Reloading pipeline...');
                        await load();
                    } else {
                        toast('Unable to update lead stage.');
                        await load();
                    }
                } catch {
                    toast('Network error during stage update.');
                    await load();
                }
            });
        });
    } catch (err) {
        if (err.message !== 'Unauthorized') {
            toast('Failed to load leads.');
        }
    }
}

// SignalR Hub Connection Setup
const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/crm', {
        accessTokenFactory: () => getAuthToken() || ''
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .build();

connection.onreconnecting(error => {
    console.warn('Realtime connection lost. Reconnecting...', error);
    toast('Realtime connection lost. Reconnecting...');
});

connection.onreconnected(connectionId => {
    console.info('Realtime reconnected:', connectionId);
    toast('Realtime connection restored.');
    load();
});

connection.onclose(error => {
    console.error('Realtime disconnected:', error);
    toast('Realtime connection closed.');
});

connection.on('leadAssigned', x => {
    toast(x.message || 'A lead was assigned.');
    load();
});

connection.on('activityCreated', x => {
    toast(x.message || 'New activity logged.');
});

connection.on('leadStageChanged', data => {
    toast(`Lead "${esc(data.companyName)}" moved to ${label(data.newStatus)}.`);
    load();
});

connection.on('leadScored', () => {
    load();
});

async function startSignalR() {
    try {
        await connection.start();
        console.info('SignalR connected.');
    } catch (err) {
        console.warn('SignalR initial connection failed. Will retry automatically if authenticated.', err);
    }
}

startSignalR();
load();
