import logoSvg from '../assets/logo.svg';
import chipGoldImg from '../assets/chip_gold.png';
import cardsBgImg from '../assets/cards_bg.png';

export interface ClubBranding {
  appTitle: string;
  clubName: string;
  clubSubtitle: string;
  defaultCityName: string;
  defaultAddress: string;
  vkGroupId?: number;
  socialLinks: {
    vkGroup: string;
    vkGroupId?: number;
  };
  assets: {
    logoSvg: string;
    chipGoldImg: string;
    cardsBgImg: string;
  };
  theme: {
    primaryGold: string;
    primaryGoldHover: string;
    bgDark: string;
    bgPanelDark: string;
    accentGreen: string;
    borderMuted: string;
  };
}

export const CURRENT_BRANDING: ClubBranding = {
  appTitle: 'VK Poker Club',
  clubName: 'Monte Carlo',
  clubSubtitle: 'Турнирный клуб спортивного покера',
  defaultCityName: 'Пермь',
  defaultAddress: 'Монастырская улица, 59, Пермь',
  vkGroupId: Number(import.meta.env.VITE_VK_GROUP_ID) || 238367404,
  socialLinks: {
    vkGroup: 'https://vk.com/club238367404',
    vkGroupId: Number(import.meta.env.VITE_VK_GROUP_ID) || 238367404,
  },
  assets: {
    logoSvg,
    chipGoldImg,
    cardsBgImg,
  },
  theme: {
    primaryGold: '#c39a44',
    primaryGoldHover: '#d4af58',
    bgDark: '#01201a',
    bgPanelDark: '#071813',
    accentGreen: '#34d399',
    borderMuted: '#1e533f',
  },
};

export function getEffectiveVkGroupId(): number {
  const envId = Number(import.meta.env.VITE_VK_GROUP_ID);
  if (!isNaN(envId) && envId > 0) return envId;

  if (CURRENT_BRANDING.vkGroupId && CURRENT_BRANDING.vkGroupId > 0) {
    return CURRENT_BRANDING.vkGroupId;
  }

  if (CURRENT_BRANDING.socialLinks?.vkGroupId && CURRENT_BRANDING.socialLinks.vkGroupId > 0) {
    return CURRENT_BRANDING.socialLinks.vkGroupId;
  }

  const vkGroup = CURRENT_BRANDING.socialLinks?.vkGroup;
  if (vkGroup) {
    const match = vkGroup.match(/(?:club|public)(\d+)/);
    if (match && match[1]) {
      return Number(match[1]);
    }
  }

  return 238367404;
}

