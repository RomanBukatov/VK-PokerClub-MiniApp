import React from 'react';
import clsx from 'clsx';
import { TournamentStatus } from '../types';

interface BadgeProps {
  type?: 'green' | 'amber' | 'red' | 'gold' | 'emerald' | 'gray';
  children: React.ReactNode;
  className?: string;
  size?: 'sm' | 'md';
}

export const Badge: React.FC<BadgeProps> = ({ 
  type = 'emerald', 
  children, 
  className,
  size = 'md'
}) => {
  const baseClasses = 'inline-flex items-center font-medium rounded-full border transition-all';
  
  const sizeClasses = size === 'sm' 
    ? 'px-2 py-0.5 text-xs' 
    : 'px-2.5 py-1 text-xs font-semibold';

  const typeClasses = {
    green: 'bg-emerald-950/80 text-emerald-400 border-emerald-500/40 shadow-sm shadow-emerald-900/30',
    amber: 'bg-amber-950/80 text-amber-300 border-amber-500/50 shadow-sm shadow-amber-900/30',
    red: 'bg-red-950/80 text-red-400 border-red-500/40 shadow-sm shadow-red-900/30',
    gold: 'bg-amber-900/40 text-[#f3c64c] border-[#d4af37]/60 shadow-sm shadow-amber-950/50',
    emerald: 'bg-[#103527] text-emerald-300 border-[#2d6a4f]',
    gray: 'bg-slate-900/60 text-slate-400 border-slate-700',
  };

  return (
    <span className={clsx(baseClasses, sizeClasses, typeClasses[type], className)}>
      {children}
    </span>
  );
};

export const TournamentStatusBadge: React.FC<{ status: TournamentStatus }> = ({ status }) => {
  switch (status) {
    case TournamentStatus.RegistrationOpen:
      return <Badge type="green">Регистрация открыта</Badge>;
    case TournamentStatus.Announced:
      return <Badge type="gold">Анонс</Badge>;
    case TournamentStatus.Running:
      return <Badge type="amber">Идет турнир</Badge>;
    case TournamentStatus.Finished:
      return <Badge type="gray">Завершен</Badge>;
    case TournamentStatus.Canceled:
      return <Badge type="red">Отменен</Badge>;
    default:
      return null;
  }
};
