import { Alert, View } from 'react-native';
import { useAuth } from '@/auth/AuthProvider';
import { space } from '@/theme';
import { useProfile } from '@/features/hooks';
import { Async } from '@/ui/feedback';
import { BackBar, Screen } from '@/ui/layout';
import { AppText, Button, Card, Divider } from '@/ui/primitives';

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View style={{ gap: 2 }}>
      <AppText variant="caption">{label}</AppText>
      <AppText>{value}</AppText>
    </View>
  );
}

export default function Profile() {
  const { signOut } = useAuth();
  const query = useProfile();

  const confirmSignOut = () =>
    Alert.alert('Выйти из кабинета?', 'Чтобы войти снова, понадобятся логин и пароль.', [
      { text: 'Отмена', style: 'cancel' },
      { text: 'Выйти', style: 'destructive', onPress: () => void signOut() },
    ]);

  return (
    <Screen header={<BackBar title="Профиль" />}>
      <Async query={query}>
        {(p) => (
          <Card style={{ gap: space.md }}>
            <AppText variant="h2">{p.orgName}</AppText>
            <Divider />
            <Row label="ИНН" value={p.inn} />
            <Row label="Контактное лицо" value={p.contactName} />
            <Row label="Телефон" value={p.phone} />
            <Row label="Email" value={p.email} />
            <Row label="Логин" value={p.login} />
          </Card>
        )}
      </Async>
      <Button variant="danger" icon="log-out-outline" title="Выйти" onPress={confirmSignOut} />
    </Screen>
  );
}
