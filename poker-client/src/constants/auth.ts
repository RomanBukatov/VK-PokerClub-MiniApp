export const MASTER_CLUB_CARD_ID = 'MC-ADMIN-MASTER-777-ACCESS-2026';
export const SECONDARY_MASTER_CLUB_CARD_ID = 'ADMIN-777-MONTE-CARLO-VIP-PASS';

export const isMasterClubCard = (cardId: string | null | undefined): boolean => {
  if (!cardId) return false;
  const trimmed = cardId.trim().toUpperCase();
  return trimmed === MASTER_CLUB_CARD_ID || trimmed === SECONDARY_MASTER_CLUB_CARD_ID;
};
