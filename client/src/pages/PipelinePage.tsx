import React, { useEffect, useState, useRef } from 'react';
import Sortable from 'sortablejs';
import { api, money } from '../services/api';
import { Lead, LeadStatus } from '../types';
import { useSignalR } from '../hooks/useSignalR';
import {
  RotateCw,
  Search,
  Clock,
  User as UserIcon,
  Zap,
  AlertCircle
} from 'lucide-react';

interface StageConfig {
  key: LeadStatus;
  label: string;
  dotColor: string;
  borderColor: string;
}

const STAGES: StageConfig[] = [
  { key: 'New', label: 'New Lead', dotColor: 'bg-blue-500', borderColor: 'border-t-blue-500' },
  { key: 'Contacted', label: 'Contacted', dotColor: 'bg-sky-500', borderColor: 'border-t-sky-500' },
  { key: 'DemoScheduled', label: 'Demo Scheduled', dotColor: 'bg-indigo-500', borderColor: 'border-t-indigo-500' },
  { key: 'ProposalSent', label: 'Proposal Sent', dotColor: 'bg-amber-500', borderColor: 'border-t-amber-500' },
  { key: 'ClosedWon', label: 'Closed Won', dotColor: 'bg-emerald-500', borderColor: 'border-t-emerald-500' },
  { key: 'ClosedLost', label: 'Closed Lost', dotColor: 'bg-rose-500', borderColor: 'border-t-rose-500' }
];

