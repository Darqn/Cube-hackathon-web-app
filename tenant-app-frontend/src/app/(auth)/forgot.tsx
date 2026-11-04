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
    <Screen header={<BackBar title="Восстановление пароля" />}>
      {sent ? (
        <Card style={{ alignItems: 'center', gap: space.md }}>
          <IconCircle name="mail-outline" size={64} tone="success" />
          <AppText variant="h2" style={{ textAlign: 'center' }}>Инструкция отправлена</AppText>
          <AppText variant="small" style={{ textAlign: 'center' }}>
            Если такой пользователь есть, мы отправили ссылку для смены пароля. Проверьте почту или сообщения.
          </AppText>
          <Button title="Вернуться ко входу" onPress={nav.back} style={{ alignSelf: 'stretch' }} />
        </Card>
      ) : (
        <Card style={{ gap: space.lg }}>
          <AppText variant="small">Введите email или телефон, указанные при регистрации.</AppText>
          <FormInput control={control} name="contact" label="Email или телефон" autoCapitalize="none" autoCorrect={false}
            rules={{ required: 'Введите email или телефон' }} />
          {serverError ? (
            <View style={{ backgroundColor: colors.dangerBg, borderRadius: radius.sm, padding: space.md }}>
              <AppText variant="small" color={colors.danger}>{serverError}</AppText>
            </View>
          ) : null}
          <Button title="Отправить" loading={formState.isSubmitting} onPress={submit} />
        </Card>
      )}
    </Screen>
  );
}
