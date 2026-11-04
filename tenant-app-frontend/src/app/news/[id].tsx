import { useEffect } from 'react';
import { View } from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api';
import { errorMessage } from '@/api/errors';
import type { NewsItem } from '@/api/types';
import { NEWS_CATEGORY } from '@/lib/config';
import { fmtDate, fmtTime } from '@/lib/format';
import { space } from '@/theme';
import { Async, useToast } from '@/ui/feedback';
import { BackBar, Screen } from '@/ui/layout';
import { AppText, Badge, Button, Card, IconCircle } from '@/ui/primitives';

export default function NewsDetails() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const qc = useQueryClient();
  const query = useQuery({ queryKey: ['news-item', id], queryFn: () => api.newsItem(id) });

  // открытая новость считается прочитанной — обновляем список и счётчики
  useEffect(() => {
    if (query.data) void qc.invalidateQueries({ queryKey: ['news'] });
  }, [query.data, qc]);

  return (
    <Screen header={<BackBar title="Новость" />}>
      <Async query={query}>{(n) => <Details item={n} />}</Async>
    </Screen>
  );
}

function Details({ item }: { item: NewsItem }) {
  const toast = useToast();
  const qc = useQueryClient();
  const cat = NEWS_CATEGORY[item.category];
  const ack = useMutation({
    mutationFn: () => api.acknowledgeNews(item.id),
    onSuccess: () => {
      toast('Спасибо, отметили, что вы ознакомлены');
      void qc.invalidateQueries({ queryKey: ['news'] });
      void qc.invalidateQueries({ queryKey: ['news-item', item.id] });
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  });
  return (
    <Card style={{ gap: space.md }}>
      <View style={{ flexDirection: 'row', gap: space.md, alignItems: 'center' }}>
        <IconCircle name={cat.icon} tone={cat.tone} size={48} />
        <View style={{ flex: 1, gap: 4 }}>
          <Badge label={cat.label} tone={cat.tone} />
          <AppText variant="small">{fmtDate(item.createdAt)}, {fmtTime(item.createdAt)}</AppText>
        </View>
      </View>
      <AppText variant="h2">{item.title}</AppText>
      <AppText>{item.body}</AppText>
      {item.urgent && !item.acknowledged ? (
        <Button icon="checkmark" title="Ознакомлен" loading={ack.isPending} onPress={() => ack.mutate()} />
      ) : null}
      {item.acknowledged ? <Badge label="Вы ознакомлены" tone="success" /> : null}
    </Card>
  );
}