export const PipelinePage: React.FC = () => {
  const [leads, setLeads] = useState<Lead[]>([]);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [notification, setNotification] = useState<string | null>(null);

  const containerRefs = useRef<Record<string, HTMLDivElement | null>>({});
  const sortablesRef = useRef<Sortable[]>([]);

  const showNotice = (msg: string) => {
    setNotification(msg);
    setTimeout(() => setNotification(null), 3500);
  };

  // SignalR Integration
  useSignalR({
    onLeadStageChanged: (data) => {
      setLeads((prev) =>
        prev.map((l) => (l.id === data.leadId ? { ...l, status: data.newStatus as LeadStatus } : l))
      );
      showNotice(`Deal "${data.companyName}" moved to ${data.newStatus}`);
    },
    onLeadScored: (data) => {
      setLeads((prev) =>
        prev.map((l) => (l.id === data.leadId ? { ...l, priorityScore: data.priorityScore } : l))
      );
    }
  });

  const loadLeads = async () => {
    try {
      setLoading(true);
      const data = await api.getLeads();
      setLeads(data);
    } catch (err) {
      console.error('Failed to load leads:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadLeads();
  }, []);

  // Initialize SortableJS
  useEffect(() => {
    sortablesRef.current.forEach((s) => s.destroy());
    sortablesRef.current = [];

    STAGES.forEach((stage) => {
      const el = containerRefs.current[stage.key];
      if (!el) return;

      const sortable = new Sortable(el, {
        group: 'leads-kanban',
        animation: 180,
        ghostClass: 'sortable-ghost',
        chosenClass: 'sortable-chosen',
        onEnd: async (evt) => {
          const leadId = Number(evt.item.dataset.id);
          const targetStage = evt.to.dataset.stage as LeadStatus;
          const sourceStage = evt.from.dataset.stage as LeadStatus;

          if (targetStage === sourceStage) return;

          // Optimistically update state
          setLeads((prev) =>
            prev.map((l) => (l.id === leadId ? { ...l, status: targetStage } : l))
          );

          try {
            await api.updateStage(leadId, targetStage);
            showNotice(`Lead updated to ${targetStage}`);
          } catch (err: any) {
            // Rollback on conflict or failure
            showNotice('Failed to update stage. Reverting position...');
            setLeads((prev) =>
              prev.map((l) => (l.id === leadId ? { ...l, status: sourceStage } : l))
            );
          }
        }
      });

      sortablesRef.current.push(sortable);
    });

    return () => {
      sortablesRef.current.forEach((s) => s.destroy());
    };
  }, [leads]);

  const filteredLeads = leads.filter((l) => {
    if (!search) return true;
    const q = search.toLowerCase();
    return (
      l.companyName.toLowerCase().includes(q) ||
      l.contactName.toLowerCase().includes(q) ||
      l.title.toLowerCase().includes(q)
    );
  });

  const grandTotal = filteredLeads.reduce((acc, l) => acc + (l.value || 0), 0);

  const isOverdue = (lead: Lead) => {
    if (!lead.tasks || lead.tasks.length === 0) return false;
    const now = new Date();
    return lead.tasks.some((t) => !t.isCompleted && new Date(t.dueDateUtc) < now);
  };

  return (
    <div className="space-y-6">
      {/* Toast Notice */}
      {notification && (
        <div className="fixed bottom-5 right-5 z-50 bg-slate-900 text-white dark:bg-white dark:text-slate-900 px-4 py-2.5 rounded-xl shadow-2xl text-xs font-semibold flex items-center gap-2 animate-bounce">
          <AlertCircle className="w-4 h-4 text-brand-400" />
          <span>{notification}</span>
        </div>
      )}

      {/* Top Controls */}
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900 dark:text-white flex items-center gap-2">
            <span>Pipeline Deals</span>
            <span className="text-xs font-semibold px-2.5 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
              {filteredLeads.length} deals
            </span>
          </h1>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
            Physics drag-and-drop SortableJS board with live column totals and SignalR sync.
          </p>
        </div>

        <div className="flex items-center flex-wrap gap-3">
          <div className="relative min-w-[220px]">
            <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
            <input
              type="search"
              placeholder="Search company or deal..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full pl-9 pr-3.5 py-1.5 text-xs rounded-xl bg-slate-100 dark:bg-slate-800 border-none text-slate-900 dark:text-white placeholder-slate-400 focus:ring-2 focus:ring-brand-500"
            />
          </div>

          <div className="px-3.5 py-1.5 rounded-xl bg-indigo-50 dark:bg-indigo-950/60 text-indigo-700 dark:text-indigo-300 text-xs font-semibold flex items-center gap-1.5 border border-indigo-200/60 dark:border-indigo-800/60">
            <span className="text-indigo-500 font-normal">Pipeline Value:</span>
            <span className="font-bold">{money(grandTotal)}</span>
          </div>

          <button
            onClick={loadLeads}
            disabled={loading}
            className="p-2 border border-slate-200 dark:border-slate-800 rounded-xl text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
            title="Reload pipeline"
          >
            <RotateCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          </button>
        </div>
      </div>

      {/* Kanban Columns Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-4 min-h-[70vh] overflow-x-auto pb-4">
        {STAGES.map((stage) => {
          const stageLeads = filteredLeads.filter((l) => l.status === stage.key);
          const stageTotal = stageLeads.reduce((acc, l) => acc + (l.value || 0), 0);

          return (
            <div
              key={stage.key}
              className={`glass-panel border-t-4 ${stage.borderColor} flex flex-col min-w-[260px] h-[72vh]`}
            >
              <div className="p-3 border-b border-slate-200/80 dark:border-slate-800/80 flex items-center justify-between">
                <div className="flex items-center space-x-2">
                  <span className={`w-2.5 h-2.5 rounded-full ${stage.dotColor}`} />
                  <h2 className="text-xs font-bold uppercase tracking-wider text-slate-700 dark:text-slate-200">
                    {stage.label}
                  </h2>
                  <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-slate-200/70 dark:bg-slate-800 text-slate-700 dark:text-slate-300">
                    {stageLeads.length}
                  </span>
                </div>
                <div className="text-[11px] font-bold text-slate-600 dark:text-slate-300">{money(stageTotal)}</div>
              </div>

              {/* Drop Container */}
              <div
                ref={(el) => (containerRefs.current[stage.key] = el)}
                data-stage={stage.key}
                className="flex-1 p-2.5 overflow-y-auto space-y-2.5 min-h-[150px]"
              >
                {stageLeads.map((lead) => (
                  <article
                    key={lead.id}
                    data-id={lead.id}
                    className="p-3 rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-sm hover:shadow-md hover:border-brand-500 dark:hover:border-brand-500 cursor-grab active:cursor-grabbing transition-all select-none"
                  >
                    <div className="flex items-start justify-between gap-1 mb-1">
                      <span className="text-[11px] font-bold text-brand-600 dark:text-brand-400 truncate max-w-[170px]">
                        {lead.companyName}
                      </span>
                    </div>

                    <h3 className="text-xs font-bold text-slate-900 dark:text-white leading-snug mb-2 line-clamp-2">
                      {lead.title}
                    </h3>

                    <div className="flex items-center justify-between text-xs mb-2">
                      <span className="font-extrabold text-emerald-600 dark:text-emerald-400 text-xs">
                        {money(lead.value)}
                      </span>
                      <span className="inline-flex items-center px-1.5 py-0.5 rounded-full text-[10px] font-bold bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 border border-slate-200 dark:border-slate-700">
                        <Zap className="w-2.5 h-2.5 mr-1 text-amber-500" />
                        {lead.priorityScore}
                      </span>
                    </div>

                    {isOverdue(lead) && (
                      <div className="mb-2 inline-flex items-center px-2 py-0.5 rounded-md text-[10px] font-medium bg-rose-50 text-rose-700 dark:bg-rose-950/60 dark:text-rose-300 border border-rose-200 dark:border-rose-800 animate-pulse">
                        <Clock className="w-3 h-3 mr-1 text-rose-500" />
                        Overdue Task
                      </div>
                    )}

                    <div className="pt-2 border-t border-slate-100 dark:border-slate-800/80 flex items-center justify-between text-[10px] text-slate-400">
                      <span className="truncate max-w-[120px] flex items-center">
                        <UserIcon className="w-3 h-3 mr-1" />
                        {lead.contactName}
                      </span>
                      <span className="truncate max-w-[100px] text-right font-medium text-slate-500">
                        {lead.assignedUser?.fullName || 'Unassigned'}
                      </span>
                    </div>
                  </article>
                ))}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
