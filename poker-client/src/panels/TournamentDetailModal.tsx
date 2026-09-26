import React, { useState } from 'react';
import { ChevronLeft, CheckCircle2, AlertCircle, MapPin, Clock, Info, Users, Trophy, CreditCard } from 'lucide-react';
import { useTournamentsStore } from '../store/useTournamentsStore';
import { useUserStore } from '../store/useUserStore';
import { formatCurrency } from '../utils/formatters';
import { triggerHaptic, requestGroupMessagesPermission, openExternalUrl } from '../utils/vkBridge';
import { TournamentStatus } from '../types';
import { CURRENT_BRANDING, getEffectiveVkGroupId } from '../config/branding';
import { PlayerAvatar } from '../components/PlayerAvatar';

// eslint-disable-next-line react-refresh/only-export-components
export const checkCanViewVkId = (
  vkUser?: { isAdmin?: boolean } | null,
  hasAdminRole?: boolean,
  profile?: { isAdmin?: boolean } | null
): boolean => {
  return hasAdminRole === true || vkUser?.isAdmin === true || profile?.isAdmin === true;
};

// eslint-disable-next-line react-refresh/only-export-components
export const checkProfileCompleteness = (
  vkUser?: { id?: number; first_name?: string; last_name?: string; isAdmin?: boolean } | null,
  profile?: { fullName?: string; firstName?: string; lastName?: string; nickname?: string; phoneNumber?: string; clubCardId?: string } | null
) => {
  const rawFullName = (
    profile?.fullName ||
    `${profile?.lastName || ''} ${profile?.firstName || ''}`.trim() ||
    `${vkUser?.last_name || ''} ${vkUser?.first_name || ''}`.trim()
  ).trim();

  const lowerFullName = rawFullName.toLowerCase();
  const lowerNick = (profile?.nickname || '').trim().toLowerCase();
  const vkFirst = (vkUser?.first_name || '').trim().toLowerCase();
  const vkLast = (vkUser?.last_name || '').trim().toLowerCase();

  const isGuestUser =
    !vkUser ||
    vkUser.id === 0 ||
    (vkFirst === 'гость' && (vkLast === 'клуба' || !vkLast)) ||
    lowerFullName === 'гость клуба' ||
    lowerFullName === 'клуба гость' ||
    lowerFullName === 'гость' ||
    lowerNick === 'гость клуба' ||
    lowerNick === 'гость';

  const hasRealName =
    rawFullName.length >= 2 &&
    lowerFullName !== 'гость клуба' &&
    lowerFullName !== 'клуба гость' &&
    lowerFullName !== 'гость' &&
    lowerFullName !== 'игрок vk' &&
    lowerFullName !== 'игрок' &&
    !lowerFullName.startsWith('игрок #') &&
    !/^игрок\s*\d+$/i.test(lowerFullName);

  const rawPhone = (profile?.phoneNumber || '').replace(/\D/g, '');
  const hasPhone = rawPhone.length >= 10;

  const hasClubCard = Boolean(profile?.clubCardId && profile.clubCardId.trim().length > 0);
  const isProfileComplete = !isGuestUser && hasRealName && hasPhone;
  const shouldShowClubCardBanner = !hasClubCard;

  return {
    isProfileComplete,
    isGuestUser,
    hasRealName,
    hasPhone,
    hasClubCard,
    shouldShowClubCardBanner,
  };
};

