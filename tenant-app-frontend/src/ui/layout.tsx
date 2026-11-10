import { useAuth } from '@/auth/AuthProvider';
import { CompanyBrand } from './CompanyBrand';
import { usePathname } from 'expo-router';
import { type ReactNode } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View, useWindowDimensions } from 'react-native';
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
  const wide = useWindowDimensions().width >= 900;
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
          contentContainerStyle={{ width: '100%', maxWidth: header ? 1120 : 500, alignSelf: 'center', padding: wide ? 32 : 20, paddingBottom: space.xxl + bottom, gap: space.lg }}
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
  const compact = useWindowDimensions().width < 400;
  const nav = useNav();
  const notifications = useQuery({ queryKey: ['notifications'], queryFn: api.notifications, refetchInterval: 60000 });
  const unread = notifications.data?.filter((n) => !n.read).length ?? 0;
  return (
    <View style={styles.brand}>
      <CompanyBrand compact={compact} />
      <View style={{ flex: 1 }} />
      <DesktopNavigation />
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
    <View>
      <CompanyHeader />
      <View style={styles.back}>
      <Pressable onPress={nav.back} hitSlop={10} accessibilityRole="button" accessibilityLabel="Назад" style={styles.iconBtn}>
        <Icon name="chevron-back" size={26} color={colors.heading} />
      </Pressable>
      <Text style={styles.backTitle} numberOfLines={1}>{title}</Text>
      {right}
      </View>
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


const navigation = [
  ['/', 'Главная'], ['/invoices', 'Счета'], ['/requests', 'Обращения'],
  ['/documents', 'Документы'], ['/premises', 'Помещения'], ['/news', 'Новости'],
] as const;
function DesktopNavigation() {
  const wide = useWindowDimensions().width >= 1000;
  const { user } = useAuth();
  const pathname = usePathname();
  const nav = useNav();
  if (!wide || user?.role !== 'Tenant') return null;
  return (
    <View style={{ flexDirection: 'row', gap: 4 }}>
      {navigation.map(([href, label]) => (
        <Pressable
          key={href}
          accessibilityRole="link"
          accessibilityState={{ selected: pathname === href }}
          onPress={() => nav.push(href)}
          style={{ paddingHorizontal: 12, paddingVertical: 14, borderBottomWidth: 2, borderBottomColor: pathname === href ? colors.accent : 'transparent' }}
        >
          <AppText variant="small" color={pathname === href ? colors.accentDark : colors.muted}>{label}</AppText>
        </Pressable>
      ))}
    </View>
  );
}
function CompanyHeader() {
  return <View style={styles.brand}><CompanyBrand /><View style={{ flex: 1 }} /><DesktopNavigation /></View>;
}

const styles = StyleSheet.create({
  brand: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.md,
    paddingHorizontal: space.xl,
    paddingVertical: space.md,
    backgroundColor: colors.bg,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
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
  dotText: { color: colors.card, fontSize: 11, fontWeight: '700' },
  back: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.sm,
    paddingHorizontal: space.md,
    height: 56,
    backgroundColor: colors.bg,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  backTitle: { flex: 1, fontSize: 24, fontWeight: '400', color: colors.accentDark },
});
