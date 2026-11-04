import { useState } from 'react';
import { View } from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api';
import { errorMessage } from '@/api/errors';
import type { LocalFile, Profile, RequestCategory } from '@/api/types';
import { REQUEST_CATEGORIES, categoryByKey } from '@/lib/config';
import { useNav } from '@/lib/nav';
import { colors, radius, space } from '@/theme';
import { useMyPremises, useProfile } from '@/features/hooks';
import { Async, useToast } from '@/ui/feedback';
import { AttachmentPicker, FormInput, SelectField } from '@/ui/form';
import { BackBar, Screen } from '@/ui/layout';
import { AppText, Button, Card } from '@/ui/primitives';

type Form = {
  category: RequestCategory;
  premiseId: string;
  subject: string;
  description: string;
  contactName: string;
  phone: string;
  email: string;
};

export default function NewRequest() {
  const profile = useProfile();
  return (
    <Screen header={<BackBar title="Новое обращение" />}>
      <Async query={profile}>{(p) => <RequestForm profile={p} />}</Async>
    </Screen>
  );
}

function RequestForm({ profile }: { profile: Profile }) {
  const params = useLocalSearchParams<{ category?: string; premiseId?: string; subject?: string; description?: string }>();
  const nav = useNav();
  const toast = useToast();
  const qc = useQueryClient();
  const premises = useMyPremises();
  const [files, setFiles] = useState<LocalFile[]>([]);
  const [serverError, setServerError] = useState<string | null>(null);

  const initialCategory = REQUEST_CATEGORIES.some((c) => c.key === params.category)
    ? (params.category as RequestCategory)
    : 'rent';

  const { control, handleSubmit } = useForm<Form>({
    defaultValues: {
      category: initialCategory,
      premiseId: params.premiseId ?? '',
      subject: params.subject ?? '',
      description: params.description ?? '',
      contactName: profile.contactName,
      phone: profile.phone,
      email: profile.email,
    },
  });

  const selectedCategory = useWatch({ control, name: 'category' });

  const send = useMutation({
    mutationFn: (v: Form) =>
      api.createRequest({
        category: v.category,
        subject: v.subject.trim(),
        description: v.description.trim(),
        premiseId: v.premiseId || null,
        contactName: v.contactName.trim(),
        phone: v.phone.trim(),
        email: v.email.trim(),
        files,
      }),
    onSuccess: (created) => {
      void qc.invalidateQueries({ queryKey: ['requests'] });
      toast(`Обращение №${created.number} отправлено ответственному сотруднику`);
      nav.replace(`/requests/${created.id}`);
    },
    onError: (e) => {
      setServerError(errorMessage(e));
      toast(errorMessage(e), 'error');
    },
  });

  const submit = handleSubmit((v) => {
    setServerError(null);
    send.mutate(v);
  });

  return (
    <View style={{ gap: space.lg }}>
      <Card style={{ gap: space.lg }}>
        <Controller
          control={control}
          name="category"
          rules={{ required: true }}
          render={({ field }) => (
            <SelectField
              label="Тип обращения"
              value={field.value}
              onChange={(v) => field.onChange(v)}
              options={REQUEST_CATEGORIES.map((c) => ({ value: c.key, label: c.title, hint: `Получатель: ${c.assignee}` }))}
            />
          )}
        />
        <Controller
          control={control}
          name="premiseId"
          render={({ field }) => (
            <SelectField
              label="Помещение"
              placeholder="Не связано с помещением"
              value={field.value}
              onChange={(v) => field.onChange(v)}
              options={[
                { value: '', label: 'Не связано с помещением' },
                ...(premises.data ?? []).map((p) => ({ value: p.id, label: p.name, hint: p.building })),
              ]}
            />
          )}
        />
        <FormInput control={control} name="subject" label="Тема" rules={{ required: 'Укажите тему обращения' }} />
        <FormInput
          control={control}
          name="description"
          label="Описание"
          multiline
          rules={{ required: 'Опишите вопрос или проблему', minLength: { value: 10, message: 'Опишите подробнее, минимум 10 символов' } }}
        />
        <AttachmentPicker files={files} onChange={setFiles} label="Файлы и фото" />
      </Card>

      <Card style={{ gap: space.lg }}>
        <AppText variant="h3">Контактное лицо</AppText>
        <FormInput control={control} name="contactName" label="Контактное лицо" rules={{ required: 'Укажите контактное лицо' }} />
        <FormInput control={control} name="phone" label="Телефон" keyboardType="phone-pad" rules={{ required: 'Укажите телефон' }} />
        <FormInput
          control={control}
          name="email"
          label="Email"
          keyboardType="email-address"
          autoCapitalize="none"
          rules={{ required: 'Укажите email', pattern: { value: /^\S+@\S+\.\S+$/, message: 'Введите корректный email' } }}
        />
      </Card>

      <AppText variant="caption">
        Обращение получит: {categoryByKey(selectedCategory).assignee}
      </AppText>

      {serverError ? (
        <View style={{ backgroundColor: colors.dangerBg, borderRadius: radius.sm, padding: space.md }}>
          <AppText variant="small" color={colors.danger}>{serverError}</AppText>
        </View>
      ) : null}
      <Button title="Отправить" icon="send" loading={send.isPending} onPress={submit} />
    </View>
  );
}
