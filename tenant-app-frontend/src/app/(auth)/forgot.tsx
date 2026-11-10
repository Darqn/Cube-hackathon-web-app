import { useState } from 'react';
import { View } from 'react-native';
import { useForm } from 'react-hook-form';
import { api } from '@/api';
import { errorMessage } from '@/api/errors';
import { useNav } from '@/lib/nav';
import { colors, radius, space } from '@/theme';
import { AuthScreen } from '@/ui/AuthScreen';
import { AppText, Button, Card, IconCircle } from '@/ui/primitives';
import { FormInput } from '@/ui/form';

type Form = { contact: string };

export default function Forgot() {
  const nav = useNav();
  const [sent, setSent] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);
  const { control, handleSubmit, formState } = useForm<Form>({ defaultValues: { contact: '' } });

  const submit = handleSubmit(async ({ contact }) => {
    setServerError(null);
    try {
      await api.forgotPassword(contact.trim());
      setSent(true);
    } catch (e) {
      setServerError(errorMessage(e));
    }
  });

  return (
    <AuthScreen title="Восстановление пароля">
      {sent ? (
        <Card style={{ alignItems: 'center', gap: space.md }}>
          <IconCircle name="mail-outline" size={64} tone="success" />
          <AppText variant="h2" style={{ textAlign: 'center' }}>Запрос принят</AppText>
          <AppText variant="small" style={{ textAlign: 'center' }}>
            Автоматическая отправка писем пока не подключена. Обратитесь к администратору для восстановления доступа.
          </AppText>
          <Button title="Вернуться ко входу" onPress={nav.back} style={{ alignSelf: 'stretch' }} />
        </Card>
      ) : (
        <Card style={{ gap: space.lg }}>
          <AppText variant="small">Введите email, указанные при регистрации.</AppText>
          <FormInput control={control} name="contact" label="Email" autoCapitalize="none" autoCorrect={false}
            keyboardType="email-address" rules={{ required: 'Введите email', pattern: { value: /^\S+@\S+\.\S+$/, message: 'Введите корректный email' } }} />
          {serverError ? (
            <View style={{ backgroundColor: colors.dangerBg, borderRadius: radius.sm, padding: space.md }}>
              <AppText variant="small" color={colors.danger}>{serverError}</AppText>
            </View>
          ) : null}
          <Button title="Отправить" loading={formState.isSubmitting} onPress={submit} />
        </Card>
      )}
          <Button variant="ghost" title="Вернуться ко входу" onPress={() => nav.replace('/login')} />
    </AuthScreen>
  );
}
