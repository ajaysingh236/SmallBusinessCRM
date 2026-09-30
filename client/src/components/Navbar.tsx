import React, { useState } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import {
  TrendingUp,
  LayoutDashboard,
  Kanban,
  ShieldAlert,
  Sun,
  Moon,
  LogOut,
  Menu,
  X
} from 'lucide-react';

interface NavbarProps {
  connectionState?: 'connected' | 'reconnecting' | 'disconnected';
}

export const Navbar: React.FC<NavbarProps> = ({ connectionState = 'connected' }) => {
  const { user, logout, hasRole } = useAuth();
  const navigate = useNavigate();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [isDark, setIsDark] = useState(() => document.documentElement.classList.contains('dark'));

  const toggleTheme = () => {
    const nextDark = document.documentElement.classList.toggle('dark');
    localStorage.setItem('crm_theme', nextDark ? 'dark' : 'light');
    setIsDark(nextDark);
    window.dispatchEvent(new CustomEvent('themeChanged', { detail: { isDark: nextDark } }));
  };

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  const navLinkClass = ({ isActive }: { isActive: boolean }) =>
    `inline-flex items-center px-3 py-2 rounded-xl text-xs font-semibold transition-all ${
      isActive
        ? 'bg-brand-50 text-brand-700 dark:bg-brand-950/60 dark:text-brand-300 shadow-sm'
        : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white hover:bg-slate-100/70 dark:hover:bg-slate-800/60'
    }`;

  return (
    <header className="sticky top-0 z-40 glass-nav border-b border-slate-200 dark:border-slate-800">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16">
          {/* Logo & Brand */}
          <div className="flex items-center space-x-3">
            <NavLink to="/dashboard" className="flex items-center space-x-3 group">
              <div className="w-10 h-10 rounded-xl bg-gradient-to-tr from-brand-600 to-indigo-500 flex items-center justify-center text-white shadow-md shadow-brand-500/25 group-hover:scale-105 transition-transform">
                <TrendingUp className="w-5 h-5" />
              </div>
              <div>
                <span className="text-base font-extrabold tracking-tight text-slate-900 dark:text-white">ApexCRM</span>
                <span className="hidden sm:inline-block ml-2 text-[10px] font-bold px-2 py-0.5 rounded-full bg-brand-100 text-brand-700 dark:bg-brand-900/50 dark:text-brand-300">
                  SPA
                </span>
              </div>
            </NavLink>

            {/* Desktop Navigation Links */}
            <nav className="hidden md:flex ml-8 space-x-1.5" aria-label="Main Navigation">
              <NavLink to="/dashboard" className={navLinkClass}>
                <LayoutDashboard className="w-4 h-4 mr-1.5 opacity-70" />
                Dashboard
              </NavLink>
              <NavLink to="/pipeline" className={navLinkClass}>
                <Kanban className="w-4 h-4 mr-1.5 opacity-70" />
                Pipeline
              </NavLink>
              {hasRole('Admin') && (
                <NavLink to="/admin" className={navLinkClass}>
                  <ShieldAlert className="w-4 h-4 mr-1.5 opacity-70" />
                  Admin
                </NavLink>
              )}
            </nav>
          </div>

          {/* Right Header Actions */}
          <div className="flex items-center space-x-3">
            {/* Realtime Connection Status */}
            <div
              className={`hidden sm:flex items-center space-x-1.5 px-2.5 py-1 rounded-full text-[11px] font-semibold border ${
                connectionState === 'connected'
                  ? 'bg-emerald-50 text-emerald-700 border-emerald-200 dark:bg-emerald-950/60 dark:text-emerald-300 dark:border-emerald-800'
                  : connectionState === 'reconnecting'
                  ? 'bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-950/60 dark:text-amber-300 dark:border-amber-800'
                  : 'bg-rose-50 text-rose-700 border-rose-200 dark:bg-rose-950/60 dark:text-rose-300 dark:border-rose-800'
              }`}
            >
              <span
                className={`w-2 h-2 rounded-full ${
                  connectionState === 'connected'
                    ? 'bg-emerald-500 pulse-green'
                    : connectionState === 'reconnecting'
                    ? 'bg-amber-500 animate-ping'
                    : 'bg-rose-500'
                }`}
              />
              <span className="capitalize">{connectionState === 'connected' ? 'Live Sync' : connectionState}</span>
            </div>

            {/* Dark Mode Toggle */}
            <button
              type="button"
              onClick={toggleTheme}
              className="p-2 rounded-xl text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 focus:outline-none transition-colors"
              aria-label="Toggle theme"
            >
              {isDark ? <Sun className="w-4 h-4 text-amber-400" /> : <Moon className="w-4 h-4 text-slate-600 dark:text-slate-300" />}
            </button>

            {/* User Badge / Logout */}
            {user && (
              <div className="flex items-center space-x-2 pl-2 border-l border-slate-200 dark:border-slate-800">
                <div className="hidden lg:flex flex-col text-right">
                  <span className="text-xs font-bold text-slate-900 dark:text-white leading-none">{user.fullName}</span>
                  <span className="text-[10px] text-slate-400 font-medium capitalize">{user.roles[0] || 'User'}</span>
                </div>
                <button
                  type="button"
                  onClick={handleLogout}
                  className="p-2 text-slate-500 hover:text-rose-600 dark:text-slate-400 dark:hover:text-rose-400 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-xl transition-colors"
                  title="Sign out"
                >
                  <LogOut className="w-4 h-4" />
                </button>
              </div>
            )}

            {/* Mobile Menu Trigger */}
            <button
              type="button"
              onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
              className="md:hidden p-2 rounded-xl text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800"
            >
              {mobileMenuOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
            </button>
          </div>
        </div>

        {/* Mobile Navigation Dropdown */}
        {mobileMenuOpen && (
          <div className="md:hidden py-3 px-2 space-y-1 border-t border-slate-200 dark:border-slate-800">
            <NavLink
              to="/dashboard"
              onClick={() => setMobileMenuOpen(false)}
              className="flex items-center px-3 py-2 rounded-lg text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800"
            >
              <LayoutDashboard className="w-4 h-4 mr-2 text-brand-500" />
              Dashboard
            </NavLink>
            <NavLink
              to="/pipeline"
              onClick={() => setMobileMenuOpen(false)}
              className="flex items-center px-3 py-2 rounded-lg text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800"
            >
              <Kanban className="w-4 h-4 mr-2 text-brand-500" />
              Pipeline
            </NavLink>
            {hasRole('Admin') && (
              <NavLink
                to="/admin"
                onClick={() => setMobileMenuOpen(false)}
                className="flex items-center px-3 py-2 rounded-lg text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800"
              >
                <ShieldAlert className="w-4 h-4 mr-2 text-brand-500" />
                Admin
              </NavLink>
            )}
          </div>
        )}
      </div>
    </header>
  );
};
