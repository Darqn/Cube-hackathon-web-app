import { type ReactNode } from 'react';
import {
  ActivityIndicator,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
  type StyleProp,
  type TextStyle,
  type ViewStyle,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors, radius, shadow, space, type as typo } from '@/theme';
import type { IconName, Tone } from '@/lib/config';

export function AppText({
  variant = 'body',
  color,
  style,
  children,
  numberOfLines,
}: {
  variant?: 'title' | 'h2' | 'h3' | 'body' | 'small' | 'caption' | 'amount';
  color?: string;
  style?: StyleProp<TextStyle>;
  children: ReactNode;
  numberOfLines?: number;
}) {
  return (
    <Text numberOfLines={numberOfLines} style={[typo[variant], color ? { color } : null, style]}>
      {children}
    </Text>
  );
}

export function Icon({ name, size = 20, color = colors.accentDark }: { name: IconName; size?: number; color?: string }) {
  return <Ionicons name={name} size={size} color={color} />;
}

export function IconCircle({ name, size = 44, tone }: { name: IconName; size?: number; tone?: Tone }) {
  const t = tone ? toneColors[tone] : { fg: colors.accentDark, bg: colors.accentSoft };
  return (
    <View
      style={{
        width: size,
        height: size,
        borderRadius: size / 2,
        backgroundColor: t.bg,
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <Ionicons name={name} size={size * 0.5} color={t.fg} />
    </View>
  );
}

export function Card({
  children,
  onPress,
  style,
}: {
  children: ReactNode;
  onPress?: () => void;
  style?: StyleProp<ViewStyle>;
}) {
  if (onPress) {
    return (
      <Pressable
        onPress={onPress}
        accessibilityRole="button"
        style={({ pressed }) => [styles.card, pressed && styles.pressed, style]}
      >
        {children}
      </Pressable>
    );
  }
  return <View style={[styles.card, style]}>{children}</View>;
}

export function Button({
  title,
  onPress,
  variant = 'primary',
  icon,
  loading,
  disabled,
  small,
  style,
}: {
  title: string;
  onPress: () => void;
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger';
  icon?: IconName;
  loading?: boolean;
  disabled?: boolean;
  small?: boolean;
  style?: StyleProp<ViewStyle>;
}) {
  const v = buttonColors[variant];
  const inactive = disabled || loading;
  return (
    <Pressable
      onPress={onPress}
      disabled={inactive}
      accessibilityRole="button"
      accessibilityState={{ disabled: !!inactive, busy: !!loading }}
      style={({ pressed }) => [
        styles.button,
        small && styles.buttonSmall,
        { backgroundColor: v.bg, borderColor: v.border },
        pressed && styles.pressed,
        inactive && { opacity: 0.55 },
        style,
      ]}
    >
      {loading ? (
        <ActivityIndicator size="small" color={v.fg} />
      ) : (
        <>
          {icon ? <Ionicons name={icon} size={small ? 16 : 18} color={v.fg} /> : null}
          <Text style={{ color: v.fg, fontWeight: '600', fontSize: small ? 14 : 15 }}>{title}</Text>
        </>
      )}
    </Pressable>
  );
}

const buttonColors = {
  primary: { bg: colors.accent, border: colors.accent, fg: colors.text },
  secondary: { bg: colors.card, border: colors.accent, fg: colors.accentDark },
  ghost: { bg: 'transparent', border: 'transparent', fg: colors.accentDark },
  danger: { bg: colors.dangerBg, border: colors.dangerBg, fg: colors.danger },
};

export const toneColors: Record<Tone, { fg: string; bg: string }> = {
  neutral: { fg: colors.neutral, bg: colors.neutralBg },
  info: { fg: colors.info, bg: colors.infoBg },
  warning: { fg: colors.warning, bg: colors.warningBg },
  success: { fg: colors.success, bg: colors.successBg },
  danger: { fg: colors.danger, bg: colors.dangerBg },
};

export function Badge({ label, tone = 'neutral' }: { label: string; tone?: Tone }) {
  const t = toneColors[tone];
  return (
    <View style={[styles.badge, { backgroundColor: t.bg }]}>
      <Text style={{ color: t.fg, fontSize: 12, fontWeight: '600' }}>{label}</Text>
    </View>
  );
}

export function SectionHeader({
  title,
  action,
  onAction,
}: {
  title: string;
  action?: string;
  onAction?: () => void;
}) {
  return (
    <View style={styles.sectionHeader}>
      <AppText variant="h2">{title}</AppText>
      {action ? (
        <Pressable onPress={onAction} hitSlop={8} accessibilityRole="link">
          <Text style={{ color: colors.accentDark, fontWeight: '600', fontSize: 14 }}>{action} ›</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

export function MetaRow({ icon, children }: { icon: IconName; children: ReactNode }) {
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
      <Ionicons name={icon} size={14} color={colors.muted} />
      <AppText variant="small">{children}</AppText>
    </View>
  );
}

export function Divider() {
  return <View style={{ height: StyleSheet.hairlineWidth, backgroundColor: colors.border }} />;
}

/** Горизонтальные фильтры-вкладки (прокручиваются, если не помещаются). */
export function FilterTabs<K extends string>({
  items,
  value,
  onChange,
}: {
  items: { key: K; label: string; count?: number }[];
  value: K;
  onChange: (key: K) => void;
}) {
  return (
    <ScrollView
      horizontal
      showsHorizontalScrollIndicator={false}
      contentContainerStyle={{ gap: space.sm, paddingVertical: 2 }}
    >
      {items.map((it) => {
        const active = it.key === value;
        return (
          <Pressable
            key={it.key}
            onPress={() => onChange(it.key)}
            accessibilityRole="tab"
            accessibilityState={{ selected: active }}
            style={[styles.tab, active && styles.tabActive]}
          >
            <Text style={{ color: active ? colors.text : colors.heading, fontWeight: active ? '700' : '500', fontSize: 14 }}>
              {it.label}
            </Text>
            {it.count !== undefined ? (
              <Text style={{ color: active ? colors.text : colors.muted, fontSize: 12, fontWeight: '600' }}>{it.count}</Text>
            ) : null}
          </Pressable>
        );
      })}
    </ScrollView>
  );
}

/** Переключатель на 2–3 равных варианта. */
export function Segmented<K extends string>({
  items,
  value,
  onChange,
}: {
  items: { key: K; label: string }[];
  value: K;
  onChange: (key: K) => void;
}) {
  return (
    <View style={styles.segmented}>
      {items.map((it) => {
        const active = it.key === value;
        return (
          <Pressable
            key={it.key}
            onPress={() => onChange(it.key)}
            accessibilityRole="button"
            accessibilityState={{ selected: active }}
            style={[styles.segment, active && styles.segmentActive]}
          >
            <Text style={{ color: active ? colors.text : colors.heading, fontWeight: active ? '700' : '500', fontSize: 14 }}>
              {it.label}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.card,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border,
    padding: space.lg,
    ...shadow,
  },
  pressed: { opacity: 0.85 },
  button: {
    minHeight: 48,
    borderRadius: radius.md,
    borderWidth: 1,
    paddingHorizontal: space.lg,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
  },
  buttonSmall: { minHeight: 38, paddingHorizontal: space.md, borderRadius: radius.sm },
  badge: { alignSelf: 'flex-start', paddingHorizontal: 10, paddingVertical: 4, borderRadius: radius.pill },
  sectionHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  tab: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingHorizontal: 14,
    height: 38,
    borderRadius: radius.pill,
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
  },
  tabActive: { backgroundColor: colors.accentSoft, borderColor: colors.accent },
  segmented: {
    flexDirection: 'row',
    backgroundColor: colors.card,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    padding: 3,
  },
  segment: { flex: 1, height: 40, alignItems: 'center', justifyContent: 'center', borderRadius: radius.sm },
  segmentActive: { backgroundColor: colors.accentSoft },
});
