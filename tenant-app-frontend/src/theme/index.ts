import { TextStyle } from 'react-native';

export const colors = {
  bg: '#FAF8F4',
  card: '#FFFFFF',
  text: '#22252A',
  heading: '#3A3F47',
  accent: '#B8893A',
  accentDark: '#8F6A27',
  accentSoft: '#F6EEDD',
  border: '#E7E2D8',
  muted: '#7A7F87',
  inactive: '#8B9098',
  success: '#2F7D4F',
  successBg: '#E6F3EB',
  danger: '#B3372F',
  dangerBg: '#FBEAE8',
  warning: '#8F6A27',
  warningBg: '#F6EEDD',
  info: '#2F5F9A',
  infoBg: '#E8F0F9',
  neutral: '#5C6169',
  neutralBg: '#EFEDE8',
};

export const space = { xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32 };
export const radius = { sm: 8, md: 12, lg: 16, pill: 999 };

export const type: Record<string, TextStyle> = {
  title: { fontSize: 26, lineHeight: 32, fontWeight: '700', color: colors.heading },
  h2: { fontSize: 19, lineHeight: 25, fontWeight: '700', color: colors.heading },
  h3: { fontSize: 16, lineHeight: 22, fontWeight: '600', color: colors.heading },
  body: { fontSize: 15, lineHeight: 22, color: colors.text },
  small: { fontSize: 13, lineHeight: 18, color: colors.muted },
  caption: { fontSize: 12, lineHeight: 16, color: colors.muted },
  amount: { fontSize: 22, lineHeight: 28, fontWeight: '700', color: colors.text },
};

export const shadow = {
  shadowColor: '#3A3F47',
  shadowOpacity: 0.06,
  shadowRadius: 8,
  shadowOffset: { width: 0, height: 2 },
  elevation: 1,
};
