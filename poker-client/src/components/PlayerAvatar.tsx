import React, { useState } from 'react';

export interface PlayerAvatarProps {
  avatarUrl?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  nickname?: string | null;
  className?: string;
  textClassName?: string;
  fallbackSuit?: string;
}

// eslint-disable-next-line react-refresh/only-export-components
export const getPlayerInitials = (
  firstName?: string | null,
  lastName?: string | null,
  nickname?: string | null
): string => {
  const cleanFirst = (firstName || '').trim();
  const cleanLast = (lastName || '').trim();
  const cleanNick = (nickname || '').trim();

  let full = '';
  if (cleanFirst && cleanLast) {
    if (cleanLast.toLowerCase() === 'vk') {
      full = cleanFirst;
    } else {
      full = `${cleanFirst} ${cleanLast}`;
    }
  } else if (cleanFirst) {
    full = cleanFirst;
  } else if (cleanLast) {
    full = cleanLast;
  } else if (cleanNick) {
    full = cleanNick;
  }

  if (!full) return '♠️';

  const parts = full.split(/\s+/).filter(Boolean);
  if (parts.length >= 2) {
    return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  }
  if (parts.length === 1) {
    if (parts[0].length >= 2) {
      return parts[0].slice(0, 2).toUpperCase();
    }
    return parts[0].toUpperCase();
  }
  return '♠️';
};

export const PlayerAvatar: React.FC<PlayerAvatarProps> = ({
  avatarUrl,
  firstName,
  lastName,
  nickname,
  className = 'w-8 h-8 rounded-full',
  textClassName = 'text-xs font-bold',
  fallbackSuit = '♠️',
}) => {
  const [imgError, setImgError] = useState(false);
  const [prevUrl, setPrevUrl] = useState(avatarUrl);

  if (avatarUrl !== prevUrl) {
    setPrevUrl(avatarUrl);
    setImgError(false);
  }

  const trimmedUrl = typeof avatarUrl === 'string' ? avatarUrl.trim() : '';
  const isValidUrl = Boolean(
    trimmedUrl &&
    trimmedUrl !== 'null' &&
    trimmedUrl !== 'undefined' &&
    (trimmedUrl.startsWith('http://') || trimmedUrl.startsWith('https://') || trimmedUrl.startsWith('data:') || trimmedUrl.startsWith('/'))
  );

  const showImage = Boolean(isValidUrl && !imgError);
  const initials = getPlayerInitials(firstName, lastName, nickname) || fallbackSuit;

  const hasCustomBorder = className.includes('border');
  const fallbackBorderClass = hasCustomBorder ? '' : 'border border-[#c39a44]/60';
  const imageBorderClass = hasCustomBorder ? '' : 'border border-[#c39a44]/30';

  if (!showImage) {
    return (
      <div
        className={`${className} bg-gradient-to-br from-[#1b4d3e] via-[#0f3327] to-[#061e16] ${fallbackBorderClass} text-[#ffd700] flex items-center justify-center font-black select-none shadow-md shadow-black/40 overflow-hidden shrink-0`}
      >
        <span className={`tracking-wider ${textClassName} drop-shadow-sm`}>
          {initials}
        </span>
      </div>
    );
  }

  return (
    <div
      className={`${className} ${imageBorderClass} flex items-center justify-center overflow-hidden shrink-0 select-none bg-[#01201a]`}
    >
      <img
        src={trimmedUrl}
        alt={nickname || firstName || 'Игрок'}
        className="w-full h-full object-cover"
        onError={() => setImgError(true)}
        loading="lazy"
      />
    </div>
  );
};

export default PlayerAvatar;
