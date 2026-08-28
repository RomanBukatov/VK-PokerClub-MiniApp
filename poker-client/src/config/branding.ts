import logoSvg from '../assets/logo.svg';
import chipGoldImg from '../assets/chip_gold.png';
import cardsBgImg from '../assets/cards_bg.png';

export interface ClubBranding {
  appTitle: string;
  clubName: string;
  clubSubtitle: string;
  defaultCityName: string;
  defaultAddress: string;
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
