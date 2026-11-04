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

type Form = { login: string; password: string; fullName: string; organization: string; inn: string; email: string; phone: string };

export default function Register() {
  const nav = useNav();
  const toast = useToast();
  const [sent, setSent] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);
  const { control, handleSubmit, formState } = useForm<Form>({
    defaultValues: { login: '', password: '', fullName: '', organization: '', inn: '', email: '', phone: '' },
  });

  const submit = handleSubmit(async (v) => {
    setServerError(null);
    try {
      await api.register({
        login: v.login.trim(),
        password: v.password,
        fullName: v.fullName.trim(),
        organization: v.organization.trim(),
        inn: v.inn.trim(),
        email: v.email.trim(),
        phone: v.phone.trim(),
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
            Администратор проверит данные организации и подтвердит доступ. После подтверждения войдите с выбранным логином и паролем.
          </AppText>
          <Button title="Вернуться ко входу" onPress={nav.back} style={{ alignSelf: 'stretch' }} />
        </Card>
      ) : (
        <>
          <AppText variant="small">
            Укажите данные организации. Выберите логин и пароль. Доступ появится после одобрения администратором.
          </AppText>
          <Card style={{ gap: space.lg }}>
            <FormInput control={control} name="login" label="Логин" autoCapitalize="none" autoCorrect={false}
              rules={{ required: 'Введите логин', pattern: { value: /^[a-zA-Z0-9._@+\-]+$/, message: 'Используйте латинские буквы, цифры и . _ @ + -' } }} />
            <FormInput control={control} name="password" label="Пароль" secureTextEntry autoCapitalize="none"
              hint="От 12 символов: заглавная и строчная латинские буквы, цифра и спецсимвол"
              rules={{ required: 'Введите пароль', minLength: { value: 12, message: 'Не менее 12 символов' }, validate: v => /[a-z]/.test(v) && /[A-Z]/.test(v) && /[0-9]/.test(v) && /[^a-zA-Z0-9]/.test(v) || 'Добавьте буквы обоих регистров, цифру и спецсимвол' }} />
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
            <FormInput control={control} name="phone" label="Телефон" rules={{ required: 'Введите телефон' }} keyboardType="phone-pad" textContentType="telephoneNumber" />
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
