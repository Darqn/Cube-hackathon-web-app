import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { ActivityIndicator, Animated, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import type { UseQueryResult } from '@tanstack/react-query';
import { colors, radius, space } from '@/theme';
import { errorMessage } from '@/api/errors';
import type { IconName } from '@/lib/config';
import { AppText, Button, Icon } from './primitives';

// ---------- Уведомления о результате действия ----------

type ToastTone = 'success' | 'error';
const ToastContext = createContext<(text: string, tone?: ToastTone) => void>(() => {});
export const useToast = () => useContext(ToastContext);

export function ToastProvider({ children }: { children: ReactNode }) {
  const insets = useSafeAreaInsets();
  const [toast, setToast] = useState<{ id: number; text: string; tone: ToastTone } | null>(null);
  const opacity = useRef(new Animated.Value(0)).current;
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const show = useCallback(
    (text: string, tone: ToastTone = 'success') => {
      if (timer.current) clearTimeout(timer.current);
      setToast({ id: Date.now(), text, tone });
      Animated.timing(opacity, { toValue: 1, duration: 180, useNativeDriver: true }).start();
      timer.current = setTimeout(() => {
        Animated.timing(opacity, { toValue: 0, duration: 250, useNativeDriver: true }).start(() => setToast(null));
      }, 3800);
    },
    [opacity],
  );

  useEffect(() => () => {
    if (timer.current) clearTimeout(timer.current);
  }, []);

  const ok = toast?.tone !== 'error';
  return (
    <ToastContext.Provider value={show}>
      {children}
      {toast ? (
        <Animated.View
          pointerEvents="none"
          accessibilityLiveRegion="polite"
          style={[styles.toast, { top: insets.top + space.sm, opacity, backgroundColor: ok ? colors.successBg : colors.dangerBg, borderColor: ok ? colors.success : colors.danger }]}
        >
          <Icon name={ok ? 'checkmark-circle' : 'alert-circle'} color={ok ? colors.success : colors.danger} size={22} />
          <Text style={{ flex: 1, color: colors.text, fontSize: 14, lineHeight: 20 }}>{toast.text}</Text>
        </Animated.View>
      ) : null}
    </ToastContext.Provider>
  );
}

// ---------- Загрузка, ошибка, пустое состояние ----------

export function Loading({ label = 'Загрузка…' }: { label?: string }) {
  return (
    <View style={styles.center} accessibilityLabel={label}>
      <ActivityIndicator color={colors.accent} />
      <AppText variant="small">{label}</AppText>
    </View>
  );
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <View style={styles.center}>
      <Icon name="cloud-offline-outline" size={36} color={colors.danger} />
      <AppText variant="h3">Не удалось загрузить данные</AppText>
      <AppText variant="small" style={{ textAlign: 'center' }}>
        {errorMessage(error)}
      </AppText>
      {onRetry ? <Button title="Повторить" variant="secondary" small onPress={onRetry} icon="refresh" /> : null}
    </View>
  );
}

export function EmptyState({
  icon = 'file-tray-outline',
  title,
  text,
  action,
  onAction,
}: {
  icon?: IconName;
  title: string;
  text?: string;
  action?: string;
  onAction?: () => void;
}) {
  return (
    <View style={styles.center}>
      <Icon name={icon} size={36} color={colors.inactive} />
      <AppText variant="h3">{title}</AppText>
      {text ? (
        <AppText variant="small" style={{ textAlign: 'center' }}>
          {text}
        </AppText>
      ) : null}
      {action && onAction ? <Button title={action} variant="secondary" small onPress={onAction} /> : null}
    </View>
  );
}

/** Показывает загрузку/ошибку/пустой список вместо содержимого, пока данные не готовы. */
export function Async<T>({
  query,
  children,
  isEmpty,
  empty,
}: {
  query: UseQueryResult<T, Error>;
  children: (data: T) => ReactNode;
  isEmpty?: (data: T) => boolean;
  empty?: ReactNode;
}) {
  if (query.isPending) return <Loading />;
  if (query.isError) return <ErrorState error={query.error} onRetry={() => query.refetch()} />;
  if (isEmpty?.(query.data)) return <>{empty}</>;
  return <>{children(query.data)}</>;
}

const styles = StyleSheet.create({
  toast: {
    position: 'absolute',
    left: space.lg,
    right: space.lg,
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.sm,
    padding: space.md,
    borderRadius: radius.md,
    borderWidth: 1,
    zIndex: 100,
    elevation: 10,
  },
  center: { alignItems: 'center', justifyContent: 'center', gap: space.sm, paddingVertical: space.xxl, paddingHorizontal: space.lg },
});
