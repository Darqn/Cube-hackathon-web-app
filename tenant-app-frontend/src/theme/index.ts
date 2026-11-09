import { TextStyle } from 'react-native';

export const colors = {
  bg: '#FAF9F6',
  card: '#FFFFFF',
  text: '#353630',
  heading: '#30312C',
  accent: '#996C28',
  accentDark: '#86601F',
  accentSoft: '#F5F0E5',
  border: '#E8E2D6',
  muted: '#72736B',
  inactive: '#77786E',
  success: '#2F7D4F',
  successBg: '#E6F3EB',
  danger: '#B3372F',
  dangerBg: '#FBEAE8',
  warning: '#86601F',
  warningBg: '#F5F0E5',
  info: '#2F5F9A',
  infoBg: '#E8F0F9',
  neutral: '#5C6169',
  neutralBg: '#EFEDE8',
};

export const space = { xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32 };
export const radius = { sm: 2, md: 3, lg: 4, pill: 999 };

export const type: Record<string, TextStyle> = {
  title: { fontSize: 32, lineHeight: 39, fontWeight: '400', color: colors.accentDark },
  h2: { fontSize: 19, lineHeight: 26, fontWeight: '500', color: colors.heading },
  h3: { fontSize: 16, lineHeight: 22, fontWeight: '600', color: colors.heading },
  body: { fontSize: 15, lineHeight: 22, color: colors.text },
  small: { fontSize: 13, lineHeight: 18, color: colors.muted },
  caption: { fontSize: 12, lineHeight: 16, color: colors.muted },
  amount: { fontSize: 22, lineHeight: 28, fontWeight: '700', color: colors.text },
};

export const shadow = {
  shadowColor: '#30312C',
  shadowOpacity: 0,
  shadowRadius: 8,
  shadowOffset: { width: 0, height: 2 },
  elevation: 0,
};
