import React, { useEffect, useState } from 'react';
import { api, money } from '../services/api';
import { DashboardReport, AnalyticsReport, Lead, ActivityLog } from '../types';
import {
  Users,
  Award,
  AlertTriangle,
  RotateCw,
  Zap,
  TrendingUp,
  PieChart as PieIcon,
  BarChart3,
  Phone,
  Mail,
  Calendar,
  FileText,
  GitCommit,
  Cog
} from 'lucide-react';
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  BarElement,
  Title,
  Tooltip,
  Legend,
  ArcElement,
  PointElement,
  LineElement
} from 'chart.js';
import { Bar, Doughnut } from 'react-chartjs-2';

ChartJS.register(
  CategoryScale,
  LinearScale,
  BarElement,
  Title,
  Tooltip,
  Legend,
  ArcElement,
  PointElement,
  LineElement
);

export const DashboardPage: React.FC = () => {
  const [report, setReport] = useState<DashboardReport | null>(null);
  const [analytics, setAnalytics] = useState<AnalyticsReport | null>(null);
  const [leads, setLeads] = useState<Lead[]>([]);
  const [activities, setActivities] = useState<ActivityLog[]>([]);
  const [loading, setLoading] = useState(true);
  const [recalculating, setRecalculating] = useState(false);
  const [isDark, setIsDark] = useState(() => document.documentElement.classList.contains('dark'));

  useEffect(() => {
    const handleThemeChange = (e: any) => setIsDark(e.detail?.isDark);
    window.addEventListener('themeChanged', handleThemeChange);
    return () => window.removeEventListener('themeChanged', handleThemeChange);
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [r, a, l, act] = await Promise.all([
        api.getDashboardReport(),
        api.getAnalyticsReport(),
        api.getLeads(),
        api.getRecentActivities()
      ]);
      setReport(r);
      setAnalytics(a);
      setLeads(l);
      setActivities(act.data || []);
    } catch (err) {
      console.error('Failed to load dashboard:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleRecalculate = async () => {
    setRecalculating(true);
    try {
      await api.recalculateScores();
      await loadData();
    } finally {
      setRecalculating(false);
    }
  };

  const chartTheme = {
    text: isDark ? '#94a3b8' : '#64748b',
    grid: isDark ? 'rgba(51, 65, 85, 0.4)' : 'rgba(226, 232, 240, 0.8)',
    tooltipBg: isDark ? '#1e293b' : '#ffffff',
    tooltipText: isDark ? '#f8fafc' : '#0f172a',
    tooltipBorder: isDark ? '#334155' : '#e2e8f0'
  };

  // 1. Monthly Deal Velocity Data
  const velocityData = {
    labels: analytics?.monthlyDealVelocity.map(x => x.month) || [],
    datasets: [
      {
        label: 'Deals Created',
        data: analytics?.monthlyDealVelocity.map(x => x.createdCount) || [],
        backgroundColor: 'rgba(99, 102, 241, 0.8)',
        borderRadius: 6
      },
      {
        label: 'Deals Won',
        data: analytics?.monthlyDealVelocity.map(x => x.wonCount) || [],
        backgroundColor: 'rgba(16, 185, 129, 0.85)',
        borderRadius: 6
      }
    ]
  };

  // 2. Activity Breakdown Data
  const activityData = {
    labels: analytics?.salesActivityDistribution.map(x => x.type) || [],
    datasets: [
      {
        data: analytics?.salesActivityDistribution.map(x => x.count) || [],
        backgroundColor: ['#6366f1', '#10b981', '#38bdf8', '#f59e0b', '#8b5cf6', '#94a3b8'],
        borderColor: isDark ? '#1e293b' : '#ffffff',
        borderWidth: 2
      }
    ]
  };

  // 3. Funnel Chart Data
  const funnelData = {
    labels: analytics?.leadConversionFunnel.map(x => x.stage.replace(/([A-Z])/g, ' $1').trim()) || [],
    datasets: [
      {
        label: 'Deals in Stage',
        data: analytics?.leadConversionFunnel.map(x => x.count) || [],
        backgroundColor: [
          'rgba(59, 130, 246, 0.85)',
          'rgba(14, 165, 233, 0.85)',
          'rgba(99, 102, 241, 0.85)',
          'rgba(245, 158, 11, 0.85)',
          'rgba(16, 185, 129, 0.85)'
        ],
        borderRadius: 6
      }
    ]
  };

  const getActivityIcon = (type: string) => {
    switch (type) {
      case 'Call': return <Phone className="w-3.5 h-3.5 text-emerald-500" />;
      case 'Email': return <Mail className="w-3.5 h-3.5 text-blue-500" />;
      case 'Meeting': return <Calendar className="w-3.5 h-3.5 text-purple-500" />;
      case 'Note': return <FileText className="w-3.5 h-3.5 text-amber-500" />;
      case 'StatusChanged': return <GitCommit className="w-3.5 h-3.5 text-brand-500" />;
      default: return <Cog className="w-3.5 h-3.5 text-slate-400" />;
    }
  };

  return (
    <div className="space-y-8">
      {/* Header & Controls */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 className="text-2xl sm:text-3xl font-extrabold tracking-tight text-slate-900 dark:text-white">
            Sales Intelligence & Analytics
          </h1>
          <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-1">
            Real-time pipeline analytics, deal velocity, and automated lead scoring.
          </p>
        </div>
        <div className="flex items-center space-x-3">
          <button
            onClick={loadData}
            disabled={loading}
            className="p-2 border border-slate-200 dark:border-slate-800 rounded-xl text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
            title="Refresh metrics"
          >
            <RotateCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          </button>
          <button
            onClick={handleRecalculate}
            disabled={recalculating}
            className="inline-flex items-center px-4 py-2 rounded-xl text-xs font-semibold text-white bg-gradient-to-r from-brand-600 to-indigo-600 hover:from-brand-700 hover:to-indigo-700 shadow-md shadow-brand-500/25 transition-all disabled:opacity-50"
          >
            <Zap className={`w-3.5 h-3.5 mr-1.5 ${recalculating ? 'animate-bounce' : ''}`} />
            {recalculating ? 'Recalculating...' : 'Recalculate AI Scores'}
          </button>
        </div>
      </div>

      {/* KPI Cards */}
      <section className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
        <div className="glass-panel p-5 shadow-sm">
          <div className="flex items-center justify-between">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">Total Leads</span>
            <div className="w-9 h-9 rounded-xl bg-blue-100 dark:bg-blue-950/60 text-blue-600 dark:text-blue-400 flex items-center justify-center">
              <Users className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3 flex items-baseline justify-between">
            <span className="text-3xl font-extrabold text-slate-900 dark:text-white">{report?.totalLeads ?? '--'}</span>
            <span className="text-xs font-medium text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-950/40 px-2 py-0.5 rounded-full">Pipeline</span>
          </div>
        </div>

        <div className="glass-panel p-5 shadow-sm">
          <div className="flex items-center justify-between">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">Open Pipeline</span>
            <div className="w-9 h-9 rounded-xl bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
              <TrendingUp className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3 flex items-baseline justify-between">
            <span className="text-3xl font-extrabold text-slate-900 dark:text-white">{report ? money(report.openPipeline) : '--'}</span>
            <span className="text-xs font-medium text-emerald-600 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-950/40 px-2 py-0.5 rounded-full">In Flight</span>
          </div>
        </div>

        <div className="glass-panel p-5 shadow-sm">
          <div className="flex items-center justify-between">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">Win Rate</span>
            <div className="w-9 h-9 rounded-xl bg-brand-100 dark:bg-brand-950/60 text-brand-600 dark:text-brand-400 flex items-center justify-center">
              <Award className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3 flex items-baseline justify-between">
            <span className="text-3xl font-extrabold text-slate-900 dark:text-white">{report ? `${report.conversionRate}%` : '--'}</span>
            <span className="text-xs font-medium text-brand-600 dark:text-brand-400 bg-brand-50 dark:bg-brand-950/40 px-2 py-0.5 rounded-full">Closed Won</span>
          </div>
        </div>

        <div className="glass-panel p-5 shadow-sm">
          <div className="flex items-center justify-between">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">Overdue Follow-ups</span>
            <div className="w-9 h-9 rounded-xl bg-rose-100 dark:bg-rose-950/60 text-rose-600 dark:text-rose-400 flex items-center justify-center">
              <AlertTriangle className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3 flex items-baseline justify-between">
            <span className="text-3xl font-extrabold text-slate-900 dark:text-white">{report?.overdueTasks ?? '--'}</span>
            <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${(report?.overdueTasks || 0) > 0 ? 'bg-rose-100 text-rose-700 dark:bg-rose-950/60 dark:text-rose-300 animate-pulse' : 'bg-slate-100 text-slate-500 dark:bg-slate-800'}`}>
              {(report?.overdueTasks || 0) > 0 ? 'Action Due' : 'Clear'}
            </span>
          </div>
        </div>
      </section>

      {/* Charts Section */}
      <section className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <div className="lg:col-span-7 glass-panel p-6 shadow-sm flex flex-col">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h2 className="text-base font-bold text-slate-900 dark:text-white">Monthly Deal Velocity</h2>
              <p className="text-xs text-slate-500 dark:text-slate-400">Created vs. Won deal volume</p>
            </div>
            <BarChart3 className="w-4 h-4 text-brand-500" />
          </div>
          <div className="flex-1 min-h-[280px]">
            <Bar
              data={velocityData}
              options={{
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                  legend: { labels: { color: chartTheme.text, font: { size: 11, family: 'Inter' } } },
                  tooltip: { backgroundColor: chartTheme.tooltipBg, titleColor: chartTheme.tooltipText, bodyColor: chartTheme.tooltipText }
                },
                scales: {
                  x: { grid: { display: false }, ticks: { color: chartTheme.text } },
                  y: { grid: { color: chartTheme.grid }, ticks: { color: chartTheme.text } }
                }
              }}
            />
          </div>
        </div>

        <div className="lg:col-span-5 glass-panel p-6 shadow-sm flex flex-col">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h2 className="text-base font-bold text-slate-900 dark:text-white">Sales Activity Distribution</h2>
              <p className="text-xs text-slate-500 dark:text-slate-400">Past 30 days engagement</p>
            </div>
            <PieIcon className="w-4 h-4 text-emerald-500" />
          </div>
          <div className="flex-1 min-h-[280px] flex items-center justify-center">
            <Doughnut
              data={activityData}
              options={{
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                  legend: { position: 'bottom', labels: { color: chartTheme.text, font: { size: 10 } } }
                },
                cutout: '65%'
              }}
            />
          </div>
        </div>

        <div className="lg:col-span-12 glass-panel p-6 shadow-sm">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h2 className="text-base font-bold text-slate-900 dark:text-white">Lead Conversion Funnel</h2>
              <p className="text-xs text-slate-500 dark:text-slate-400">Pipeline progression across stages</p>
            </div>
          </div>
          <div className="min-h-[220px]">
            <Bar
              data={funnelData}
              options={{
                indexAxis: 'y',
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                  x: { grid: { color: chartTheme.grid }, ticks: { color: chartTheme.text } },
                  y: { grid: { display: false }, ticks: { color: chartTheme.text } }
                }
              }}
            />
          </div>
        </div>
      </section>

      {/* Prioritized Leads & Activity Feed */}
      <section className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Leads Table (7 cols) */}
        <div className="lg:col-span-7 glass-panel shadow-sm overflow-hidden flex flex-col">
          <div className="p-5 border-b border-slate-200 dark:border-slate-800 flex items-center justify-between">
            <h2 className="text-base font-bold text-slate-900 dark:text-white">High Priority Leads</h2>
            <span className="text-xs font-bold px-2 py-0.5 rounded-full bg-brand-50 text-brand-700 dark:bg-brand-950/60 dark:text-brand-300 border border-brand-200 dark:border-brand-800">
              Top Ranked
            </span>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead>
                <tr className="bg-slate-100/60 dark:bg-slate-900/60 text-slate-500 border-b border-slate-200 dark:border-slate-800 font-bold uppercase tracking-wider">
                  <th className="py-3 px-4">Deal</th>
                  <th className="py-3 px-4">Contact</th>
                  <th className="py-3 px-4">Value</th>
                  <th className="py-3 px-4">Stage</th>
                  <th className="py-3 px-4 text-center">Score</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800 text-slate-700 dark:text-slate-300">
                {leads.slice(0, 8).map((lead) => (
                  <tr key={lead.id} className="hover:bg-slate-50/60 dark:hover:bg-slate-800/40 transition-colors">
                    <td className="py-3 px-4">
                      <div className="font-bold text-slate-900 dark:text-white">{lead.companyName}</div>
                      <div className="text-[11px] text-slate-400 font-normal">{lead.title}</div>
                    </td>
                    <td className="py-3 px-4">{lead.contactName}</td>
                    <td className="py-3 px-4 font-bold text-emerald-600 dark:text-emerald-400">{money(lead.value)}</td>
                    <td className="py-3 px-4">
                      <span className="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700">
                        {lead.status}
                      </span>
                    </td>
                    <td className="py-3 px-4 text-center font-extrabold text-brand-600 dark:text-brand-400">
                      {lead.priorityScore}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Live Timeline (5 cols) */}
        <div className="lg:col-span-5 glass-panel p-5 shadow-sm flex flex-col">
          <div className="flex items-center justify-between mb-4 pb-3 border-b border-slate-200 dark:border-slate-800">
            <h2 className="text-base font-bold text-slate-900 dark:text-white">Customer Activity Stream</h2>
            <span className="w-2.5 h-2.5 rounded-full bg-emerald-500 pulse-green" />
          </div>
          <div className="space-y-3.5 overflow-y-auto max-h-[440px] pr-1">
            {activities.slice(0, 10).map((item) => (
              <div key={item.id} className="flex items-start space-x-3 text-xs pb-3 border-b border-slate-100 dark:border-slate-800/60 last:border-0">
                <div className="w-7 h-7 rounded-lg bg-slate-100 dark:bg-slate-800 flex items-center justify-center flex-shrink-0">
                  {getActivityIcon(item.type)}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-baseline justify-between">
                    <span className="font-bold text-slate-900 dark:text-white truncate">{item.lead || 'General'}</span>
                    <time className="text-[10px] text-slate-400">
                      {new Date(item.occurredUtc).toLocaleDateString([], { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })}
                    </time>
                  </div>
                  <p className="text-slate-600 dark:text-slate-300 mt-0.5 line-clamp-2">{item.message}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>
    </div>
  );
};
