import React from 'react';
import { Info, X, ShieldAlert, CheckCircle2 } from 'lucide-react';
import { UserRole } from '../types';

interface BannerNoticeProps {
  currentRole: UserRole;
  selectedBuildingName?: string;
  onSelectBuilding?: (building: string) => void;
  toastMessage?: string | null;
  onCloseToast?: () => void;
}

export const BannerNotice: React.FC<BannerNoticeProps> = ({
  currentRole,
  selectedBuildingName = 'Tòa nhà An Gia 01',
  onSelectBuilding,
  toastMessage,
  onCloseToast,
}) => {
  const [showPreviewBanner, setShowPreviewBanner] = React.useState(true);

  const roleText =
    currentRole === 'landlord'
      ? 'Chủ trọ (Toàn quyền)'
      : currentRole === 'manager'
      ? 'Quản lý (Vận hành & Ghi số)'
      : 'Khách thuê (Xem thông tin)';

  return (
    <div className="space-y-3 mb-6">
      {/* Top row: Role simulation pill & Optional update toast */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        {/* Role tag */}
        <div className="inline-flex items-center gap-2 px-3 py-1.5 bg-blue-50/80 border border-blue-200/80 rounded-lg text-xs font-medium text-slate-700">
          <ShieldAlert className="w-4 h-4 text-indigo-600 shrink-0" />
          <span>
            Mô phỏng phân quyền kiểm thử:{' '}
            <strong className="text-indigo-700 font-semibold">Vai trò: {roleText}</strong>
          </span>
        </div>

        {/* Update Notification toast */}
        {toastMessage && (
          <div className="inline-flex items-center gap-2.5 px-3.5 py-1.5 bg-indigo-50 border border-indigo-100 rounded-lg text-xs text-indigo-900 shadow-xs animate-fadeIn">
            <CheckCircle2 className="w-4 h-4 text-indigo-600 shrink-0" />
            <span>{toastMessage}</span>
            {onCloseToast && (
              <button
                onClick={onCloseToast}
                className="text-slate-400 hover:text-slate-600 ml-1 p-0.5 rounded"
                aria-label="Đóng thông báo"
              >
                <X className="w-3.5 h-3.5" />
              </button>
            )}
          </div>
        )}
      </div>

      {/* Main Preview Info Banner */}
      {showPreviewBanner && (
        <div className="p-3.5 bg-blue-50/70 border border-blue-200/90 rounded-xl text-xs text-slate-700 flex items-start gap-3 relative">
          <div className="mt-0.5 p-1 bg-blue-100 text-blue-700 rounded-full shrink-0">
            <Info className="w-4 h-4" />
          </div>

          <div className="flex-1 space-y-1">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-semibold text-slate-900">
                Dữ liệu mẫu — chưa lưu SQL (bản thử nghiệm giao diện)
              </span>
              <span className="px-2 py-0.5 bg-blue-100/90 text-blue-800 font-medium rounded text-[11px]">
                Chế độ xem trước
              </span>
            </div>
            <p className="text-slate-600 text-xs leading-relaxed">
              Thao tác dùng dữ liệu giả lập, chỉ nằm trong phiên giao diện hiện tại; chưa có yêu cầu nào
              được gửi hoặc lưu vào cơ sở dữ liệu.
            </p>
            <div className="flex items-center gap-2 pt-1 text-xs">
              <span className="text-slate-500">Cơ sở áp dụng:</span>
              <button
                onClick={() => onSelectBuilding && onSelectBuilding('Tòa nhà An Gia 01')}
                className={`px-2 py-0.5 rounded border transition-colors ${
                  selectedBuildingName === 'Tòa nhà An Gia 01'
                    ? 'bg-white border-blue-300 text-indigo-700 font-medium shadow-2xs'
                    : 'bg-transparent border-transparent text-slate-600 hover:bg-white/60'
                }`}
              >
                Tòa nhà An Gia 01
              </button>
              <button
                onClick={() => onSelectBuilding && onSelectBuilding('Nhà trọ Bình Minh')}
                className={`px-2 py-0.5 rounded border transition-colors ${
                  selectedBuildingName === 'Nhà trọ Bình Minh'
                    ? 'bg-white border-blue-300 text-indigo-700 font-medium shadow-2xs'
                    : 'bg-transparent border-transparent text-slate-600 hover:bg-white/60'
                }`}
              >
                Nhà trọ Bình Minh
              </button>
            </div>
          </div>

          <button
            onClick={() => setShowPreviewBanner(false)}
            className="text-slate-400 hover:text-slate-600 p-1 rounded-lg"
            aria-label="Đóng biểu ngữ"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      )}
    </div>
  );
};
