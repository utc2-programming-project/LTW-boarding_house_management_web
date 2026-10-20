import React, { useState } from 'react';
import type { UserRole, SimulationState, Language } from '../types/index.ts';
import { translations } from '../utils/i18n.ts';

interface SandboxPanelProps {
  role: UserRole;
  setRole: (role: UserRole) => void;
  simulation: SimulationState;
  setSimulation: (state: SimulationState) => void;
  onOpenPicker: () => void;
  lang: Language;
}

export const SandboxPanel: React.FC<SandboxPanelProps> = ({
  role,
  setRole,
  simulation,
  setSimulation,
  onOpenPicker,
  lang,
}) => {
  const t = translations[lang];
  const [collapsed, setCollapsed] = useState(() => typeof window !== 'undefined' && window.innerWidth < 768);

  const roles: { id: UserRole; label: string }[] = [
    { id: 'landlord', label: t.role_landlord },
    { id: 'manager', label: t.role_manager },
    { id: 'tenant', label: t.role_tenant },
    { id: 'admin', label: t.role_admin },
  ];

  const simulationStates: { id: SimulationState; label: string }[] = [
    { id: 'normal', label: t.state_normal },
    { id: 'loading', label: t.state_loading },
    { id: 'empty', label: t.state_empty },
    { id: 'error', label: t.state_error },
    { id: 'conflict', label: t.state_conflict },
    { id: 'offline', label: t.state_offline },
  ];

  if (collapsed) {
    return (
      <div className="fixed bottom-6 right-6 z-50">
        <button
          onClick={() => setCollapsed(false)}
          className="h-10 px-3.5 rounded-xl bg-white dark:bg-[#191c1e] text-[#191c1e] dark:text-neutral-100 shadow-[0_4px_16px_rgba(0,0,0,0.12)] border border-neutral-200/80 dark:border-neutral-700 flex items-center gap-2 hover:bg-[#f2f4f6] dark:hover:bg-neutral-800 transition-all cursor-pointer font-semibold text-xs"
          title={t.sandbox_title}
        >
          <span className="material-symbols-outlined text-[#3525cd] dark:text-indigo-400 text-[18px]">
            developer_mode
          </span>
          <span>{t.sandbox_title}</span>
          <span className="w-2 h-2 rounded-full bg-[#059669]"></span>
        </button>
      </div>
    );
  }

  return (
    <div
      className="fixed bottom-6 right-6 z-50 bg-white dark:bg-[#191c1e] rounded-xl shadow-[0_4px_16px_rgba(0,0,0,0.12)] border border-neutral-100 dark:border-neutral-800 p-4 w-[320px] flex flex-col gap-2.5 transition-colors"
      id="demo-sandbox-panel"
    >
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-1.5">
          <span className="material-symbols-outlined text-[#3525cd] dark:text-indigo-400 text-[18px]">
            developer_mode
          </span>
          <span className="font-bold text-sm text-[#191c1e] dark:text-neutral-100">
            {t.sandbox_title}
          </span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="px-2 py-0.5 rounded-full bg-[#e2dfff] dark:bg-indigo-950/80 font-mono text-[11px] text-[#0f0069] dark:text-indigo-300 font-semibold">
            {t.sandbox_badge}
          </span>
          <button
            onClick={() => setCollapsed(true)}
            className="text-[#777587] hover:text-[#191c1e] dark:hover:text-white p-0.5 rounded cursor-pointer"
            title="Minimize"
          >
            <span className="material-symbols-outlined text-[16px]">expand_more</span>
          </button>
        </div>
      </div>

      {/* RBAC Role Selector */}
      <div className="flex flex-col gap-1">
        <label className="text-[11px] font-semibold text-[#565e74] dark:text-neutral-400">
          {t.sandbox_rbac_label}
        </label>
        <div className="grid grid-cols-2 gap-1">
          {roles.map((r) => {
            const isActive = role === r.id;
            return (
              <button
                key={r.id}
                onClick={() => setRole(r.id)}
                type="button"
                className={`px-2 py-1.5 rounded-lg text-xs font-semibold text-center transition-all cursor-pointer ${
                  isActive
                    ? 'bg-[#4f46e5] text-white shadow-xs'
                    : 'bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-200 hover:bg-[#e6e8ea] dark:hover:bg-neutral-700'
                }`}
              >
                {r.label}
              </button>
            );
          })}
        </div>
      </div>

      {/* Simulation States */}
      <div className="flex flex-col gap-1">
        <label className="text-[11px] font-semibold text-[#565e74] dark:text-neutral-400">
          {t.sandbox_state_label}
        </label>
        <div className="grid grid-cols-3 gap-1">
          {simulationStates.map((s) => {
            const isActive = simulation === s.id;
            return (
              <button
                key={s.id}
                onClick={() => setSimulation(s.id)}
                type="button"
                className={`px-1.5 py-1 rounded-md text-xs text-center transition-all cursor-pointer ${
                  isActive
                    ? 'bg-[#dae2fd] dark:bg-indigo-900/60 text-[#0f0069] dark:text-indigo-200 font-bold ring-1 ring-[#4f46e5]'
                    : 'bg-[#f2f4f6] dark:bg-neutral-800 text-[#565e74] dark:text-neutral-300 font-medium hover:bg-[#e6e8ea] dark:hover:bg-neutral-700 hover:text-[#191c1e] dark:hover:text-white'
                }`}
              >
                {s.label}
              </button>
            );
          })}
        </div>
      </div>

      {/* Test Component Popover */}
      <div className="pt-1.5 border-t border-neutral-100 dark:border-neutral-800 flex items-center justify-between">
        <span className="text-[11px] text-[#565e74] dark:text-neutral-400 font-medium">
          {t.sandbox_test_picker}
        </span>
        <button
          onClick={onOpenPicker}
          type="button"
          className="px-2.5 py-1 rounded-lg bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-200 text-xs font-semibold hover:bg-[#e6e8ea] dark:hover:bg-neutral-700 flex items-center gap-1 transition-colors cursor-pointer"
        >
          <span className="material-symbols-outlined text-[14px] text-[#3525cd] dark:text-indigo-400">
            person_search
          </span>
          <span>{t.sandbox_btn_popover}</span>
        </button>
      </div>
    </div>
  );
};
