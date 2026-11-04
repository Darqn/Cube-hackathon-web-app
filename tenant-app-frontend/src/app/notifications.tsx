import { View } from 'react-native';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api';
import { errorMessage } from '@/api/errors';
import type { AppNotification } from '@/api/types';
import { fmtDateTime } from '@/lib/format';
import { useNav } from '@/lib/nav';
import { colors, space } from '@/theme';
import { Async, EmptyState, useToast } from '@/ui/feedback';
import { BackBar, Screen } from '@/ui/layout';
import { AppText, Button, Card, IconCircle } from '@/ui/primitives';

export default function Notifications() {
  const nav = useNav();
  const toast = useToast();
  const qc = useQueryClient();
  const query = useQuery({ queryKey: ['notifications'], queryFn: api.notifications });
  const refresh = () => void qc.invalidateQueries({ queryKey: ['notifications'] });

  const readAll = useMutation({
    mutationFn: api.readAllNotifications,
    onSuccess: refresh,
    onError: (e) => toast(errorMessage(e), 'error'),
  });
  const readOne = useMutation({ mutationFn: (id: string) => api.readNotification(id), onSuccess: refresh });

  const open = (n: AppNotification) => {
    if (!n.read) readOne.mutate(n.id);
    if (n.link) nav.push(n.link);
  };

  const hasUnread = query.data?.some((n) => !n.read);

  return (
    <Screen
      header={
        <BackBar
          title="Уведомления"
          right={hasUnread ? <Button small variant="ghost" title="Прочитать все" loading={readAll.isPending} onPress={() => readAll.mutate()} /> : undefined}
        />
      }
      refreshing={query.isRefetching}
      onRefresh={() => void query.refetch()}
    >
      <Async
        query={query}
        isEmpty={(d) => d.length === 0}
        empty={<EmptyState icon="notifications-off-outline" title="Уведомлений нет" text="Здесь появятся сообщения о счетах, обращениях и документах." />}
      >
        {(items) => (
          <View style={{ gap: space.md }}>
            {items.map((n) => (
              <Card key={n.id} onPress={() => open(n)} style={{ flexDirection: 'row', gap: space.md, alignItems: 'center', backgroundColor: n.read ? colors.card : colors.accentSoft }}>
                <IconCircle name={n.read ? 'notifications-outline' : 'notifications'} size={40} />
                <View style={{ flex: 1, gap: 2 }}>
                  <AppText variant="h3">{n.title}</AppText>
                  <AppText variant="small">{n.text}</AppText>
                  <AppText variant="caption">{fmtDateTime(n.createdAt)}</AppText>
                </View>
              </Card>
            ))}
          </View>
        )}
      </Async>
    </Screen>
  );
}
