import { useState } from 'react';
import { View } from 'react-native';
import { useForm } from 'react-hook-form';
import { useAuth } from '@/auth/AuthProvider';
import { errorMessage } from '@/api/errors';
import { USE_MOCK } from '@/api/http';
import { useNav } from '@/lib/nav';
import { colors, radius, space } from '@/theme';
import { Screen } from '@/ui/layout';
import { AppText, Button, Card, Icon } from '@/ui/primitives';
import { FormInput } from '@/ui/form';

type Form = { login: string; password: string };

export default function Login() {
  const nav = useNav();
  const { signIn } = useAuth();
  const [serverError, setServerError] = useState<string | null>(null);
  const [showPassword, setShowPassword] = useState(false);
  const { control, handleSubmit, formState } = useForm<Form>({ defaultValues: { login: '', password: '' } });

  const submit = handleSubmit(async ({ login, password }) => {
    setServerError(null);
    try {
      await signIn(login.trim(), password);
    } catch (e) {
      setServerError(errorMessage(e));
    }
  });

  return (
    <Screen>
      <View style={{ alignItems: 'center', gap: space.md, paddingTop: space.xl }}>
        <View
          style={{
            width: 84,
            height: 84,
            borderRadius: 42,
            borderWidth: 2,
            borderColor: colors.accent,
            backgroundColor: colors.accentSoft,
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <Icon name="cog" size={44} color={colors.accent} />
        </View>
        <AppText variant="title" style={{ textAlign: 'center' }}>АО «Дальреммаш»</AppText>
        <AppText variant="small">Личный кабинет арендатора</AppText>
      </View>

      <Card style={{ gap: space.lg }}>
        <AppText variant="h2">Вход</AppText>
        <FormInput
          control={control}
          name="login"
          label="Логин"
          autoCapitalize="none"
          autoCorrect={false}
          textContentType="username"
          rules={{ required: 'Введите логин' }}
        />
        <FormInput
          control={control}
          name="password"
          label="Пароль"
          secureTextEntry={!showPassword}
          autoCapitalize="none"
          textContentType="password"
          rules={{ required: 'Введите пароль' }}
        />
        <Button
          variant="ghost"
          small
          icon={showPassword ? 'eye-off-outline' : 'eye-outline'}
          title={showPassword ? 'Скрыть пароль' : 'Показать пароль'}
          onPress={() => setShowPassword((v) => !v)}
          style={{ alignSelf: 'flex-start' }}
        />

        {serverError ? (
          <View
            accessibilityLiveRegion="polite"
            style={{ backgroundColor: colors.dangerBg, borderRadius: radius.sm, padding: space.md }}
          >
            <AppText variant="small" color={colors.danger}>{serverError}</AppText>
          </View>
        ) : null}

        <Button title="Войти" loading={formState.isSubmitting} onPress={submit} />
        <Button variant="ghost" title="Забыли пароль?" onPress={() => nav.push('/forgot')} />
      </Card>

      <Card style={{ gap: space.sm }}>
        <AppText variant="h3">Ещё нет доступа?</AppText>
        <AppText variant="small">Отправьте заявку. Администратор проверит данные и подтвердит доступ.</AppText>
        <Button variant="secondary" title="Заявка на регистрацию" onPress={() => nav.push('/register')} />
      </Card>

      {USE_MOCK ? (
        <AppText variant="caption" style={{ textAlign: 'center' }}>
          Тестовый режим без сервера. Логин: demo, пароль: demo1234
        </AppText>
      ) : null}
    </Screen>
  );
}
