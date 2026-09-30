import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { TrendingUp, Lock, Mail, ArrowRight, AlertCircle, KeyRound } from 'lucide-react';

export const LoginPage: React.FC = () => {
  const [email, setEmail] = useState('team@paruluniversity.ac.in');
  const [password, setPassword] = useState('Atul6001@');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);

    try {
      await login(email, password);
      navigate('/dashboard');
    } catch (err: any) {
      setError(err.message || 'Invalid email or password.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-slate-950 flex items-center justify-center p-4">
      <main className="w-full max-w-md glass-panel p-8 shadow-2xl border border-slate-200 dark:border-slate-800">
        <div className="text-center mb-8">
          <div className="inline-flex w-14 h-14 rounded-2xl bg-gradient-to-tr from-brand-600 to-indigo-500 items-center justify-center text-white shadow-lg shadow-brand-500/30 mb-3">
            <TrendingUp className="w-7 h-7" />
          </div>
          <h1 className="text-2xl font-extrabold tracking-tight text-slate-900 dark:text-white">ApexCRM</h1>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">Decoupled React 18 + Vite SPA Workspace</p>
        </div>

        {error && (
          <div className="mb-5 p-3 rounded-xl bg-rose-50 dark:bg-rose-950/60 border border-rose-200 dark:border-rose-800 text-rose-700 dark:text-rose-300 text-xs flex items-center gap-2">
            <AlertCircle className="w-4 h-4 flex-shrink-0" />
            <span>{error}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">Email address</label>
            <div className="relative">
              <Mail className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400" />
              <input
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="user@crm.local"
                className="w-full pl-9 pr-3.5 py-2.5 text-xs rounded-xl bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 text-slate-900 dark:text-white placeholder-slate-400 focus:ring-2 focus:ring-brand-500 focus:outline-none"
              />
            </div>
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">Password</label>
            <div className="relative">
              <Lock className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400" />
              <input
                type="password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••••••"
                className="w-full pl-9 pr-3.5 py-2.5 text-xs rounded-xl bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 text-slate-900 dark:text-white placeholder-slate-400 focus:ring-2 focus:ring-brand-500 focus:outline-none"
              />
            </div>
          </div>

          <button
            type="submit"
            disabled={loading}
            className="w-full py-2.5 px-4 text-xs font-bold rounded-xl text-white bg-gradient-to-r from-brand-600 to-indigo-600 hover:from-brand-700 hover:to-indigo-700 shadow-md shadow-brand-500/25 focus:ring-2 focus:ring-brand-500 transition-all flex items-center justify-center space-x-2 disabled:opacity-50"
          >
            <span>{loading ? 'Authenticating...' : 'Sign in to Workspace'}</span>
            <ArrowRight className="w-3.5 h-3.5" />
          </button>
        </form>

        <div className="mt-6 pt-5 border-t border-slate-200/80 dark:border-slate-800/80 text-[11px] text-slate-500 dark:text-slate-400 space-y-1">
          <div className="font-bold text-slate-700 dark:text-slate-300 uppercase tracking-wider text-[10px] mb-1.5 flex items-center">
            <KeyRound className="w-3.5 h-3.5 text-brand-500 mr-1.5" />
            Seed Accounts:
          </div>
          <div className="flex items-center justify-between bg-slate-100/70 dark:bg-slate-900/60 p-2 rounded-lg font-mono">
            <span>team@paruluniversity.ac.in</span>
            <span className="text-slate-400">Atul6001@</span>
          </div>
          <div className="flex items-center justify-between bg-slate-100/70 dark:bg-slate-900/60 p-2 rounded-lg font-mono">
            <span>atul@crm.local</span>
            <span className="text-slate-400">ChangeMe123!</span>
          </div>
        </div>
      </main>
    </div>
  );
};
