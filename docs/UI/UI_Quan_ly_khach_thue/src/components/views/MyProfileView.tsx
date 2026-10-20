import React from 'react';
import type { Language } from '../../types/index.ts';

interface MyProfileViewProps {
  lang: Language;
}

export const MyProfileView: React.FC<MyProfileViewProps> = ({ lang }) => {
  return (
    <div className="flex flex-col gap-6 max-w-4xl">
      <div>
        <h1 className="text-2xl font-bold text-[#191c1e] dark:text-neutral-100 tracking-tight">
          {lang === 'vi' ? 'Hồ sơ chủ trọ' : 'Landlord Profile'}
        </h1>
        <p className="text-sm text-[#565e74] dark:text-neutral-400 mt-1">
          {lang === 'vi'
            ? 'Thông tin định danh người quản lý và các dãy trọ đang khai thác'
            : 'Manager identification information and managed boarding facilities'}
        </p>
      </div>

      {/* Main Profile Info Card */}
      <div className="p-6 rounded-xl bg-white dark:bg-[#191c1e] shadow-sm border border-neutral-100 dark:border-neutral-800 flex flex-col md:flex-row gap-6 items-start">
        <div className="w-20 h-20 rounded-2xl bg-[#3525cd] text-white flex items-center justify-center font-bold text-3xl shadow-md shrink-0">
          A
        </div>
        <div className="flex flex-col flex-1 gap-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <h2 className="text-lg font-bold text-[#191c1e] dark:text-neutral-100">
                Nguyễn Văn An
              </h2>
              <div className="flex items-center gap-2 mt-0.5">
                <span className="font-mono text-xs text-[#565e74] dark:text-neutral-400">
                  #LANDLORD-01
                </span>
                <span className="px-2 py-0.5 rounded-full bg-[#ECFDF5] dark:bg-emerald-950/70 text-[#065F46] dark:text-emerald-300 text-xs font-semibold">
                  {lang === 'vi' ? 'Đã xác thực CCCD' : 'ID Verified'}
                </span>
                <span className="px-2 py-0.5 rounded-full bg-[#dae2fd] dark:bg-indigo-950/70 text-[#3525cd] dark:text-indigo-300 text-xs font-semibold">
                  June Pro
                </span>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 pt-4 border-t border-neutral-100 dark:border-neutral-800 text-xs">
            <div>
              <span className="text-[#565e74] dark:text-neutral-400">{lang === 'vi' ? 'Email liên hệ' : 'Email'}</span>
              <p className="text-sm font-semibold text-[#191c1e] dark:text-neutral-100 font-mono mt-0.5">
                nguyen.an@demo.vn
              </p>
            </div>
            <div>
              <span className="text-[#565e74] dark:text-neutral-400">{lang === 'vi' ? 'Số điện thoại' : 'Phone'}</span>
              <p className="text-sm font-semibold text-[#191c1e] dark:text-neutral-100 font-mono mt-0.5">
                0912 345 678
              </p>
            </div>
            <div>
              <span className="text-[#565e74] dark:text-neutral-400">{lang === 'vi' ? 'Chuẩn an toàn dữ liệu' : 'Security Standard'}</span>
              <p className="text-sm font-semibold text-[#3525cd] dark:text-indigo-400 mt-0.5 flex items-center gap-1">
                <span className="material-symbols-outlined text-[16px]">verified</span>
                D9 Privacy Compliant
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* Managed Properties */}
      <div className="flex flex-col gap-3">
        <h3 className="text-sm font-bold text-[#191c1e] dark:text-neutral-100 uppercase tracking-wider">
          {lang === 'vi' ? 'Dãy phòng đang quản lý' : 'Managed Properties'}
        </h3>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {[
            { name: 'Toà A - Hoa Cúc', rooms: '8/8 phòng kín', occ: '100%' },
            { name: 'Toà B - Lan Rừng', rooms: '9/10 phòng kín', occ: '90%' },
            { name: 'Toà C - Hướng Dương', rooms: '7/8 phòng kín', occ: '87.5%' },
            { name: 'Toà D - Mai Vàng', rooms: '4/6 phòng kín', occ: '66.7%' },
          ].map((prop, idx) => (
            <div
              key={idx}
              className="p-4 rounded-xl bg-white dark:bg-[#191c1e] shadow-sm border border-neutral-100 dark:border-neutral-800 flex flex-col justify-between"
            >
              <div className="flex items-center justify-between">
                <span className="font-semibold text-xs text-[#191c1e] dark:text-neutral-100">
                  {prop.name}
                </span>
                <span className="text-xs font-mono font-bold text-[#4f46e5] dark:text-indigo-400">
                  {prop.occ}
                </span>
              </div>
              <p className="text-xs text-[#565e74] dark:text-neutral-400 mt-2 font-mono">
                {prop.rooms}
              </p>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};
