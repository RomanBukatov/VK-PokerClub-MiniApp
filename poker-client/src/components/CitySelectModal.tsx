import React, { useEffect, useState } from 'react';
import { X, Check, Building2, MapPin } from 'lucide-react';
import { useUserStore } from '../store/useUserStore';
import { citiesApi } from '../api/citiesApi';
import type { City } from '../types';
import { triggerHaptic } from '../utils/vkBridge';

export const CitySelectModal: React.FC = () => {
  const { isCityModalOpen, setIsCityModalOpen, selectedCityId, setSelectedCity } = useUserStore();
  const [cities, setCities] = useState<City[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let isMounted = true;
    if (isCityModalOpen) {
      const fetchCities = async () => {
        try {
          setLoading(true);
          const data = await citiesApi.getCities();
          if (isMounted) {
            setCities(data);
          }
        } catch (err) {
          console.error('Ошибка загрузки городов:', err);
        } finally {
          if (isMounted) {
            setLoading(false);
          }
        }
      };
      fetchCities();
    }
    return () => {
      isMounted = false;
    };
  }, [isCityModalOpen]);

  if (!isCityModalOpen) return null;

  const handleSelect = (id: number | null, name: string) => {
    triggerHaptic('light');
    setSelectedCity(id, name);
    setIsCityModalOpen(false);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-0 sm:p-4 bg-black/75 backdrop-blur-sm animate-fade-in">
      <div className="w-full max-w-md bg-[#0d281e] border-t sm:border border-[#1b4d3e] rounded-t-2xl sm:rounded-2xl p-5 shadow-2xl safe-bottom max-h-[85vh] flex flex-col">
        {/* Шапка модалки */}
        <div className="flex items-center justify-between pb-4 border-b border-[#1b4d3e]">
          <div className="flex items-center gap-2">
            <MapPin className="w-5 h-5 text-[#e5a93c]" />
            <h3 className="text-base font-bold text-slate-100">Выберите ваш город</h3>
          </div>
          <button
            onClick={() => setIsCityModalOpen(false)}
            className="p-1.5 rounded-lg bg-[#081c15] text-slate-400 hover:text-white border border-[#1b4d3e]"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Список городов */}
        <div className="py-3 overflow-y-auto space-y-2 flex-1">
          {/* Пункт Все города */}
          <button
            onClick={() => handleSelect(null, 'Все города')}
            className={`w-full flex items-center justify-between p-3.5 rounded-xl border transition-all active:scale-[0.98] ${
              selectedCityId === null
                ? 'bg-amber-950/40 border-[#d4af37] text-white'
                : 'bg-[#081c15] border-[#1b4d3e] text-slate-300 hover:border-slate-600'
            }`}
          >
            <div className="flex items-center gap-3">
              <Building2 className={`w-5 h-5 ${selectedCityId === null ? 'text-[#f3c64c]' : 'text-slate-500'}`} />
              <div className="text-left font-medium">Все города</div>
            </div>
            {selectedCityId === null && <Check className="w-5 h-5 text-[#f3c64c]" />}
          </button>

          {loading ? (
            <div className="py-6 text-center text-sm text-slate-400 animate-pulse">
              Загрузка списка городов...
            </div>
          ) : (
            cities.map((city) => {
              const isSelected = selectedCityId === city.id;
              return (
                <button
                  key={city.id}
                  onClick={() => handleSelect(city.id, city.name)}
                  className={`w-full flex items-center justify-between p-3.5 rounded-xl border transition-all active:scale-[0.98] ${
                    isSelected
                      ? 'bg-amber-950/40 border-[#d4af37] text-white'
                      : 'bg-[#081c15] border-[#1b4d3e] text-slate-300 hover:border-slate-600'
                  }`}
                >
                  <div className="flex items-center gap-3">
                    <MapPin className={`w-5 h-5 ${isSelected ? 'text-[#f3c64c]' : 'text-slate-500'}`} />
                    <div className="text-left">
                      <div className="font-semibold text-sm">{city.name}</div>
                      <div className="text-xs text-slate-400">
                        {city.activeClubsCount} {city.activeClubsCount === 1 ? 'клуб' : 'клубов'}
                      </div>
                    </div>
                  </div>
                  {isSelected && <Check className="w-5 h-5 text-[#f3c64c]" />}
                </button>
              );
            })
          )}
        </div>
      </div>
    </div>
  );
};
