import { Image, View } from 'react-native';
import { AppText } from './primitives';
import { colors } from '@/theme';

/** Shared company identity. Replace the provisional vector with the supplied original logo. */
export function CompanyBrand({ compact = false }: { compact?: boolean }) {
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10, flexShrink: 0 }}>
      <Image source={require('../../assets/dalremmash.svg')} accessibilityLabel="Логотип Дальреммаш" style={{ width: compact ? 32 : 42, height: compact ? 32 : 42 }} />
      <AppText style={{ fontSize: compact ? 14 : 17, fontWeight: '600', color: colors.heading }}>Дальреммаш</AppText>
    </View>
  );
}
