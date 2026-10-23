import React, { useState } from 'react';
import type { Tenant, Language } from '../types/index.ts';
import { translations } from '../utils/i18n.ts';
import { generateTenantCode } from '../utils/tenantLogic.ts';

interface TenantAdmissionModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSave: (tenant: Tenant) => void;
  existingTenants: Tenant[];
  lang: Language;
}

export const TenantAdmissionModal: React.FC<TenantAdmissionModalProps> = ({
  isOpen,
  onClose,
  onSave,
  existingTenants,
  lang,
}) => {
  const t = translations[lang];

  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [cccd, setCccd] = useState('');
  const [room, setRoom] = useState('P.101 - Toà A');
  const [gender, setGender] = useState<'male' | 'female' | 'other'>('male');
  const [hometown, setHometown] = useState('');
  const [emergencyContact, setEmergencyContact] = useState('');
  const [rent, setRent] = useState('4500000');
  const [deposit, setDeposit] = useState('4500000');
  const [notes, setNotes] = useState('');
  const [hasApp, setHasApp] = useState(true);
  const [errors, setErrors] = useState<{ [key: string]: string }>({});

  if (!isOpen) return null;

  const validate = () => {
    const errs: { [key: string]: string } = {};
    if (!name.trim()) {
      errs.name = lang === 'vi' ? 'Vui lòng nhập họ và tên' : 'Please enter full name';
    }
    const cleanPhone = phone.replace(/\D/g, '');
    if (!cleanPhone || cleanPhone.length < 9 || cleanPhone.length > 11) {
      errs.phone = lang === 'vi' ? 'Số điện thoại không hợp lệ (9-11 số)' : 'Invalid phone number';
    }
    const cleanCCCD = cccd.replace(/\D/g, '');
    if (!cleanCCCD || cleanCCCD.length < 9) {
      errs.cccd = lang === 'vi' ? 'CCCD/CMND cần ít nhất 9-12 số' : 'National ID requires 9-12 digits';
    }
    setErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) return;

    const newCode = generateTenantCode(existingTenants);
    const newTenant: Tenant = {
      id: `t-${Date.now()}`,
      code: newCode,
      name: name.trim(),
      phone: phone.trim(),
      cccd: cccd.replace(/\s+/g, ''),
      status: 'active',
      hasApp: hasApp,
      room: room || 'P.101',
      gender: gender,
      hometown: hometown.trim() || undefined,
      emergencyContact: emergencyContact.trim() || undefined,
      startDate: new Date().toISOString().split('T')[0],
      monthlyRent: parseInt(rent, 10) || 4500000,
      depositAmount: parseInt(deposit, 10) || 4500000,
      notes: notes.trim() || undefined,
      avatarBg: 'bg-[#4f46e5]/10',
      avatarColor: 'text-[#4f46e5]',
    };

    onSave(newTenant);
    onClose();
    // Reset form
    setName('');
    setPhone('');
    setCccd('');
    setNotes('');
    setErrors({});
  };

  return (
    <div
      className="fixed inset-0 z-50 bg-[#191c1e]/50 backdrop-blur-[2px] flex items-center justify-center p-4 overflow-y-auto"
      id="modal-tenant"
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-tenant-title"
    >
      <div className="bg-white dark:bg-[#191c1e] w-full max-w-lg rounded-xl shadow-2xl flex flex-col overflow-hidden animate-in fade-in zoom-in-95 duration-150 border border-neutral-100 dark:border-neutral-800 my-8">
        {/* Modal Header */}
        <div className="px-6 py-4 bg-[#f2f4f6] dark:bg-neutral-800 flex items-center justify-between border-b border-neutral-200/60 dark:border-neutral-700">
          <div className="flex items-center gap-2.5">
            <span className="material-symbols-outlined text-[#3525cd] dark:text-indigo-400 text-[22px]">
              person_add
            </span>
            <span
              id="modal-tenant-title"
              className="text-base font-bold text-[#191c1e] dark:text-neutral-100"
            >
              {t.modal_admit_title}
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

        {/* Modal Body */}
        <form onSubmit={handleSubmit}>
          <div className="p-6 flex flex-col gap-4 max-h-[75vh] overflow-y-auto">
            {/* Họ và tên */}
            <div className="flex flex-col gap-1">
              <label className="text-xs font-semibold text-[#565e74] dark:text-neutral-300">
                {t.input_name} <span className="text-[#ba1a1a]">*</span>
              </label>
              <input
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Ví dụ: Lê Bảo Hoàng"
                className={`h-10 px-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-100 text-sm focus:outline-none focus:ring-1 ${
                  errors.name
                    ? 'ring-1 ring-[#ba1a1a]'
                    : 'focus:ring-[#4f46e5]'
                }`}
              />
              {errors.name && (
                <span className="text-[11px] text-[#ba1a1a] font-medium">{errors.name}</span>
              )}
            </div>

            {/* SĐT & CCCD */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="flex flex-col gap-1">
                <label className="text-xs font-semibold text-[#565e74] dark:text-neutral-300">
                  {t.input_phone} <span className="text-[#ba1a1a]">*</span>
                </label>
                <input
                  type="tel"
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  placeholder="09xxxxxxxx"
                  className={`h-10 px-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-100 text-sm focus:outline-none focus:ring-1 ${
                    errors.phone
                      ? 'ring-1 ring-[#ba1a1a]'
                      : 'focus:ring-[#4f46e5]'
                  }`}
                />
                {errors.phone && (
                  <span className="text-[11px] text-[#ba1a1a] font-medium">{errors.phone}</span>
                )}
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-xs font-semibold text-[#565e74] dark:text-neutral-300">
                  {t.input_cccd} <span className="text-[#ba1a1a]">*</span>
                </label>
                <input
                  type="text"
                  value={cccd}
                  onChange={(e) => setCccd(e.target.value)}
                  placeholder="12 chữ số"
                  className={`h-10 px-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-100 text-sm font-mono focus:outline-none focus:ring-1 ${
                    errors.cccd
                      ? 'ring-1 ring-[#ba1a1a]'
                      : 'focus:ring-[#4f46e5]'
                  }`}
                />
                {errors.cccd && (
                  <span className="text-[11px] text-[#ba1a1a] font-medium">{errors.cccd}</span>
                )}
              </div>
            </div>

            {/* Phòng & Giới tính */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="flex flex-col gap-1">
                <label className="text-xs font-semibold text-[#565e74] dark:text-neutral-300">
                  {t.input_room}
                </label>
                <select
                  value={room}
                  onChange={(e) => setRoom(e.target.value)}
                  className="h-10 px-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-100 text-sm focus:outline-none focus:ring-1 focus:ring-[#4f46e5]"
                >
                  <option value="P.101 - Toà A">P.101 - Toà A</option>
                  <option value="P.102 - Toà A">P.102 - Toà A</option>
                  <option value="P.201 - Toà A">P.201 - Toà A</option>
                  <option value="P.202 - Toà A">P.202 - Toà A</option>
                  <option value="P.301 - Toà A">P.301 - Toà A</option>
                  <option value="P.302 - Toà A">P.302 - Toà A</option>
                  <option value="P.103 - Toà B">P.103 - Toà B</option>
                  <option value="P.104 - Toà B">P.104 - Toà B</option>
                </select>
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-xs font-semibold text-[#565e74] dark:text-neutral-300">
                  {t.input_gender}
                </label>
                <div className="grid grid-cols-3 gap-1">
                  {(['male', 'female', 'other'] as const).map((g) => (
                    <button
                      key={g}
                      type="button"
                      onClick={() => setGender(g)}
                      className={`h-10 rounded-xl text-xs font-semibold transition-all cursor-pointer ${
                        gender === g
                          ? 'bg-[#4f46e5] text-white shadow-xs'
                          : 'bg-[#f2f4f6] dark:bg-neutral-800 text-[#565e74] dark:text-neutral-300 hover:bg-[#eceef0] dark:hover:bg-neutral-700'
                      }`}
                    >
                      {g === 'male'
                        ? t.input_gender_male
                        : g === 'female'
                        ? t.input_gender_female
                        : t.input_gender_other}
                    </button>
                  ))}
                </div>
              </div>
            </div>

            {/* Quê quán & SĐT Khẩn cấp */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="flex flex-col gap-1">
                <label className="text-xs font-semibold text-[#565e74] dark:text-neutral-300">
                  {t.input_hometown}
                </label>
                <input
                  type="text"
                  value={hometown}
                  onChange={(e) => setHometown(e.target.value)}
                  placeholder="Ví dụ: TP. Hồ Chí Minh"
                  className="h-10 px-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-100 text-sm focus:outline-none focus:ring-1 focus:ring-[#4f46e5]"
                />
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-xs font-semibold text-[#565e74] dark:text-neutral-300">
                  {t.input_emergency}
                </label>
                <input
                  type="text"
                  value={emergencyContact}
                  onChange={(e) => setEmergencyContact(e.target.value)}
                  placeholder="09xx xxx xxx (Người thân)"
                  className="h-10 px-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-100 text-sm focus:outline-none focus:ring-1 focus:ring-[#4f46e5]"
                />
              </div>
            </div>

            {/* Tiền thuê & Cọc */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="flex flex-col gap-1">
                <label className="text-xs font-semibold text-[#565e74] dark:text-neutral-300">
                  {t.input_rent}
                </label>
                <input
                  type="number"
                  value={rent}
                  onChange={(e) => setRent(e.target.value)}
                  className="h-10 px-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-100 text-sm font-mono focus:outline-none focus:ring-1 focus:ring-[#4f46e5]"
                />
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-xs font-semibold text-[#565e74] dark:text-neutral-300">
                  {t.input_deposit}
                </label>
                <input
                  type="number"
                  value={deposit}
                  onChange={(e) => setDeposit(e.target.value)}
                  className="h-10 px-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 text-[#191c1e] dark:text-neutral-100 text-sm font-mono focus:outline-none focus:ring-1 focus:ring-[#4f46e5]"
                />
              </div>
            </div>

            {/* Kích hoạt tài khoản app */}
            <div className="flex items-center gap-3 p-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800">
              <input
                id="hasAppCheckbox"
                type="checkbox"
                checked={hasApp}
                onChange={(e) => setHasApp(e.target.checked)}
                className="w-4 h-4 text-[#4f46e5] rounded focus:ring-[#4f46e5] cursor-pointer"
              />
              <label
                htmlFor="hasAppCheckbox"
                className="text-xs text-[#191c1e] dark:text-neutral-200 cursor-pointer font-medium"
              >
                {lang === 'vi'
                  ? 'Gửi SMS liên kết tài khoản ứng dụng cư dân cho khách thuê'
                  : 'Send SMS link for resident app account activation'}
              </label>
            </div>

            {/* D9 Notice Banner */}
            <div className="p-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800/60 border border-neutral-200/50 dark:border-neutral-700/60 flex items-start gap-2.5">
              <span className="material-symbols-outlined text-[#3525cd] dark:text-indigo-400 text-[19px] shrink-0 mt-0.5">
                security
              </span>
              <p className="text-xs text-[#565e74] dark:text-neutral-400 leading-relaxed">
                {t.d9_notice}
              </p>
            </div>
          </div>

          {/* Modal Footer */}
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
