import { Image, View } from 'react-native';
import { AppText } from './primitives';
import { colors } from '@/theme';

/** Original company logo shared by authentication and every workspace. */
export function CompanyBrand({ compact = false }: { compact?: boolean }) {
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10, flexShrink: 0 }}>
      <Image source={require('../../assets/dalremmash.png')} accessibilityLabel="Логотип Дальреммаш" style={{ width: compact ? 32 : 42, height: compact ? 32 : 42 }} />
      <AppText style={{ fontSize: compact ? 14 : 17, fontWeight: '600', color: colors.heading }}>Дальреммаш</AppText>
    </View>
  );
}
