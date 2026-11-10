import type { ReactNode } from 'react';
import { View, useWindowDimensions } from 'react-native';
import { Screen } from './layout';
import { CompanyBrand } from './CompanyBrand';
import { AppText } from './primitives';
import { colors } from '@/theme';

export function AuthScreen({ title, subtitle, children }: {
  title: string;
  subtitle?: string;
  children: ReactNode;
}) {
  const wide = useWindowDimensions().width >= 900;
  const section = title === 'Регистрация' ? 'РЕГИСТРАЦИЯ'
    : title === 'Восстановление пароля' ? 'ДОСТУП' : 'ВХОД';
  return (
    <Screen header={
      <View style={{ paddingHorizontal: wide ? 48 : 24, paddingVertical: 20, borderBottomWidth: 1, borderColor: colors.border }}>
        <CompanyBrand />
      </View>
    }>
      <View style={{ flexDirection: wide ? 'row' : 'column', gap: wide ? 100 : 28, paddingVertical: wide ? 56 : 16, alignItems: 'flex-start' }}>
        <View style={{ flex: wide ? 1 : undefined, width: wide ? undefined : '100%', gap: 18 }}>
          <AppText variant="caption" style={{ letterSpacing: 2 }}>ЛИЧНЫЙ КАБИНЕТ · {section}</AppText>
          <AppText variant="title" style={{ fontSize: wide ? 52 : 36, lineHeight: wide ? 60 : 43, maxWidth: 420 }}>{title}</AppText>
          <AppText variant="small">{subtitle ?? 'Для арендаторов и сотрудников'}</AppText>
          {wide ? (
            <View style={{ marginTop: 48, borderTopWidth: 1, borderColor: colors.border, paddingTop: 24, gap: 12 }}>
              <AppText variant="h3">Всё, что нужно для работы</AppText>
              <AppText variant="small" style={{ maxWidth: 300 }}>
                Помещения и договоры, счета и документы, обращения и показания — в вашем кабинете.
              </AppText>
            </View>
          ) : null}
        </View>
        <View style={{ width: wide ? 400 : '100%', gap: 20 }}>{children}</View>
      </View>
    </Screen>
  );
}
