import { useState } from 'react';
import { View } from 'react-native';
import { useForm } from 'react-hook-form';
import { api } from '@/api';
import { errorMessage } from '@/api/errors';
import { useNav } from '@/lib/nav';
import { colors, radius, space } from '@/theme';
import { BackBar, Screen } from '@/ui/layout';
import { AppText, Button, Card, IconCircle } from '@/ui/primitives';
import { FormInput } from '@/ui/form';
import { useToast } from '@/ui/feedback';

type Form = { fullName: string; organization: string; inn: string; email: string; phone: string };

export default function Register() {
  const nav = useNav();
  const toast = useToast();
  const [sent, setSent] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);
  const { control, handleSubmit, formState } = useForm<Form>({
    defaultValues: { fullName: '', organization: '', inn: '', email: '', phone: '' },
  });

  const submit = handleSubmit(async (v) => {
    setServerError(null);
    try {
      await api.register({
        fullName: v.fullName.trim(),
        organization: v.organization.trim(),
        inn: v.inn.trim(),
        email: v.email.trim(),
        phone: v.phone.trim() || undefined,
      });
      setSent(true);
      toast('Заявка на регистрацию отправлена');
    } catch (e) {
      setServerError(errorMessage(e));
    }
  });

  return (
    <Screen header={<BackBar title="Заявка на регистрацию" />}>
      {sent ? (
        <Card style={{ alignItems: 'center', gap: space.md }}>
          <IconCircle name="checkmark-circle-outline" size={64} tone="success" />
          <AppText variant="h2" style={{ textAlign: 'center' }}>Заявка отправлена</AppText>
          <AppText variant="small" style={{ textAlign: 'center' }}>
            Администратор проверит данные организации и подтвердит доступ. Ответ придёт на указанный email.
          </AppText>
          <Button title="Вернуться ко входу" onPress={nav.back} style={{ alignSelf: 'stretch' }} />
        </Card>
      ) : (
        <>
          <AppText variant="small">
            Укажите данные организации. После проверки администратор создаст доступ и пришлёт логин.
          </AppText>
          <Card style={{ gap: space.lg }}>
            <FormInput control={control} name="fullName" label="ФИО" autoCapitalize="words" textContentType="name"
              rules={{ required: 'Введите ФИО', minLength: { value: 5, message: 'Введите ФИО полностью' } }} />
            <FormInput control={control} name="organization" label="Организация"
              rules={{ required: 'Введите название организации' }} />
            <FormInput control={control} name="inn" label="ИНН" keyboardType="number-pad" maxLength={12}
              hint="10 цифр для организации, 12 для ИП"
              rules={{ required: 'Введите ИНН', pattern: { value: /^(\d{10}|\d{12})$/, message: 'ИНН состоит из 10 или 12 цифр' } }} />
            <FormInput control={control} name="email" label="Email" keyboardType="email-address" autoCapitalize="none"
              autoCorrect={false} textContentType="emailAddress"
              rules={{ required: 'Введите email', pattern: { value: /^\S+@\S+\.\S+$/, message: 'Введите корректный email' } }} />
            <FormInput control={control} name="phone" label="Телефон (необязательно)" keyboardType="phone-pad" textContentType="telephoneNumber" />
            {serverError ? (
              <View style={{ backgroundColor: colors.dangerBg, borderRadius: radius.sm, padding: space.md }}>
                <AppText variant="small" color={colors.danger}>{serverError}</AppText>
              </View>
            ) : null}
            <Button title="Отправить заявку" loading={formState.isSubmitting} onPress={submit} />
          </Card>
        </>
      )}
    </Screen>
  );
}
