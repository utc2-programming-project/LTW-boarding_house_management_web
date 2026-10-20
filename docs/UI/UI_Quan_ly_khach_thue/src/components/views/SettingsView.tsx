import React, { useState } from 'react';
import type { Language } from '../../types/index.ts';

interface SettingsViewProps {
  lang: Language;
}

export const SettingsView: React.FC<SettingsViewProps> = ({ lang }) => {
  const [d9Strict, setD9Strict] = useState(true);
  const [smsNotification, setSmsNotification] = useState(true);
  const [autoMaskExport, setAutoMaskExport] = useState(true);
  const [savedNotice, setSavedNotice] = useState(false);

  const handleSave = () => {
    setSavedNotice(true);
    setTimeout(() => setSavedNotice(false), 3000);
  };

  return (
    <div className="flex flex-col gap-6 max-w-4xl">
      <div>
        <h1 className="text-2xl font-bold text-[#191c1e] dark:text-neutral-100 tracking-tight">
          {lang === 'vi' ? 'Cài đặt hệ thống' : 'System Settings'}
        </h1>
        <p className="text-sm text-[#565e74] dark:text-neutral-400 mt-1">
          {lang === 'vi'
            ? 'Cấu hình chuẩn bảo mật D9, thông báo tự động và quản lý sao lưu dữ liệu'
            : 'Configure D9 security standards, automated notifications, and database backups'}
        </p>
      </div>

      {savedNotice && (
        <div className="p-3 rounded-xl bg-[#ECFDF5] dark:bg-emerald-950/70 border border-[#059669]/20 text-[#065F46] dark:text-emerald-300 text-xs font-semibold flex items-center gap-2">
          <span className="material-symbols-outlined text-[18px]">check_circle</span>
          <span>{lang === 'vi' ? 'Đã lưu cài đặt thành công!' : 'Settings saved successfully!'}</span>
        </div>
      )}

      {/* D9 Security Mandate Card */}
      <div className="p-6 rounded-xl bg-white dark:bg-[#191c1e] shadow-sm border border-neutral-100 dark:border-neutral-800 flex flex-col gap-4">
        <div className="flex items-center gap-2">
          <span className="material-symbols-outlined text-[#3525cd] dark:text-indigo-400 text-[22px]">
            security
          </span>
          <h2 className="text-base font-bold text-[#191c1e] dark:text-neutral-100">
            {lang === 'vi' ? 'Bảo mật thông tin định danh D9' : 'D9 Identity Privacy Standard'}
          </h2>
        </div>
        <p className="text-xs text-[#565e74] dark:text-neutral-400 leading-relaxed">
          {lang === 'vi'
            ? 'Quy chuẩn bảo vệ dữ liệu cá nhân theo Nghị định bảo vệ dữ liệu cư trú. Số CCCD/CMND được tự động che 8 số đầu (••••••••xxxx).'
            : 'Personal data protection according to identity privacy regulations. National ID digits are automatically masked (••••••••xxxx).'}
        </p>

        <div className="flex flex-col gap-3 pt-2">
          <label className="flex items-center justify-between p-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 cursor-pointer">
            <div className="flex flex-col">
              <span className="text-xs font-semibold text-[#191c1e] dark:text-neutral-100">
                {lang === 'vi' ? 'Bắt buộc chuẩn che D9 trên mọi danh sách' : 'Enforce D9 Masking Everywhere'}
              </span>
              <span className="text-[11px] text-[#565e74] dark:text-neutral-400">
                {lang === 'vi' ? 'Chỉ cho phép mở khi có quyền Quản lý/Chủ trọ' : 'Requires Landlord/Manager permissions to reveal'}
              </span>
            </div>
            <input
              type="checkbox"
              checked={d9Strict}
              onChange={(e) => setD9Strict(e.target.checked)}
              className="w-4 h-4 text-[#4f46e5] rounded focus:ring-[#4f46e5] cursor-pointer"
            />
          </label>

          <label className="flex items-center justify-between p-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 cursor-pointer">
            <div className="flex flex-col">
              <span className="text-xs font-semibold text-[#191c1e] dark:text-neutral-100">
                {lang === 'vi' ? 'Mã hóa tự động khi xuất file Excel / CSV' : 'Mask ID digits in Excel / CSV export'}
              </span>
              <span className="text-[11px] text-[#565e74] dark:text-neutral-400">
                {lang === 'vi' ? 'Bảo vệ thông tin khách thuê khi chuyển giao file cho bên thứ ba' : 'Protect tenant identity when exporting'}
              </span>
            </div>
            <input
              type="checkbox"
              checked={autoMaskExport}
              onChange={(e) => setAutoMaskExport(e.target.checked)}
              className="w-4 h-4 text-[#4f46e5] rounded focus:ring-[#4f46e5] cursor-pointer"
            />
          </label>

          <label className="flex items-center justify-between p-3 rounded-xl bg-[#f2f4f6] dark:bg-neutral-800 cursor-pointer">
            <div className="flex flex-col">
              <span className="text-xs font-semibold text-[#191c1e] dark:text-neutral-100">
                {lang === 'vi' ? 'Gửi tin nhắn SMS kích hoạt tự động khi tiếp nhận khách' : 'Auto SMS on tenant admission'}
              </span>
              <span className="text-[11px] text-[#565e74] dark:text-neutral-400">
                {lang === 'vi' ? 'Gửi kèm link tải ứng dụng RoomJune Resident' : 'Includes download link for RoomJune Resident App'}
              </span>
            </div>
            <input
              type="checkbox"
              checked={smsNotification}
              onChange={(e) => setSmsNotification(e.target.checked)}
              className="w-4 h-4 text-[#4f46e5] rounded focus:ring-[#4f46e5] cursor-pointer"
            />
          </label>
        </div>

        <div className="pt-2 flex justify-end">
          <button
            onClick={handleSave}
            type="button"
            className="px-4 py-2 rounded-xl bg-[#4f46e5] text-white hover:bg-[#3525cd] text-xs font-semibold shadow-xs transition-all active:scale-[0.98] cursor-pointer"
          >
            {lang === 'vi' ? 'Lưu thay đổi' : 'Save Changes'}
          </button>
        </div>
      </div>
    </div>
  );
};
