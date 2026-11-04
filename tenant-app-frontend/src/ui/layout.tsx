import { type ReactNode } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/api';
import { colors, space } from '@/theme';
import { useNav } from '@/lib/nav';
import { AppText, Icon } from './primitives';

/** Каркас экрана: безопасные отступы, прокрутка, «потяни, чтобы обновить». */
export function Screen({
  children,
  header,
  footer,
  scroll = true,
  tabs = false,
  refreshing,
  onRefresh,
}: {
  children: ReactNode;
  header?: ReactNode;
  footer?: ReactNode;
  scroll?: boolean;
  tabs?: boolean;
  refreshing?: boolean;
  onRefresh?: () => void;
}) {
  const insets = useSafeAreaInsets();
  const bottom = tabs ? 0 : insets.bottom;
  return (
    <KeyboardAvoidingView
      style={{ flex: 1, backgroundColor: colors.bg, paddingTop: insets.top }}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      {header}
      {scroll ? (
        <ScrollView
          keyboardShouldPersistTaps="handled"
          contentContainerStyle={{ width: '100%', maxWidth: header ? 1120 : 500, alignSelf: 'center', padding: space.lg, paddingBottom: space.xxl + bottom, gap: space.lg }}
          refreshControl={
            onRefresh ? (
              <RefreshControl refreshing={!!refreshing} onRefresh={onRefresh} tintColor={colors.accent} colors={[colors.accent]} />
            ) : undefined
          }
        >
          {children}
        </ScrollView>
      ) : (
        <View style={{ flex: 1, width: '100%', maxWidth: 1120, alignSelf: 'center' }}>{children}</View>
      )}
      {footer ? <View style={{ paddingBottom: bottom }}>{footer}</View> : null}
    </KeyboardAvoidingView>
  );
}

/** Верхняя панель главных экранов: название компании, уведомления, профиль. */
export function BrandBar() {
  const nav = useNav();
  const notifications = useQuery({ queryKey: ['notifications'], queryFn: api.notifications, refetchInterval: 60000 });
  const unread = notifications.data?.filter((n) => !n.read).length ?? 0;
  return (
    <View style={styles.brand}>
      <View style={styles.logo}>
        <Icon name="cube-outline" size={24} color={colors.accent} />
      </View>
      <View style={{ flex: 1 }}>
        <Text style={styles.brandName} numberOfLines={1}>КУБ · Дальреммаш</Text>
        <AppText variant="small">Личный кабинет арендатора</AppText>
      </View>
      <Pressable onPress={() => nav.push('/notifications')} hitSlop={8} accessibilityRole="button" accessibilityLabel="Уведомления" style={styles.iconBtn}>
        <Icon name="notifications-outline" size={26} color={colors.heading} />
        {unread > 0 ? (
          <View style={styles.dot}>
            <Text style={styles.dotText}>{unread > 9 ? '9+' : unread}</Text>
          </View>
        ) : null}
      </Pressable>
      <Pressable onPress={() => nav.push('/profile')} hitSlop={8} accessibilityRole="button" accessibilityLabel="Профиль" style={styles.iconBtn}>
        <Icon name="person-circle-outline" size={30} color={colors.heading} />
      </Pressable>
    </View>
  );
}

/** Верхняя панель вложенных экранов: стрелка «назад» и заголовок. */
export function BackBar({ title, right }: { title: string; right?: ReactNode }) {
  const nav = useNav();
  return (
    <View style={styles.back}>
      <Pressable onPress={nav.back} hitSlop={10} accessibilityRole="button" accessibilityLabel="Назад" style={styles.iconBtn}>
        <Icon name="chevron-back" size={26} color={colors.heading} />
      </Pressable>
      <Text style={styles.backTitle} numberOfLines={1}>{title}</Text>
      {right}
    </View>
  );
}

/** Заголовок раздела на главных экранах. */
export function PageTitle({ title, subtitle, action }: { title: string; subtitle?: string; action?: ReactNode }) {
  return (
    <View style={{ flexDirection: 'row', flexWrap: 'wrap', alignItems: 'flex-start', gap: space.md }}>
      <View style={{ flex: 1, gap: 4 }}>
        <AppText variant="title">{title}</AppText>
        {subtitle ? <AppText variant="small">{subtitle}</AppText> : null}
      </View>
      {action}
    </View>
  );
}

const styles = StyleSheet.create({
  brand: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.md,
    paddingHorizontal: space.lg,
    paddingVertical: space.md,
    backgroundColor: colors.card,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  logo: {
    width: 44,
    height: 44,
    borderRadius: 22,
    borderWidth: 1.5,
    borderColor: colors.accent,
    backgroundColor: colors.accentSoft,
    alignItems: 'center',
    justifyContent: 'center',
  },
  brandName: { fontSize: 17, fontWeight: '700', color: colors.heading },
  iconBtn: { padding: 8, minWidth: 44, minHeight: 44, alignItems: 'center', justifyContent: 'center' },
  dot: {
    position: 'absolute',
    top: -4,
    right: -6,
    minWidth: 18,
    height: 18,
    borderRadius: 9,
    paddingHorizontal: 4,
    backgroundColor: colors.accent,
    alignItems: 'center',
    justifyContent: 'center',
  },
  dotText: { color: colors.text, fontSize: 11, fontWeight: '700' },
  back: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.sm,
    paddingHorizontal: space.md,
    height: 56,
    backgroundColor: colors.card,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  backTitle: { flex: 1, fontSize: 18, fontWeight: '700', color: colors.heading },
});