export const TournamentDetailModal: React.FC = () => {
  const { 
    selectedTournament, 
    isDetailModalOpen, 
    closeDetail, 
    registerToTournament, 
    unregisterFromTournament,
    isActionLoading,
    actionError,
  } = useTournamentsStore();
  const { vkUser, profile, setIsProfileModalOpen, hasAdminRole: storeHasAdminRole } = useUserStore();

  const [isCancelConfirmOpen, setIsCancelConfirmOpen] = useState(false);
  const [isRegistering, setIsRegistering] = useState(false);
  const [currentTime] = useState(() => Date.now());

  if (!isDetailModalOpen || !selectedTournament) return null;

  const t = selectedTournament;
  const isRegistered = t.isUserRegistered || 
    (vkUser && t.participants?.some(p => p.vkId === vkUser.id.toString()));

  const maxSeats = t.maxSeats || 30;
  const participantsCount = t.participants ? t.participants.length : (t.registeredCount || 0);
  const isSeatsFull = participantsCount >= maxSeats;

  const { isProfileComplete, hasClubCard, shouldShowClubCardBanner } = checkProfileCompleteness(vkUser, profile);
  const hasAdminRole = checkCanViewVkId(vkUser, storeHasAdminRole, profile);

  const groupId = getEffectiveVkGroupId();
  const communityMessagesUrl = groupId > 0
    ? `https://vk.me/club${groupId}`
    : 'https://vk.me/club238367404';

  const handleRegister = async () => {
    if (isRegistering || isActionLoading) return;
    if (!hasClubCard) {
      triggerHaptic('heavy');
      setIsProfileModalOpen(true);
      return;
    }
    if (!isProfileComplete) {
      triggerHaptic('heavy');
      setIsProfileModalOpen(true);
      return;
    }

    setIsRegistering(true);
    triggerHaptic('heavy');
    try {
      // Всегда вызываем запрос разрешений на сообщения сообщества при клике на регистрацию
      // (подписка на группу не дает прав на отправку сообщений, нужен именно отдельный permission)
      await requestGroupMessagesPermission(groupId || 238367404);
      await registerToTournament(t.id);
    } finally {
      setIsRegistering(false);
    }
  };

  const handleConfirmUnregister = async () => {
    setIsCancelConfirmOpen(false);
    triggerHaptic('medium');
    await unregisterFromTournament(t.id);
  };

  const formatCardDate = (dateStr: string) => {
    try {
      const date = new Date(dateStr);
      const time = date.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
      const now = new Date();
      const isToday = date.toDateString() === now.toDateString();
      if (isToday) return `СЕГОДНЯ · ${time}`;
      
      const day = date.getDate();
      const month = date.toLocaleDateString('ru-RU', { month: 'long' }).toUpperCase();
      return `${day} ${month} · ${time}`;
    } catch {
      return 'СЕГОДНЯ · 19:00';
    }
  };

  const getRegEndString = (startTimeStr: string, regEndStr?: string | null) => {
    try {
      if (regEndStr) {
        const regEndDate = new Date(regEndStr);
        if (!isNaN(regEndDate.getTime())) {
          const startDate = new Date(startTimeStr);
          const isSameDay = !isNaN(startDate.getTime()) && 
            regEndDate.toDateString() === startDate.toDateString();
          const time = regEndDate.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
          if (isSameDay) {
            return `в ${time}`;
          }
          const day = regEndDate.getDate();
          const month = regEndDate.toLocaleDateString('ru-RU', { month: 'short' });
          return `${day} ${month} в ${time}`;
        }
      }
      const date = new Date(startTimeStr);
      date.setMinutes(date.getMinutes() - 15);
      return `в ${date.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })}`;
    } catch {
      return 'в 18:45';
    }
  };

  const renderActionButton = () => {
    if (isRegistered) {
      return (
        <button
          type="button"
          onClick={() => { triggerHaptic('light'); setIsCancelConfirmOpen(true); }}
          disabled={isActionLoading}
          className="w-full py-3.5 px-6 rounded-full bg-[#24332b] border border-white/10 text-[#fca5a5] hover:text-white font-bold text-sm hover:bg-[#34463c] active:scale-[0.98] transition-all shadow-xl flex items-center justify-center gap-2"
        >
          {isActionLoading ? 'Отмена записи...' : 'Отменить запись'}
        </button>
      );
    }

    if (t.status === TournamentStatus.Announced) {
      return (
        <button
          type="button"
          disabled
          className="w-full py-3.5 px-6 rounded-full bg-neutral-800 text-neutral-400 border border-neutral-700/50 font-bold text-sm shadow-xl cursor-not-allowed"
        >
          Регистрация откроется позже
        </button>
      );
    }

    if (t.status === TournamentStatus.Finished) {
      return (
        <button
          type="button"
          disabled
          className="w-full py-3.5 px-6 rounded-full bg-neutral-800 text-neutral-400 border border-neutral-700/50 font-bold text-sm shadow-xl cursor-not-allowed"
        >
          Турнир завершен
        </button>
      );
    }

    if (isSeatsFull) {
      return (
        <button
          type="button"
          disabled
          className="w-full py-3.5 px-6 rounded-full bg-neutral-800 text-neutral-400 border border-neutral-700/50 font-bold text-sm shadow-xl cursor-not-allowed"
        >
          Все места заняты
        </button>
      );
    }

    if (t.status === TournamentStatus.RegistrationOpen) {
      const isPast = new Date(t.startTime).getTime() <= currentTime;
      if (isPast) {
        return (
          <button
            type="button"
            disabled
            className="w-full py-3.5 px-6 rounded-full bg-neutral-800 text-neutral-400 border border-neutral-700/50 font-bold text-sm shadow-xl cursor-not-allowed"
          >
            Регистрация закрыта
          </button>
        );
      }

      if (!hasClubCard) {
        return (
          <button
            type="button"
            onClick={() => {
              triggerHaptic('medium');
              setIsProfileModalOpen(true);
            }}
            data-testid="get-club-card-button"
            className="w-full py-4 px-6 rounded-full bg-gradient-to-r from-[#d8af56] to-[#b38833] text-black font-extrabold text-base shadow-xl shadow-[#c39a44]/30 hover:brightness-105 active:scale-[0.98] transition-all flex items-center justify-center gap-2"
          >
            <CreditCard className="w-5 h-5 shrink-0" />
            <span>Получить клубный ID для записи</span>
          </button>
        );
      }

      return (
        <button
          type="button"
          onClick={handleRegister}
          disabled={isActionLoading || isRegistering}
          data-testid="register-button"
          className="w-full py-4 px-6 rounded-full bg-[#c39a44] text-white font-bold text-base shadow-xl hover:brightness-105 active:scale-[0.98] transition-all flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {isActionLoading || isRegistering ? 'Запись...' : !isProfileComplete ? 'Заполнить профиль для записи' : 'Зарегистрироваться'}
        </button>
      );
    }

    return (
      <button
        type="button"
        disabled
        className="w-full py-3.5 px-6 rounded-full bg-neutral-800 text-neutral-400 border border-neutral-700/50 font-bold text-sm shadow-xl cursor-not-allowed"
      >
        {t.status === TournamentStatus.Running ? 'Турнир уже идет' : 'Регистрация закрыта'}
      </button>
    );
  };

  return (
    <div className="fixed inset-0 z-50 bg-[#01201a] overflow-y-auto animate-fade-in flex flex-col justify-between">
      <div className="p-5 pb-28 max-w-md mx-auto w-full space-y-4">
        {/* Шапка модального окна */}
        <div className="flex items-center justify-between safe-top">
          <button
            type="button"
            onClick={() => { triggerHaptic('light'); closeDetail(); }}
            className="p-2 -ml-2 text-white hover:opacity-80 active:scale-95 transition-opacity"
            title="Назад"
          >
            <ChevronLeft className="w-7 h-7" />
          </button>
          <img src={CURRENT_BRANDING.assets.logoSvg} alt={CURRENT_BRANDING.clubName} className="h-7 object-contain" />
        </div>

        {/* Заголовок и дата турнира */}
        <div>
          <h1 className="text-2xl font-extrabold text-white tracking-tight mb-1">
            {t.title}
          </h1>
          <div className="flex items-center gap-2 text-xs font-semibold text-[#a4c9b7]">
            <Clock className="w-3.5 h-3.5 text-[#c39a44]" />
            <span>{formatCardDate(t.startTime)}</span>
          </div>
        </div>

        {/* Пиллы параметров турнира */}
        <div className="flex flex-wrap gap-2">
          <span className="px-3.5 py-1 rounded-full text-xs font-semibold text-white bg-black/70 border border-[#1a3b2b]">
            {t.format || 'NL Holdem'}
          </span>
          <span className="px-3.5 py-1 rounded-full text-xs font-semibold text-white bg-black/70 border border-[#1a3b2b]">
            стартовый стек {(t.startingStack ?? t.startingChips ?? 10000).toLocaleString('ru-RU')} chips
          </span>
          <span className="px-3.5 py-1 rounded-full text-xs font-semibold text-white bg-black/70 border border-[#1a3b2b]">
            {formatCurrency(t.buyIn)}
          </span>
        </div>

        {/* Баннер «Ваше место подтверждено» при активной записи */}
        {isRegistered && (
          <div className="p-4 rounded-2xl bg-gradient-to-r from-[#0d2a1f] to-[#143d2c] border border-[#c39a44]/60 shadow-lg flex items-center gap-3.5 animate-fade-in">
            <div className="w-10 h-10 rounded-full bg-[#c39a44]/20 border border-[#c39a44] flex items-center justify-center shrink-0 shadow-sm">
              <CheckCircle2 className="w-6 h-6 text-[#e5c06e]" />
            </div>
            <div className="flex-1">
              <div className="text-sm font-extrabold text-white">Ваше место подтверждено</div>
              <div className="text-xs text-[#a4c9b7] mt-0.5">Ждем вас в клубе к началу турнира</div>
            </div>
          </div>
        )}

        {/* Баннер ошибки (если произошла ошибка при регистрации/отмене) */}
        {actionError && (
          <div className="p-3.5 rounded-2xl bg-red-950/80 border border-red-500/50 text-red-200 text-xs flex items-start gap-2.5 shadow-md animate-fade-in">
            <AlertCircle className="w-4 h-4 text-red-400 shrink-0 mt-0.5" />
            <div className="flex-1">
              <span className="font-semibold">{actionError}</span>
            </div>
          </div>
        )}

        {/* Предупреждение о необходимости заполнить профиль перед регистрацией */}
        {!isRegistered && !isProfileComplete && (
          <div 
            onClick={() => { triggerHaptic('medium'); setIsProfileModalOpen(true); }}
            className="p-3.5 rounded-2xl bg-amber-950/60 border border-amber-500/40 text-amber-200 text-xs flex items-center justify-between gap-3 shadow-md cursor-pointer hover:bg-amber-950/80 transition-all animate-fade-in"
          >
            <div className="flex items-center gap-2.5">
              <AlertCircle className="w-4 h-4 text-amber-400 shrink-0" />
              <span className="font-semibold">Для записи на турнир требуется заполнить реальное ФИО и номер телефона</span>
            </div>
            <span className="text-[11px] font-bold text-[#c39a44] shrink-0 underline">Заполнить</span>
          </div>
        )}

        {/* Баннер проверки наличия клубного ID */}
        {!isRegistered && shouldShowClubCardBanner && (
          <div 
            onClick={() => { triggerHaptic('medium'); setIsProfileModalOpen(true); }}
            className="p-3.5 rounded-2xl bg-[#0a231b] border border-[#c39a44]/50 text-white text-xs flex items-start gap-3 shadow-md animate-fade-in cursor-pointer hover:bg-[#0d2e23] transition-all"
          >
            <CreditCard className="w-4 h-4 text-[#c39a44] shrink-0 mt-0.5" />
            <div className="flex-1 space-y-1">
              <div className="font-semibold text-[#e5c06e] leading-snug">
                Для регистрации необходим клубный ID. Напишите в сообщения сообщества для получения карты.
              </div>
              <div className="flex items-center justify-between pt-0.5">
                <a
                  href={communityMessagesUrl}
                  target="_top"
                  rel="noopener noreferrer"
                  onClick={(e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    triggerHaptic('light');
                    openExternalUrl(communityMessagesUrl);
                  }}
                  className="inline-flex items-center gap-1 text-[11px] font-bold text-[#c39a44] hover:text-[#e5c06e] underline transition-colors"
                >
                  <span>Получить ID в группе ВК</span>
                  <span className="text-[10px]">↗</span>
                </a>
                <span className="text-[11px] font-bold text-[#c39a44] underline">Указать ID</span>
              </div>
            </div>
          </div>
        )}

        {/* Основной информационный блок */}
        <div className="p-5 rounded-3xl bg-black/50 border border-white/10 shadow-xl space-y-4">
          {/* Места и прогресс-бар */}
          <div>
            <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider mb-1.5 flex items-center justify-between">
              <span>МЕСТА</span>
              <span className="text-[#a4c9b7] font-semibold">{participantsCount} из {maxSeats}</span>
            </div>
            <div className="flex items-center gap-3">
              <span className="text-sm font-bold text-white whitespace-nowrap">
                {isSeatsFull ? 'Мест не осталось' : maxSeats - participantsCount <= 5 ? 'Осталось мало мест' : 'Есть места'}
              </span>
              <div className="flex-1 h-2 rounded-full bg-[#132c20] overflow-hidden">
                <div
                  className="h-full rounded-full bg-[#3d6550] transition-all duration-300"
                  style={{ width: `${Math.min(100, Math.round((participantsCount / maxSeats) * 100))}%` }}
                />
              </div>
            </div>
          </div>

          {/* Адрес клуба */}
          <div className="pt-2 border-t border-white/5">
            <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider mb-1 flex items-center gap-1.5">
              <MapPin className="w-3 h-3 text-[#c39a44]" />
              <span>АДРЕС КЛУБА</span>
            </div>
            <div className="text-sm font-semibold text-white">
              {t.clubAddress || (t.clubName ? `${t.clubName}, ${t.cityName || ''}` : CURRENT_BRANDING.defaultAddress)}
            </div>
          </div>

          {/* Конец регистрации */}
          <div className="pt-2 border-t border-white/5">
            <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider mb-1 flex items-center gap-1.5">
              <Clock className="w-3 h-3 text-[#c39a44]" />
              <span>КОНЕЦ РЕГИСТРАЦИИ</span>
            </div>
            <div className="text-sm font-semibold text-white">
              {getRegEndString(t.startTime, t.registrationEnd)}
            </div>
          </div>

          {/* Полное описание турнира */}
          <div className="pt-2 border-t border-white/5">
            <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider mb-1.5 flex items-center gap-1.5">
              <Info className="w-3 h-3 text-[#c39a44]" />
              <span>О ТУРНИРЕ</span>
            </div>
            <p className="text-xs text-[#d1e0d7] leading-relaxed whitespace-pre-line">
              {t.description || (
                <>
                  Турнир для гостей и членов клуба {CURRENT_BRANDING.clubName}.
                  {'\n\n'}• Стартовый стек — {(t.startingStack ?? t.startingChips ?? 10000).toLocaleString('ru-RU')} фишек
                  {'\n'}• Формат — {t.format || 'No Limit Holdem'}
                  {'\n'}• Блайнд-апы — 15 минут
                  {'\n'}• Поздняя регистрация — 3 часа
                </>
              )}
            </p>
          </div>
        </div>

        {/* Секция участников */}
        <div className="p-5 rounded-3xl bg-black/50 border border-white/10 shadow-xl space-y-3">
          <div className="flex items-center justify-between pb-2 border-b border-white/5">
            <div className="flex items-center gap-2">
              <Users className="w-4 h-4 text-[#c39a44]" />
              <h3 className="text-sm font-extrabold text-white">
                Участники ({participantsCount} / {maxSeats})
              </h3>
            </div>
          </div>

          {t.participants && t.participants.length > 0 ? (
            <div className="space-y-2 max-h-64 overflow-y-auto pr-1">
              {t.participants.map((player, index) => {
                const isCurrentPlayer = vkUser && player.vkId === vkUser.id.toString();
                const playerName = [player.firstName, player.lastName].filter(Boolean).join(' ') || `Игрок #${player.userId}`;

                return (
                  <div
                    key={player.userId || `${player.vkId}-${index}`}
                    className={`flex items-center justify-between p-2.5 rounded-2xl border transition-all ${
                      isCurrentPlayer
                        ? 'bg-[#192d23] border-[#c39a44]/70 shadow-sm'
                        : 'bg-[#0a1e16]/60 border-white/5'
                    }`}
                  >
                    <div className="flex items-center gap-3 min-w-0">
                      {/* Порядковый номер */}
                      <span className="text-xs font-bold text-[#8fa89b] w-4 text-center">
                        {index + 1}
                      </span>

                      {/* Аватар */}
                      <PlayerAvatar
                        avatarUrl={player.avatarUrl}
                        firstName={playerName}
                        className="w-8 h-8 rounded-full"
                        textClassName="text-[11px] font-black"
                      />

                      {/* Имя игрока */}
                      <div className="truncate">
                        <div className="text-xs font-bold text-white flex items-center gap-1.5 truncate">
                          <span className="truncate">{playerName}</span>
                          {isCurrentPlayer && (
                            <span className="text-[10px] text-[#c39a44] font-semibold px-1.5 py-0.5 bg-[#c39a44]/10 rounded">
                              Вы
                            </span>
                          )}
                        </div>
                        {hasAdminRole && Boolean(player.vkId) && (
                          <div className="text-xs text-neutral-500">
                            <a
                              href={`https://vk.com/id${player.vkId}`}
                              target="_top"
                              rel="noreferrer"
                              onClick={(e) => {
                                e.preventDefault();
                                e.stopPropagation();
                                openExternalUrl(`https://vk.com/id${player.vkId}`);
                              }}
                              className="hover:text-[#c39a44] transition-colors truncate inline-flex items-center gap-0.5 text-inherit"
                            >
                              <span>VK ID: {player.vkId}</span>
                              <span className="text-[#c39a44] text-[9px]">↗</span>
                            </a>
                          </div>
                        )}
                      </div>
                    </div>

                    {/* Рейтинг игрока */}
                    <div className="flex items-center gap-1 text-[11px] font-bold text-[#c39a44] bg-[#0a231b] border border-[#1e533f] px-2.5 py-1 rounded-full shrink-0 ml-2">
                      <Trophy className="w-3 h-3 text-[#c39a44]" />
                      <span>{player.totalRating ?? 0}</span>
                    </div>
                  </div>
                );
              })}
            </div>
          ) : (
            <div className="py-6 text-center text-xs text-[#8fa89b]">
              Пока никто не зарегистрировался на этот турнир.
            </div>
          )}
        </div>
      </div>

      {/* Фиксированная нижняя панель с кнопкой действия */}
      <div className="fixed bottom-0 left-0 right-0 p-5 bg-gradient-to-t from-[#01201a] via-[#01201a]/95 to-transparent z-10">
        <div className="max-w-md mx-auto w-full">
          {renderActionButton()}
        </div>
      </div>

      {/* Модальное окно подтверждения отмены записи */}
      {isCancelConfirmOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-6 bg-black/85 backdrop-blur-md animate-fade-in">
          <div className="w-full max-w-xs p-6 rounded-3xl bg-[#091510] border border-white/10 text-center shadow-2xl space-y-4">
            <h2 className="text-xl font-extrabold text-white">
              Отменить запись?
            </h2>
            <p className="text-xs text-[#8fa89b] leading-relaxed">
              Ваше место станет доступно другим игрокам клуба.
            </p>
            <div className="flex gap-2.5 pt-2">
              <button
                type="button"
                onClick={handleConfirmUnregister}
                disabled={isActionLoading}
                className="flex-1 py-3 px-3 rounded-full bg-[#444444] text-white font-bold text-xs hover:bg-[#555555] active:scale-95 transition-all"
              >
                Отменить
              </button>
              <button
                type="button"
                onClick={() => setIsCancelConfirmOpen(false)}
                disabled={isActionLoading}
                className="flex-1 py-3 px-3 rounded-full bg-[#c39a44] text-white font-bold text-xs hover:brightness-105 active:scale-95 transition-all"
              >
                Не отменять
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
