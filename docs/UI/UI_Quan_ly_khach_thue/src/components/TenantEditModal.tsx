import React, { useState, useEffect } from 'react';
import type { Tenant, Language } from '../types/index.ts';
import { translations } from '../utils/i18n.ts';

interface TenantEditModalProps {
  tenant: Tenant | null;
  isOpen: boolean;
  onClose: () => void;
  onSave: (updated: Tenant) => void;
  lang: Language;
}

export const TenantEditModal: React.FC<TenantEditModalProps> = ({
  tenant,
  isOpen,
  onClose,
  onSave,
  lang,
}) => {
  const t = translations[lang];

  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [room, setRoom] = useState('');
  const [notes, setNotes] = useState('');
  const [hasApp, setHasApp] = useState(true);
  const [status, setStatus] = useState<'active' | 'inactive'>('active');

  useEffect(() => {
    if (tenant) {
      setName(tenant.name);
      setPhone(tenant.phone);
      setRoom(tenant.room);
      setNotes(tenant.notes || '');
      setHasApp(tenant.hasApp);
      setStatus(tenant.status);
    }
  }, [tenant]);

  if (!isOpen || !tenant) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;

    onSave({
      ...tenant,
      name: name.trim(),
      phone: phone.trim(),
      room: room.trim(),
      notes: notes.trim() || undefined,
      hasApp,
      status,
    });
    onClose();
  };

  return (
    <div
      className="fixed inset-0 z-50 bg-[#191c1e]/50 backdrop-blur-[2px] flex items-center justify-center p-4 overflow-y-auto"
      role="dialog"
      aria-modal="true"
    >
      <div className="bg-white dark:bg-[#191c1e] w-full max-w-lg rounded-xl shadow-2xl flex flex-col overflow-hidden animate-in fade-in zoom-in-95 duration-150 border border-neutral-100 dark:border-neutral-800 my-8">
        <div className="px-6 py-4 bg-[#f2f4f6] dark:bg-neutral-800 flex items-center justify-between border-b border-neutral-200/60 dark:border-neutral-700">
          <div className="flex items-center gap-2.5">
            <span className="material-symbols-outlined text-[#3525cd] dark:text-indigo-400 text-[22px]">
              edit
            </span>
            <span className="text-base font-bold text-[#191c1e] dark:text-neutral-100">
              {t.modal_edit_title}
            </span>
          </div>
          <button
            onClick={onClose}
            aria-label={t.btn_close}
            className="text-[#565e74] hover:text-[#191c1e] dark:hover:text-white p-1 rounded-lg transition-colors cursor-pointer"
            type="button"
          >
            <span className="material-symbols-outlined text-[20px]">close</span>
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="p-6 flex flex-col gap-4">
            <div className="flex flex-col gap-1">
              <label className="text-xs font-semibold text-[#565e74] dark:text-neutral-300">
                {t.input_name} <span className="text-[#ba1a1a]">*</span>
              </label>
              <input
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                className="h-10 px-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-100 text-sm focus:outline-none focus:ring-1 focus:ring-[#4f46e5]"
                required
              />
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="flex flex-col gap-1">
                <label className="text-xs font-semibold text-[#565e74] dark:text-neutral-300">
                  {t.input_phone}
                </label>
                <input
                  type="text"
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  className="h-10 px-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-100 text-sm focus:outline-none focus:ring-1 focus:ring-[#4f46e5]"
                />
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-xs font-semibold text-[#565e74] dark:text-neutral-300">
                  {t.input_room}
                </label>
                <input
                  type="text"
                  value={room}
                  onChange={(e) => setRoom(e.target.value)}
                  className="h-10 px-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-100 text-sm focus:outline-none focus:ring-1 focus:ring-[#4f46e5]"
                />
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="flex flex-col gap-1">
                <label className="text-xs font-semibold text-[#565e74] dark:text-neutral-300">
                  {t.col_status}
                </label>
                <select
                  value={status}
                  onChange={(e) => setStatus(e.target.value as 'active' | 'inactive')}
                  className="h-10 px-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-100 text-sm focus:outline-none focus:ring-1 focus:ring-[#4f46e5]"
                >
                  <option value="active">{t.status_active}</option>
                  <option value="inactive">{t.status_inactive}</option>
                </select>
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-xs font-semibold text-[#565e74] dark:text-neutral-300">
                  {t.col_account}
                </label>
                <select
                  value={hasApp ? 'yes' : 'no'}
                  onChange={(e) => setHasApp(e.target.value === 'yes')}
                  className="h-10 px-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-100 text-sm focus:outline-none focus:ring-1 focus:ring-[#4f46e5]"
                >
                  <option value="yes">{t.acc_yes}</option>
                  <option value="no">{t.acc_none}</option>
                </select>
              </div>
            </div>

            <div className="flex flex-col gap-1">
              <label className="text-xs font-semibold text-[#565e74] dark:text-neutral-300">
                {t.input_notes}
              </label>
              <textarea
                rows={3}
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                className="p-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-100 text-sm focus:outline-none focus:ring-1 focus:ring-[#4f46e5]"
              />
            </div>
          </div>

          <div className="px-6 py-3.5 bg-[#f2f4f6] dark:bg-neutral-800 border-t border-neutral-200/60 dark:border-neutral-700 flex items-center justify-end gap-2">
            <button
              onClick={onClose}
              type="button"
              className="px-4 py-2 rounded-xl text-xs font-semibold text-[#565e74] dark:text-neutral-300 hover:text-[#191c1e] dark:hover:text-white hover:bg-[#eceef0] dark:hover:bg-neutral-700 transition-colors cursor-pointer"
            >
              {t.btn_cancel}
            </button>
            <button
              type="submit"
              className="px-4 py-2 rounded-xl bg-[#4f46e5] text-white hover:bg-[#3525cd] text-xs font-semibold shadow-sm transition-all active:scale-[0.98] cursor-pointer"
            >
              {t.btn_save}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
