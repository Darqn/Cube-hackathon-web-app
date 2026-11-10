import { useState } from 'react';
import { FlatList, Modal, Pressable, StyleSheet, Text, TextInput, View, type TextInputProps } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useController, type Control, type FieldValues, type Path, type UseControllerProps } from 'react-hook-form';
import { colors, radius, space } from '@/theme';
import type { LocalFile } from '@/api/types';
import { errorMessage } from '@/api/errors';
import { pickDocuments, pickImages, takePhoto } from '@/lib/files';
import { AppText, Button, Icon } from './primitives';
import { useToast } from './feedback';

export function TextField({
  label,
  error,
  hint,
  multiline,
  style,
  ...props
}: { label: string; error?: string; hint?: string } & TextInputProps) {
  const [focused, setFocused] = useState(false);
  return (
    <View style={{ gap: 6 }}>
      <AppText variant="small" color={colors.heading} style={{ fontWeight: '600' }}>
        {label}
      </AppText>
      <TextInput
        {...props}
        multiline={multiline}
        accessibilityLabel={label}
        placeholderTextColor={colors.inactive}
        onFocus={(e) => {
          setFocused(true);
          props.onFocus?.(e);
        }}
        onBlur={(e) => {
          setFocused(false);
          props.onBlur?.(e);
        }}
        style={[
          styles.input,
          multiline && { minHeight: 110, textAlignVertical: 'top', paddingTop: 12 },
          focused && { borderColor: colors.accent },
          !!error && { borderColor: colors.danger },
          style,
        ]}
      />
      {error ? (
        <AppText variant="caption" color={colors.danger}>
          {error}
        </AppText>
      ) : hint ? (
        <AppText variant="caption">{hint}</AppText>
      ) : null}
    </View>
  );
}

/** Поле, подключённое к react-hook-form. */
export function FormInput<T extends FieldValues>({
  control,
  name,
  rules,
  label,
  hint,
  ...props
}: {
  control: Control<T>;
  name: Path<T>;
  rules?: UseControllerProps<T>['rules'];
  label: string;
  hint?: string;
} & Omit<TextInputProps, 'value' | 'onChangeText'>) {
  const { field, fieldState } = useController({ control, name, rules });
  return (
    <TextField
      {...props}
      label={label}
      hint={hint}
      value={String(field.value ?? '')}
      onChangeText={field.onChange}
      onBlur={field.onBlur}
      error={fieldState.error?.message}
    />
  );
}

export type Option = { value: string; label: string; hint?: string };

/** Выбор одного значения из списка (открывается снизу). */
export function SelectField({
  label,
  value,
  options,
  onChange,
  placeholder = 'Выберите',
  error,
}: {
  label: string;
  value: string | null | undefined;
  options: Option[];
  onChange: (value: string) => void;
  placeholder?: string;
  error?: string;
}) {
  const [open, setOpen] = useState(false);
  const insets = useSafeAreaInsets();
  const current = options.find((o) => o.value === value);
  return (
    <View style={{ gap: 6 }}>
      <AppText variant="small" color={colors.heading} style={{ fontWeight: '600' }}>
        {label}
      </AppText>
      <Pressable
        onPress={() => setOpen(true)}
        accessibilityRole="button"
        accessibilityLabel={label}
        style={[styles.input, styles.select, !!error && { borderColor: colors.danger }]}
      >
        <Text style={{ flex: 1, fontSize: 15, color: current ? colors.text : colors.inactive }} numberOfLines={1}>
          {current?.label ?? placeholder}
        </Text>
        <Icon name="chevron-down" size={18} color={colors.muted} />
      </Pressable>
      {error ? (
        <AppText variant="caption" color={colors.danger}>
          {error}
        </AppText>
      ) : null}
      <Modal visible={open} transparent animationType="slide" onRequestClose={() => setOpen(false)}>
        <Pressable style={styles.backdrop} onPress={() => setOpen(false)} />
        <View style={[styles.sheet, { paddingBottom: insets.bottom + space.md }]}>
          <AppText variant="h3" style={{ padding: space.lg }}>
            {label}
          </AppText>
          <FlatList
            data={options}
            keyExtractor={(o) => o.value}
            renderItem={({ item }) => (
              <Pressable
                onPress={() => {
                  onChange(item.value);
                  setOpen(false);
                }}
                accessibilityRole="button"
                accessibilityLabel={item.label}
                style={styles.option}
              >
                <View style={{ flex: 1 }}>
                  <Text style={{ fontSize: 15, color: colors.text }}>{item.label}</Text>
                  {item.hint ? <AppText variant="caption">{item.hint}</AppText> : null}
                </View>
                {item.value === value ? <Icon name="checkmark" size={20} /> : null}
              </Pressable>
            )}
          />
        </View>
      </Modal>
    </View>
  );
}

/** Выбор файлов: фото с камеры, из галереи или любой документ. */
export function AttachmentPicker({
  files,
  onChange,
  max = 5,
  label = 'Вложения',
}: {
  files: LocalFile[];
  onChange: (files: LocalFile[]) => void;
  max?: number;
  label?: string;
}) {
  const toast = useToast();
  const add = async (picker: () => Promise<LocalFile[] | LocalFile | null>) => {
    try {
      const res = await picker();
      const list = Array.isArray(res) ? res : res ? [res] : [];
      if (list.length) onChange([...files, ...list].slice(0, max));
    } catch (e) {
      toast(errorMessage(e), 'error');
    }
  };
  const full = files.length >= max;
  return (
    <View style={{ gap: 8 }}>
      <AppText variant="small" color={colors.heading} style={{ fontWeight: '600' }}>
        {label}
      </AppText>
      <View style={{ flexDirection: 'row', gap: 8, flexWrap: 'wrap' }}>
        <Button small variant="secondary" icon="camera-outline" title="Снять" disabled={full} onPress={() => add(takePhoto)} />
        <Button small variant="secondary" icon="image-outline" title="Галерея" disabled={full} onPress={() => add(() => pickImages())} />
        <Button small variant="secondary" icon="attach-outline" title="Файл" disabled={full} onPress={() => add(() => pickDocuments())} />
      </View>
      {files.map((f, i) => (
        <View key={`${f.uri}-${i}`} style={styles.file}>
          <Icon name="document-attach-outline" size={18} color={colors.muted} />
          <Text style={{ flex: 1, color: colors.text, fontSize: 14 }} numberOfLines={1}>
            {f.name}
          </Text>
          <Pressable onPress={() => onChange(files.filter((_, idx) => idx !== i))} hitSlop={8} accessibilityLabel={`Убрать ${f.name}`}>
            <Icon name="close-circle" size={20} color={colors.inactive} />
          </Pressable>
        </View>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  input: {
    minHeight: 48,
    borderBottomWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.bg,
    borderRadius: radius.md,
    paddingHorizontal: space.md,
    fontSize: 15,
    color: colors.text,
  },
  select: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  backdrop: { flex: 1, backgroundColor: 'rgba(34,37,42,0.45)' },
  sheet: {
    maxHeight: '70%',
    backgroundColor: colors.card,
    borderTopLeftRadius: radius.lg,
    borderTopRightRadius: radius.lg,
  },
  option: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: space.lg,
    paddingVertical: 14,
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: colors.border,
  },
  file: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    padding: space.sm,
    paddingHorizontal: space.md,
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
  },
});
