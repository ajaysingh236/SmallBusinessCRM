import React, { useEffect, useState, useRef } from 'react';
import Sortable from 'sortablejs';
import { api, money } from '../services/api';
import { Lead, LeadStatus, LeadScoringPrediction, EmailTracking } from '../types';
import { useSignalR } from '../hooks/useSignalR';
import {
  RotateCw,
  Search,
  Clock,
  User as UserIcon,
  Zap,
  AlertCircle,
  Plus,
  Mail,
  Calendar,
  CheckCircle2,
  TrendingUp,
  X,
  ExternalLink,
  Bot
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

  // Phase 3 Modal States
  const [selectedPrediction, setSelectedPrediction] = useState<LeadScoringPrediction | null>(null);
  const [selectedLeadForAi, setSelectedLeadForAi] = useState<Lead | null>(null);
  const [aiLoading, setAiLoading] = useState(false);

  const [selectedLeadForEmail, setSelectedLeadForEmail] = useState<Lead | null>(null);
  const [emailTrackings, setEmailTrackings] = useState<EmailTracking[]>([]);
  const [emailLoading, setEmailLoading] = useState(false);
  const [dripSending, setDripSending] = useState(false);

  const [calendarSyncing, setCalendarSyncing] = useState(false);

  // New Lead Modal State
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [companyName, setCompanyName] = useState('');
  const [contactName, setContactName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [value, setValue] = useState(25000);
  const [creating, setCreating] = useState(false);

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
        prev.map((l) =>
          l.id === data.leadId
            ? {
                ...l,
                priorityScore: data.priorityScore,
                conversionProbability: data.conversionProbability,
                predictedCloseDays: data.predictedCloseDays
              }
            : l
        )
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

  // Initialize SortableJS drag and drop
  useEffect(() => {
    sortablesRef.current.forEach((s) => s.destroy());
    sortablesRef.current = [];

    STAGES.forEach((stage) => {
      const el = containerRefs.current[stage.key];
      if (!el) return;

      const sortable = new Sortable(el, {
        group: 'pipeline-kanban',
        animation: 180,
        ghostClass: 'opacity-40',
        chosenClass: 'scale-[1.02]',
        dragClass: 'shadow-2xl',
        onEnd: async (evt) => {
          const itemEl = evt.item;
          const leadId = Number(itemEl.getAttribute('data-id'));
          const toStage = evt.to.getAttribute('data-stage') as LeadStatus;
          const fromStage = evt.from.getAttribute('data-stage') as LeadStatus;

          if (toStage === fromStage) return;

          // Optimistic local state update
          setLeads((prev) =>
            prev.map((l) => (l.id === leadId ? { ...l, status: toStage } : l))
          );

          try {
            await api.updateLeadStatus(leadId, toStage);
            showNotice(`Lead status updated to ${toStage}`);
          } catch (err) {
            console.error('Failed to update stage:', err);
            showNotice('Failed to update status. Reverting change.');
            // Revert state
            setLeads((prev) =>
              prev.map((l) => (l.id === leadId ? { ...l, status: fromStage } : l))
            );
          }
        }
      });

      sortablesRef.current.push(sortable);
    });

    return () => {
      sortablesRef.current.forEach((s) => s.destroy());
      sortablesRef.current = [];
    };
  }, [leads]);

  // Open AI Lead Scoring Prediction Breakdown
  const handleOpenAiModal = async (lead: Lead) => {
    setSelectedLeadForAi(lead);
    setAiLoading(true);
    try {
      const prediction = await api.getLeadPrediction(lead.id);
      setSelectedPrediction(prediction);
    } catch (err) {
      console.error('Failed to fetch prediction:', err);
      showNotice('Failed to calculate AI prediction.');
    } finally {
      setAiLoading(false);
    }
  };

  // Recalculate score with selected strategy
  const handleRecalculateStrategy = async (strategy: string) => {
    if (!selectedLeadForAi) return;
    setAiLoading(true);
    try {
      const updated = await api.recalculateLeadScore(selectedLeadForAi.id, strategy);
      setSelectedPrediction(updated);
      setLeads((prev) =>
        prev.map((l) =>
          l.id === selectedLeadForAi.id
            ? {
                ...l,
                priorityScore: updated.priorityScore,
                conversionProbability: updated.conversionProbability,
                predictedCloseDays: updated.predictedCloseDays
              }
            : l
        )
      );
      showNotice(`AI Lead Score updated via ${updated.strategy}`);
    } catch (err) {
      console.error(err);
      showNotice('Failed to recalculate score.');
    } finally {
      setAiLoading(false);
    }
  };

  // Open Email Tracking / Drip modal
  const handleOpenEmailModal = async (lead: Lead) => {
    setSelectedLeadForEmail(lead);
    setEmailLoading(true);
    try {
      const trackings = await api.getLeadEmailTracking(lead.id);
      setEmailTrackings(trackings);
    } catch (err) {
      console.error('Failed to load email tracking:', err);
      showNotice('Failed to load email tracking.');
    } finally {
      setEmailLoading(false);
    }
  };

  // Trigger Next Drip Email Step
  const handleTriggerNextDrip = async () => {
    if (!selectedLeadForEmail) return;
    setDripSending(true);
    try {
      const res = await api.triggerDripStep(selectedLeadForEmail.id);
      showNotice(`Drip Step queued: "${res.stepName}" to ${res.recipient}`);
      // Refresh email tracking list
      const updated = await api.getLeadEmailTracking(selectedLeadForEmail.id);
      setEmailTrackings(updated);
      loadLeads();
    } catch (err: any) {
      console.error(err);
      showNotice(err.message || 'Failed to trigger drip step.');
    } finally {
      setDripSending(false);
    }
  };

  // Sync with Calendar
  const handleSyncCalendar = async () => {
    setCalendarSyncing(true);
    try {
      // Connect simulated if not already connected, then sync
      await api.connectCalendar('Google');
      const res = await api.syncCalendar();
      showNotice(`Calendar synced: ${res.syncedCount} upcoming tasks synchronized with Google Calendar.`);
    } catch (err) {
      console.error(err);
      showNotice('Calendar sync failed.');
    } finally {
      setCalendarSyncing(false);
    }
  };

  const handleCreateLead = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreating(true);
    try {
      await api.createLead({
        title: `${companyName} Expansion`,
        companyName,
        contactName,
        email,
        phone,
        value: Number(value)
      });
      setCreateModalOpen(false);
      setCompanyName('');
      setContactName('');
      setEmail('');
      setPhone('');
      showNotice('New lead created successfully!');
      loadLeads();
    } catch (err) {
      console.error('Failed to create lead:', err);
      showNotice('Failed to create lead.');
    } finally {
      setCreating(false);
    }
  };

  const filteredLeads = leads.filter(
    (l) =>
      l.companyName.toLowerCase().includes(search.toLowerCase()) ||
      l.contactName.toLowerCase().includes(search.toLowerCase()) ||
      l.title.toLowerCase().includes(search.toLowerCase())
  );

  const isOverdue = (l: Lead) => {
    if (!l.tasks) return false;
    const now = new Date().toISOString();
    return l.tasks.some((t) => !t.isCompleted && t.dueDateUtc < now);
  };

  return (
    <div className="space-y-6">
      {/* Toast Notification */}
      {notification && (
        <div className="fixed bottom-5 right-5 z-50 flex items-center space-x-2 bg-slate-900 text-white dark:bg-white dark:text-slate-900 px-4 py-3 rounded-xl shadow-xl border border-slate-700 dark:border-slate-300 text-xs font-semibold animate-bounce">
          <AlertCircle className="w-4 h-4 text-brand-400" />
          <span>{notification}</span>
        </div>
      )}

      {/* Header and Controls */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 className="text-2xl font-extrabold text-slate-900 dark:text-white tracking-tight">Deal Pipeline</h1>
          <p className="text-xs text-slate-500 dark:text-slate-400">
            Real-time visual Kanban pipeline with AI Lead Scoring, Drip Campaigns, and Calendar Sync.
          </p>
        </div>

        <div className="flex items-center space-x-2.5">
          <div className="relative">
            <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
            <input
              type="text"
              placeholder="Search leads..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="pl-9 pr-3 py-2 text-xs rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-brand-500 w-48 sm:w-60 shadow-sm"
            />
          </div>

          <button
            onClick={handleSyncCalendar}
            disabled={calendarSyncing}
            className="inline-flex items-center space-x-1.5 px-3 py-2 border border-slate-200 dark:border-slate-800 rounded-xl text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors shadow-sm"
            title="Sync tasks with Google / Outlook calendar"
          >
            <Calendar className={`w-3.5 h-3.5 text-indigo-500 ${calendarSyncing ? 'animate-spin' : ''}`} />
            <span className="hidden md:inline">Calendar Sync</span>
          </button>

          <button
            onClick={loadLeads}
            disabled={loading}
            className="p-2 border border-slate-200 dark:border-slate-800 rounded-xl text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
            title="Reload pipeline"
          >
            <RotateCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          </button>

          <button
            onClick={() => setCreateModalOpen(true)}
            className="inline-flex items-center space-x-1.5 px-3 py-2 bg-brand-600 hover:bg-brand-700 text-white rounded-xl text-xs font-semibold shadow-sm shadow-brand-500/30 transition-all hover:scale-[1.02]"
          >
            <Plus className="w-4 h-4" />
            <span>New Lead</span>
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
                {stageLeads.map((lead) => {
                  const winProb = lead.conversionProbability
                    ? Math.round(lead.conversionProbability * 100)
                    : lead.priorityScore;

                  return (
                    <article
                      key={lead.id}
                      data-id={lead.id}
                      className="p-3 rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-sm hover:shadow-md hover:border-brand-500 dark:hover:border-brand-500 cursor-grab active:cursor-grabbing transition-all select-none group"
                    >
                      <div className="flex items-start justify-between gap-1 mb-1">
                        <span className="text-[11px] font-bold text-brand-600 dark:text-brand-400 truncate max-w-[170px]">
                          {lead.companyName}
                        </span>
                        <div className="flex items-center space-x-1 opacity-0 group-hover:opacity-100 transition-opacity">
                          <button
                            onClick={() => handleOpenEmailModal(lead)}
                            className="p-1 rounded hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-400 hover:text-indigo-500"
                            title="Email drip tracking"
                          >
                            <Mail className="w-3.5 h-3.5" />
                          </button>
                          <button
                            onClick={() => handleOpenAiModal(lead)}
                            className="p-1 rounded hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-400 hover:text-amber-500"
                            title="AI Score breakdown"
                          >
                            <Bot className="w-3.5 h-3.5" />
                          </button>
                        </div>
                      </div>

                      <h3 className="text-xs font-bold text-slate-900 dark:text-white leading-snug mb-2 line-clamp-2">
                        {lead.title}
                      </h3>

                      <div className="flex items-center justify-between text-xs mb-2">
                        <span className="font-extrabold text-emerald-600 dark:text-emerald-400 text-xs">
                          {money(lead.value)}
                        </span>
                        
                        {/* AI Score Badge with Click-to-Inspect */}
                        <button
                          onClick={() => handleOpenAiModal(lead)}
                          className={`inline-flex items-center px-1.5 py-0.5 rounded-full text-[10px] font-bold border transition-transform hover:scale-105 ${
                            winProb >= 70
                              ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300 border-emerald-300 dark:border-emerald-700'
                              : winProb >= 40
                              ? 'bg-amber-50 text-amber-700 dark:bg-amber-950/60 dark:text-amber-300 border-amber-300 dark:border-amber-700'
                              : 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300 border-slate-200 dark:border-slate-700'
                          }`}
                          title="Click to view AI Win Prediction breakdown"
                        >
                          <Zap className="w-2.5 h-2.5 mr-1 text-amber-500" />
                          <span>{winProb}% AI</span>
                        </button>
                      </div>

                      {lead.predictedCloseDays && (
                        <div className="text-[10px] text-slate-400 dark:text-slate-500 mb-1 flex items-center">
                          <TrendingUp className="w-2.5 h-2.5 mr-1 text-indigo-400" />
                          Est. close in ~{lead.predictedCloseDays} days
                        </div>
                      )}

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
                  );
                })}
              </div>
            </div>
          );
        })}
      </div>

      {/* AI Lead Scoring Inspection Modal */}
      {selectedLeadForAi && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/50 backdrop-blur-sm animate-fade-in">
          <div className="bg-white dark:bg-slate-900 rounded-2xl max-w-lg w-full p-6 shadow-2xl border border-slate-200 dark:border-slate-800 space-y-4">
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
              <div className="flex items-center space-x-2">
                <div className="w-8 h-8 rounded-lg bg-amber-100 dark:bg-amber-900/50 flex items-center justify-center text-amber-600 dark:text-amber-400">
                  <Bot className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                    AI Lead Win Prediction & Factors
                  </h3>
                  <p className="text-[11px] text-slate-500">{selectedLeadForAi.companyName}</p>
                </div>
              </div>
              <button
                onClick={() => setSelectedLeadForAi(null)}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {aiLoading ? (
              <div className="py-12 flex flex-col items-center justify-center space-y-3">
                <RotateCw className="w-8 h-8 animate-spin text-brand-600" />
                <p className="text-xs text-slate-500">Evaluating calibrated conversion models...</p>
              </div>
            ) : selectedPrediction ? (
              <div className="space-y-4">
                {/* Score & Probability Card */}
                <div className="grid grid-cols-3 gap-3 p-3 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/80 dark:border-slate-700/80 text-center">
                  <div>
                    <div className="text-[10px] font-bold text-slate-400 uppercase">Win Probability</div>
                    <div className="text-xl font-extrabold text-brand-600 dark:text-brand-400">
                      {Math.round(selectedPrediction.conversionProbability * 100)}%
                    </div>
                  </div>
                  <div>
                    <div className="text-[10px] font-bold text-slate-400 uppercase">Priority Score</div>
                    <div className="text-xl font-extrabold text-amber-500">
                      {selectedPrediction.priorityScore}/100
                    </div>
                  </div>
                  <div>
                    <div className="text-[10px] font-bold text-slate-400 uppercase">Est. Close Time</div>
                    <div className="text-xl font-extrabold text-emerald-600 dark:text-emerald-400">
                      ~{selectedPrediction.predictedCloseDays || 30}d
                    </div>
                  </div>
                </div>

                {/* Factors List */}
                <div>
                  <h4 className="text-xs font-bold text-slate-700 dark:text-slate-300 mb-2">
                    Key Influencing Model Signals (SHAP Breakdown)
                  </h4>
                  <div className="space-y-2 max-h-48 overflow-y-auto pr-1">
                    {selectedPrediction.factors.map((f, i) => {
                      const isPositive = f.impactScore >= 0;
                      return (
                        <div
                          key={i}
                          className="flex items-start justify-between p-2 rounded-lg bg-slate-50 dark:bg-slate-800/40 border border-slate-200/60 dark:border-slate-800 text-xs"
                        >
                          <div className="space-y-0.5 max-w-[340px]">
                            <span className="font-semibold text-slate-900 dark:text-slate-100">
                              {f.featureName}
                            </span>
                            <p className="text-[11px] text-slate-500 dark:text-slate-400 leading-snug">
                              {f.description}
                            </p>
                          </div>
                          <span
                            className={`px-2 py-0.5 rounded-full font-bold text-[10px] whitespace-nowrap ${
                              isPositive
                                ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300'
                                : 'bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-300'
                            }`}
                          >
                            {isPositive ? `+${f.impactScore}` : f.impactScore}
                          </span>
                        </div>
                      );
                    })}
                  </div>
                </div>

                {/* Model Strategy Switcher */}
                <div className="pt-2 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between">
                  <span className="text-[11px] text-slate-400">
                    Active Strategy: <strong className="text-slate-700 dark:text-slate-200">{selectedPrediction.strategy}</strong>
                  </span>
                  <div className="flex space-x-2">
                    <button
                      onClick={() => handleRecalculateStrategy('LogisticRegressionML')}
                      className="px-2.5 py-1 text-[10px] font-bold rounded-lg border border-brand-500 text-brand-600 dark:text-brand-400 hover:bg-brand-50 dark:hover:bg-brand-950/50"
                    >
                      ML Model
                    </button>
                    <button
                      onClick={() => handleRecalculateStrategy('RuleBasedHeuristic')}
                      className="px-2.5 py-1 text-[10px] font-bold rounded-lg border border-slate-300 dark:border-slate-700 text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800"
                    >
                      Rule-Based
                    </button>
                  </div>
                </div>
              </div>
            ) : null}
          </div>
        </div>
      )}

      {/* Email Tracking & Drip Campaign Modal */}
      {selectedLeadForEmail && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/50 backdrop-blur-sm animate-fade-in">
          <div className="bg-white dark:bg-slate-900 rounded-2xl max-w-lg w-full p-6 shadow-2xl border border-slate-200 dark:border-slate-800 space-y-4">
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
              <div className="flex items-center space-x-2">
                <div className="w-8 h-8 rounded-lg bg-indigo-100 dark:bg-indigo-900/50 flex items-center justify-center text-indigo-600 dark:text-indigo-400">
                  <Mail className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                    Email Drip Workflows & Engagement Tracking
                  </h3>
                  <p className="text-[11px] text-slate-500">
                    {selectedLeadForEmail.companyName} ({selectedLeadForEmail.email || 'No email registered'})
                  </p>
                </div>
              </div>
              <button
                onClick={() => setSelectedLeadForEmail(null)}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Campaign Step Overview */}
            <div className="flex items-center justify-between p-3 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/80 dark:border-slate-700/80">
              <div>
                <div className="text-[10px] font-bold text-slate-400 uppercase">Drip Campaign Step</div>
                <div className="text-sm font-bold text-slate-800 dark:text-slate-100">
                  Step {selectedLeadForEmail.dripCampaignStep || 0} of 3
                </div>
              </div>

              <button
                onClick={handleTriggerNextDrip}
                disabled={dripSending || !selectedLeadForEmail.email}
                className="inline-flex items-center space-x-1.5 px-3 py-1.5 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white rounded-lg text-xs font-semibold shadow-sm shadow-indigo-500/30 transition-transform active:scale-95"
              >
                <Mail className={`w-3.5 h-3.5 ${dripSending ? 'animate-spin' : ''}`} />
                <span>{dripSending ? 'Sending...' : 'Trigger Next Drip Step'}</span>
              </button>
            </div>

            {/* Tracking History */}
            <div>
              <h4 className="text-xs font-bold text-slate-700 dark:text-slate-300 mb-2">
                Outbound Email Delivery & Open/Click Telemetry
              </h4>

              {emailLoading ? (
                <div className="py-8 text-center text-xs text-slate-400">Loading tracking history...</div>
              ) : emailTrackings.length === 0 ? (
                <div className="py-6 text-center text-xs text-slate-400 border border-dashed border-slate-200 dark:border-slate-800 rounded-xl">
                  No automated emails have been logged for this lead yet.
                </div>
              ) : (
                <div className="space-y-2 max-h-52 overflow-y-auto pr-1">
                  {emailTrackings.map((t) => (
                    <div
                      key={t.id}
                      className="p-2.5 rounded-lg bg-slate-50 dark:bg-slate-800/40 border border-slate-200/60 dark:border-slate-800 text-xs space-y-1.5"
                    >
                      <div className="flex items-center justify-between">
                        <span className="font-bold text-slate-900 dark:text-slate-100">{t.stepName || t.subject}</span>
                        <span className="text-[10px] text-slate-400">
                          {new Date(t.sentAtUtc).toLocaleDateString()}
                        </span>
                      </div>
                      <div className="flex items-center space-x-3 text-[11px]">
                        <span
                          className={`inline-flex items-center font-semibold ${
                            t.isOpened ? 'text-emerald-600 dark:text-emerald-400' : 'text-slate-400'
                          }`}
                        >
                          <CheckCircle2 className="w-3.5 h-3.5 mr-1" />
                          {t.isOpened ? 'Opened' : 'Delivered (Unopened)'}
                        </span>

                        <span
                          className={`inline-flex items-center font-semibold ${
                            t.clickCount > 0 ? 'text-brand-600 dark:text-brand-400' : 'text-slate-400'
                          }`}
                        >
                          <ExternalLink className="w-3.5 h-3.5 mr-1" />
                          {t.clickCount} Clicks
                        </span>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* New Lead Creation Modal */}
      {createModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/50 backdrop-blur-sm animate-fade-in">
          <div className="bg-white dark:bg-slate-900 rounded-2xl max-w-md w-full p-6 shadow-2xl border border-slate-200 dark:border-slate-800 space-y-4">
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
              <h3 className="text-base font-bold text-slate-900 dark:text-white">Create New Opportunity</h3>
              <button
                onClick={() => setCreateModalOpen(false)}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <form onSubmit={handleCreateLead} className="space-y-3">
              <div>
                <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                  Company Name *
                </label>
                <input
                  type="text"
                  required
                  value={companyName}
                  onChange={(e) => setCompanyName(e.target.value)}
                  placeholder="e.g. Acme Corporation"
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-950 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-brand-500"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                  Contact Person *
                </label>
                <input
                  type="text"
                  required
                  value={contactName}
                  onChange={(e) => setContactName(e.target.value)}
                  placeholder="e.g. Jane Doe"
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-950 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-brand-500"
                />
              </div>

              <div className="grid grid-cols-2 gap-2">
                <div>
                  <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                    Direct Email
                  </label>
                  <input
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder="contact@company.com"
                    className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-950 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-brand-500"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">Phone</label>
                  <input
                    type="tel"
                    value={phone}
                    onChange={(e) => setPhone(e.target.value)}
                    placeholder="+91 98765 43210"
                    className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-950 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-brand-500"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                  Expected Deal Value (₹)
                </label>
                <input
                  type="number"
                  min="0"
                  step="1000"
                  value={value}
                  onChange={(e) => setValue(Number(e.target.value))}
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-950 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-brand-500"
                />
              </div>

              <div className="pt-3 border-t border-slate-100 dark:border-slate-800 flex justify-end space-x-2">
                <button
                  type="button"
                  onClick={() => setCreateModalOpen(false)}
                  className="px-4 py-2 rounded-xl text-xs font-semibold text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={creating}
                  className="px-4 py-2 rounded-xl text-xs font-semibold bg-brand-600 hover:bg-brand-700 text-white shadow-md shadow-brand-500/30"
                >
                  {creating ? 'Saving...' : 'Create Deal'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
