import React from 'react';
import type { PropertyItem } from '../types/index.ts';

interface PropertiesViewProps {
  properties: PropertyItem[];
  onSelectPropertyRooms: (propertyName: string) => void;
}

export const PropertiesView: React.FC<PropertiesViewProps> = ({
  properties,
  onSelectPropertyRooms,
}) => {
  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h2 className="text-[28px] font-bold text-on-surface tracking-tight">Danh sách Nhà trọ & Cơ sở</h2>
        <p className="text-[14px] text-on-surface-variant">
          Quản lý thông tin các tòa nhà, tỷ lệ lấp đầy phòng và thông tin liên hệ quản lý cơ sở.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {properties.map((prop) => {
          const occupancyRate = Math.round((prop.occupiedRooms / prop.totalRooms) * 100);

          return (
            <div
              key={prop.id}
              className="bg-surface-container-lowest rounded-2xl p-6 shadow-sm border border-surface-container flex flex-col justify-between hover:shadow-md transition-shadow gap-5"
            >
              <div className="flex flex-col gap-3">
                <div className="flex items-start justify-between gap-3">
                  <div className="flex items-center gap-3">
                    <div className="w-11 h-11 rounded-xl bg-primary-container/10 text-primary-container flex items-center justify-center">
                      <span className="material-symbols-outlined text-[24px]">apartment</span>
                    </div>
                    <div>
                      <h3 className="text-[17px] font-bold text-on-surface">{prop.name}</h3>
                      <span className="text-[12px] text-on-surface-variant">{prop.totalRooms} phòng</span>
                    </div>
                  </div>
                  <span className="px-2.5 py-1 rounded-full bg-secondary-fixed text-on-secondary-fixed-variant text-[11px] font-semibold">
                    {occupancyRate}% lấp đầy
                  </span>
                </div>

                <div className="flex items-center gap-2 text-[13px] text-on-surface-variant">
                  <span className="material-symbols-outlined text-[16px] text-outline shrink-0">location_on</span>
                  <span className="line-clamp-2">{prop.address}</span>
                </div>

                <p className="text-[13px] text-on-surface-variant leading-relaxed line-clamp-2">
                  {prop.description}
                </p>

                {/* Occupancy progress bar */}
                <div className="flex flex-col gap-1.5 pt-2">
                  <div className="flex justify-between text-[12px]">
                    <span className="text-on-surface-variant font-medium">Đang thuê:</span>
                    <span className="text-on-surface font-semibold">
                      {prop.occupiedRooms} / {prop.totalRooms} phòng
                    </span>
                  </div>
                  <div className="w-full h-2 rounded-full bg-surface-container overflow-hidden">
                    <div
                      className="h-full bg-secondary rounded-full transition-all duration-500"
                      style={{ width: `${occupancyRate}%` }}
                    />
                  </div>
                </div>

                {/* Manager info */}
                <div className="pt-3 border-t border-surface-container flex items-center justify-between text-[12px]">
                  <span className="text-on-surface-variant">Quản lý:</span>
                  <div className="text-right">
                    <div className="font-semibold text-on-surface">{prop.managerName}</div>
                    <div className="text-primary font-medium">{prop.managerPhone}</div>
                  </div>
                </div>
              </div>

              <button
                type="button"
                onClick={() => onSelectPropertyRooms(prop.name)}
                className="w-full py-2.5 rounded-xl bg-surface-container text-on-surface hover:bg-primary hover:text-on-primary transition-all text-[13px] font-semibold flex items-center justify-center gap-2"
              >
                <span>Xem danh sách phòng</span>
                <span className="material-symbols-outlined text-[16px]">arrow_forward</span>
              </button>
            </div>
          );
        })}
      </div>
    </div>
  );
};
