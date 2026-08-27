import React from 'react';
import { AlertCircle } from 'lucide-react';
import { triggerHaptic } from '../utils/vkBridge';

interface ConfirmModalProps {
  isOpen: boolean;
  title: string;
  description: string;
  confirmText?: string;
  cancelText?: string;
  isDestructive?: boolean;
  isLoading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

export const ConfirmModal: React.FC<ConfirmModalProps> = ({
  isOpen,
  title,
  description,
  confirmText = 'Подтвердить',
  cancelText = 'Отмена',
  isDestructive = false,
  isLoading = false,
  onConfirm,
  onCancel,
}) => {
  if (!isOpen) return null;

  const handleConfirm = () => {
    triggerHaptic('heavy');
    onConfirm();
  };

  const handleCancel = () => {
    triggerHaptic('light');
    onCancel();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
      <div className="w-full max-w-sm bg-[#0d281e] border border-[#1b4d3e] rounded-2xl p-5 shadow-2xl">
        <div className="flex flex-col items-center text-center">
          <div className={`w-12 h-12 rounded-full flex items-center justify-center mb-3 ${
            isDestructive ? 'bg-red-950/60 text-red-400 border border-red-500/30' : 'bg-amber-950/60 text-[#f3c64c] border border-amber-500/30'
          }`}>
            <AlertCircle className="w-6 h-6" />
          </div>

          <h3 className="text-base font-bold text-slate-100 mb-1">{title}</h3>
          <p className="text-xs text-slate-300 mb-5 leading-relaxed">{description}</p>

          <div className="flex items-center gap-2.5 w-full">
            <button
              onClick={handleCancel}
              disabled={isLoading}
              className="flex-1 py-2.5 px-3 rounded-xl bg-[#081c15] border border-[#1b4d3e] text-slate-300 text-xs font-semibold hover:border-slate-600 active:scale-95 transition-all"
            >
              {cancelText}
            </button>
            <button
              onClick={handleConfirm}
              disabled={isLoading}
              className={`flex-1 py-2.5 px-3 rounded-xl text-xs font-bold active:scale-95 transition-all flex items-center justify-center ${
                isDestructive
                  ? 'bg-red-600 hover:bg-red-700 text-white shadow-md shadow-red-900/30'
                  : 'gold-gradient-bg text-black shadow-md shadow-amber-900/40'
              }`}
            >
              {isLoading ? 'Обработка...' : confirmText}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
