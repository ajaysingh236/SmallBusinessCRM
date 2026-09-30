import React, { useEffect, useState } from 'react';
import { api } from '../services/api';
import { AuditLog, Lead } from '../types';
import {
  FileSpreadsheet,
  Upload,
  Download,
  UserPlus,
  Trash2,
  Undo2,
  Fingerprint,
  RotateCw,
  CheckCircle2
} from 'lucide-react';

export const AdminPage: React.FC = () => {
  const [auditLogs, setAuditLogs] = useState<AuditLog[]>([]);
  const [trashLeads, setTrashLeads] = useState<Lead[]>([]);
  const [loading, setLoading] = useState(true);

  // CSV Import State
  const [file, setFile] = useState<File | null>(null);
  const [importStatus, setImportStatus] = useState<string | null>(null);
  const [importing, setImporting] = useState(false);

  // User Creation State
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState('SalesRepresentative');
  const [userStatus, setUserStatus] = useState<string | null>(null);
  const [creatingUser, setCreatingUser] = useState(false);

  const loadData = async () => {
    try {
      setLoading(true);
      const [logs, trash] = await Promise.all([api.getAuditLogs(), api.getTrash()]);
      setAuditLogs(logs);
      setTrashLeads(trash);
    } catch (err) {
      console.error('Failed to load admin data:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleImport = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!file) return;

    setImporting(true);
    setImportStatus('Processing CSV import...');
    try {
      const res = await api.importLeads(file);
      setImportStatus(`Success: Imported ${res.imported} leads (${res.skipped} skipped).`);
      setFile(null);
      await loadData();
    } catch (err: any) {
      setImportStatus(`Error: ${err.message || 'Import failed'}`);
    } finally {
      setImporting(false);
    }
  };

  const handleCreateUser = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreatingUser(true);
    setUserStatus(null);
    try {
      await api.createUser({ fullName, email, password, role });
      setUserStatus(`User ${email} created successfully!`);
      setFullName('');
      setEmail('');
      setPassword('');
      await loadData();
    } catch (err: any) {
      setUserStatus(`Error: ${err.message || 'Failed to create user'}`);
    } finally {
      setCreatingUser(false);
    }
  };

  const handleRestore = async (id: number) => {
    try {
      await api.restoreLead(id);
      await loadData();
    } catch (err) {
      console.error('Failed to restore lead:', err);
    }
  };

  return (
    <div className="space-y-8">
      {/* Title */}
      <div>
        <h1 className="text-2xl sm:text-3xl font-extrabold tracking-tight text-slate-900 dark:text-white">
          Workspace Governance & Administration
        </h1>
        <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-1">
          CSV batch data operations, identity provisioning, soft-delete restoration, and audit telemetry.
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* CSV Management (5 cols) */}
        <div className="lg:col-span-5 glass-panel p-6 shadow-sm flex flex-col justify-between">
          <div>
            <div className="flex items-center space-x-2.5 mb-2">
              <div className="w-8 h-8 rounded-xl bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                <FileSpreadsheet className="w-4 h-4" />
              </div>
              <h2 className="text-base font-bold text-slate-900 dark:text-white">Bulk Data Management</h2>
            </div>
            <p className="text-xs text-slate-500 dark:text-slate-400 mb-4">
              Import opportunities with CSV formula injection escaping. Headers: Title, CompanyName, ContactName, Email, Phone, Value, Status.
            </p>

            <form onSubmit={handleImport} className="space-y-3">
              <input
                type="file"
                accept=".csv"
                onChange={(e) => setFile(e.target.files?.[0] || null)}
                className="block w-full text-xs text-slate-500 dark:text-slate-400 file:mr-4 file:py-2 file:px-4 file:rounded-xl file:border-0 file:text-xs file:font-semibold file:bg-brand-50 file:text-brand-700 hover:file:bg-brand-100 dark:file:bg-brand-950 dark:file:text-brand-300 border border-slate-200 dark:border-slate-800 rounded-xl p-1 bg-white dark:bg-slate-900 cursor-pointer"
              />
              <button
                type="submit"
                disabled={!file || importing}
                className="w-full inline-flex items-center justify-center px-4 py-2 text-xs font-semibold rounded-xl text-white bg-brand-600 hover:bg-brand-700 shadow-sm transition-colors disabled:opacity-50"
              >
                <Upload className="w-3.5 h-3.5 mr-2" />
                {importing ? 'Importing...' : 'Upload & Process CSV'}
              </button>
              {importStatus && (
                <p className="text-xs font-semibold text-slate-700 dark:text-slate-300 mt-2">{importStatus}</p>
              )}
            </form>
          </div>

          <div className="pt-4 border-t border-slate-200 dark:border-slate-800 mt-6">
            <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400 block mb-2">Export Data</span>
            <div className="flex gap-2">
              <a
                href="/api/data/leads/export"
                download
                className="flex-1 inline-flex items-center justify-center px-3 py-2 text-xs font-semibold rounded-xl border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
              >
                <Download className="w-3.5 h-3.5 mr-1.5 text-emerald-500" />
                Leads CSV
              </a>
              <a
                href="/api/data/activities/export"
                download
                className="flex-1 inline-flex items-center justify-center px-3 py-2 text-xs font-semibold rounded-xl border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
              >
                <Download className="w-3.5 h-3.5 mr-1.5 text-blue-500" />
                Timeline CSV
              </a>
            </div>
          </div>
        </div>

        {/* User Provisioning (7 cols) */}
        <div className="lg:col-span-7 glass-panel p-6 shadow-sm">
          <div className="flex items-center space-x-2.5 mb-2">
            <div className="w-8 h-8 rounded-xl bg-indigo-100 dark:bg-indigo-950/60 text-brand-600 dark:text-brand-400 flex items-center justify-center">
              <UserPlus className="w-4 h-4" />
            </div>
            <h2 className="text-base font-bold text-slate-900 dark:text-white">Provision CRM Account</h2>
          </div>
          <p className="text-xs text-slate-500 dark:text-slate-400 mb-5">
            Provision team members with scoped role-based access control.
          </p>

          <form onSubmit={handleCreateUser} className="space-y-4">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-medium text-slate-700 dark:text-slate-300 mb-1">Full Name</label>
                <input
                  required
                  value={fullName}
                  onChange={(e) => setFullName(e.target.value)}
                  placeholder="e.g. Maya Sharma"
                  className="w-full px-3 py-2 text-xs rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-slate-900 dark:text-white focus:ring-2 focus:ring-brand-500"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-700 dark:text-slate-300 mb-1">Work Email</label>
                <input
                  type="email"
                  required
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="maya@crm.local"
                  className="w-full px-3 py-2 text-xs rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-slate-900 dark:text-white focus:ring-2 focus:ring-brand-500"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-700 dark:text-slate-300 mb-1">Temporary Password</label>
                <input
                  type="password"
                  required
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Min 9 chars with symbol"
                  className="w-full px-3 py-2 text-xs rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-slate-900 dark:text-white focus:ring-2 focus:ring-brand-500"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-700 dark:text-slate-300 mb-1">Role Assignment</label>
                <select
                  value={role}
                  onChange={(e) => setRole(e.target.value)}
                  className="w-full px-3 py-2 text-xs rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-slate-900 dark:text-white focus:ring-2 focus:ring-brand-500"
                >
                  <option value="SalesRepresentative">Sales Representative</option>
                  <option value="Manager">Sales Manager</option>
                  <option value="Admin">Administrator</option>
                </select>
              </div>
            </div>
            <div className="flex items-center justify-between pt-2">
              <span className="text-xs font-semibold text-brand-600 dark:text-brand-400">{userStatus}</span>
              <button
                type="submit"
                disabled={creatingUser}
                className="inline-flex items-center px-5 py-2 text-xs font-semibold rounded-xl text-white bg-slate-900 dark:bg-white dark:text-slate-900 hover:bg-slate-800 dark:hover:bg-slate-100 shadow-sm transition-colors disabled:opacity-50"
              >
                <CheckCircle2 className="w-3.5 h-3.5 mr-1.5" />
                {creatingUser ? 'Creating...' : 'Create Account'}
              </button>
            </div>
          </form>
        </div>

        {/* Soft-Deleted Leads (12 cols) */}
        <div className="lg:col-span-12 glass-panel p-6 shadow-sm">
          <div className="flex items-center justify-between mb-4">
            <div className="flex items-center space-x-2.5">
              <div className="w-8 h-8 rounded-xl bg-rose-100 dark:bg-rose-950/60 text-rose-600 dark:text-rose-400 flex items-center justify-center">
                <Trash2 className="w-4 h-4" />
              </div>
              <div>
                <h2 className="text-base font-bold text-slate-900 dark:text-white">Deleted Opportunities Recovery</h2>
                <p className="text-xs text-slate-500 dark:text-slate-400">Soft-deleted items eligible for pipeline restoration</p>
              </div>
            </div>
            <span className="text-xs font-bold px-2.5 py-1 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
              {trashLeads.length} in trash
            </span>
          </div>
          <div className="space-y-2 max-h-48 overflow-y-auto pr-1">
            {trashLeads.length === 0 ? (
              <div className="py-6 text-center text-xs text-slate-400 bg-slate-50 dark:bg-slate-900/40 rounded-xl border border-dashed border-slate-200 dark:border-slate-800">
                No deleted leads in trash.
              </div>
            ) : (
              trashLeads.map((lead) => (
                <div
                  key={lead.id}
                  className="flex items-center justify-between p-3 rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-sm"
                >
                  <div>
                    <span className="font-bold text-xs text-slate-800 dark:text-slate-200 block">{lead.companyName}</span>
                    <span className="text-[11px] text-slate-400">{lead.title}</span>
                  </div>
                  <button
                    onClick={() => handleRestore(lead.id)}
                    className="inline-flex items-center px-3 py-1.5 rounded-lg text-xs font-semibold bg-emerald-50 text-emerald-700 hover:bg-emerald-100 dark:bg-emerald-950/60 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800 transition-colors"
                  >
                    <Undo2 className="w-3.5 h-3.5 mr-1" />
                    Restore
                  </button>
                </div>
              ))
            )}
          </div>
        </div>

        {/* Audit Trail Log (12 cols) */}
        <div className="lg:col-span-12 glass-panel shadow-sm overflow-hidden">
          <div className="p-6 border-b border-slate-200 dark:border-slate-800 flex items-center justify-between">
            <div className="flex items-center space-x-2.5">
              <div className="w-8 h-8 rounded-xl bg-purple-100 dark:bg-purple-950/60 text-purple-600 dark:text-purple-400 flex items-center justify-center">
                <Fingerprint className="w-4 h-4" />
              </div>
              <div>
                <h2 className="text-base font-bold text-slate-900 dark:text-white">Audit Trail & Mutation History</h2>
                <p className="text-xs text-slate-500 dark:text-slate-400">Tamper-evident log of changes across the organization</p>
              </div>
            </div>
            <button
              onClick={loadData}
              className="p-2 text-slate-500 hover:text-slate-700 dark:hover:text-slate-200 text-xs rounded-xl hover:bg-slate-100 dark:hover:bg-slate-800"
            >
              <RotateCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            </button>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead>
                <tr className="bg-slate-100/60 dark:bg-slate-900/60 text-slate-500 border-b border-slate-200 dark:border-slate-800 font-bold uppercase tracking-wider">
                  <th className="py-3 px-4">Time</th>
                  <th className="py-3 px-4">Actor</th>
                  <th className="py-3 px-4">Action</th>
                  <th className="py-3 px-4">Entity</th>
                  <th className="py-3 px-4">State Delta</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800 text-slate-700 dark:text-slate-300">
                {auditLogs.map((log) => (
                  <tr key={log.id} className="hover:bg-slate-50/60 dark:hover:bg-slate-800/40 transition-colors">
                    <td className="py-3 px-4 font-mono text-[11px] text-slate-400">
                      {new Date(log.occurredUtc).toLocaleString()}
                    </td>
                    <td className="py-3 px-4 font-semibold text-slate-800 dark:text-slate-200">
                      {log.actor || 'System'}
                    </td>
                    <td className="py-3 px-4">
                      <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-brand-50 text-brand-700 dark:bg-brand-950/60 dark:text-brand-300 border border-brand-200 dark:border-brand-800">
                        {log.action}
                      </span>
                    </td>
                    <td className="py-3 px-4">
                      {log.entityName} <span className="text-slate-400">#{log.entityId}</span>
                    </td>
                    <td className="py-3 px-4 text-slate-400 max-w-xs truncate" title={log.newValuesJson}>
                      {log.newValuesJson ? log.newValuesJson.replace(/[{}"\\]/g, ' ').trim() : 'N/A'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
};
