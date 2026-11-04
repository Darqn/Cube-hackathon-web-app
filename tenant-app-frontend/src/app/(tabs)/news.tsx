import { useState } from 'react';
import { View } from 'react-native';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api';
import { errorMessage } from '@/api/errors';
import type { NewsCategory } from '@/api/types';
import { NEWS_CATEGORY } from '@/lib/config';
import { fmtDate, fmtTime } from '@/lib/format';
import { useNav } from '@/lib/nav';
import { colors, space } from '@/theme';
import { useNews } from '@/features/hooks';
import { Async, EmptyState, useToast } from '@/ui/feedback';
import { BrandBar, PageTitle, Screen } from '@/ui/layout';
import { AppText, Badge, Button, Card, FilterTabs, IconCircle, MetaRow } from '@/ui/primitives';

type Filter = 'all' | NewsCategory;

export default function News() {
  const nav = useNav();
  const toast = useToast();
  const qc = useQueryClient();
  const query = useNews();
  const [filter, setFilter] = useState<Filter>('all');

  const refresh = () => void qc.invalidateQueries({ queryKey: ['news'] });
  const readAll = useMutation({
    mutationFn: api.readAllNews,
    onSuccess: () => {
      toast('Все новости отмечены прочитанными');
      refresh();
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  });
  const acknowledge = useMutation({
    mutationFn: (id: string) => api.acknowledgeNews(id),
    onSuccess: () => {
      toast('Спасибо, отметили, что вы ознакомлены');
      refresh();
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  });

  return (
    <Screen tabs header={<BrandBar />} refreshing={query.isRefetching} onRefresh={() => void query.refetch()}>
      <PageTitle
        title="Новости и оповещения"
        subtitle="Важные события на территории"
        action={<Button small variant="secondary" icon="checkmark-done-outline" title="Прочитано" loading={readAll.isPending} onPress={() => readAll.mutate()} />}
      />

      <Async
        query={query}
        isEmpty={(d) => d.length === 0}
        empty={<EmptyState icon="megaphone-outline" title="Новостей пока нет" text="Объявления и оповещения появятся здесь." />}
      >
        {(items) => {
          const sorted = [...items].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
          const urgent = sorted.find((n) => (n.requiresAcknowledgement ?? n.urgent) && !n.acknowledged);
          const list = filter === 'all' ? sorted : sorted.filter((n) => n.category === filter);
          const count = (c: NewsCategory) => items.filter((n) => n.category === c).length;
          return (
            <View style={{ gap: space.lg }}>
              {urgent ? (
                <Card style={{ borderColor: colors.danger, backgroundColor: colors.dangerBg, gap: space.sm }}>
                  <View style={{ flexDirection: 'row', gap: space.md }}>
                    <IconCircle name="warning-outline" tone="danger" />
                    <View style={{ flex: 1, gap: 4 }}>
                      <AppText variant="small" color={colors.danger} style={{ fontWeight: '700' }}>Срочное уведомление</AppText>
                      <AppText variant="h3">{urgent.title}</AppText>
                      <AppText variant="small" numberOfLines={3}>{urgent.body}</AppText>
                    </View>
                  </View>
                  <View style={{ flexDirection: 'row', gap: space.sm }}>
                    <Button small variant="secondary" title="Подробнее" onPress={() => nav.push(`/news/${urgent.id}`)} style={{ flex: 1 }} />
                    <Button small icon="checkmark" title="Ознакомлен" loading={acknowledge.isPending} onPress={() => acknowledge.mutate(urgent.id)} style={{ flex: 1 }} />
                  </View>
                </Card>
              ) : null}

              <FilterTabs
                value={filter}
                onChange={setFilter}
                items={[
                  { key: 'all', label: 'Все', count: items.length },
                  { key: 'accident', label: 'Аварии', count: count('accident') },
                  { key: 'planned', label: 'Плановые работы', count: count('planned') },
                  { key: 'announcement', label: 'Объявления', count: count('announcement') },
                  { key: 'other', label: 'Прочие', count: count('other') },
                ]}
              />

              {list.length === 0 ? (
                <EmptyState icon="megaphone-outline" title="В этой категории пусто" />
              ) : (
                list.map((n) => {
                  const cat = NEWS_CATEGORY[n.category];
                  return (
                    <Card key={n.id} onPress={() => nav.push(`/news/${n.id}`)} style={{ flexDirection: 'row', gap: space.md }}>
                      <IconCircle name={cat.icon} tone={cat.tone} size={44} />
                      <View style={{ flex: 1, gap: 6 }}>
                        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                          <AppText variant="h3" style={{ flex: 1 }}>{n.title}</AppText>
                          {!n.read ? <View accessibilityLabel="Не прочитано" style={{ width: 10, height: 10, borderRadius: 5, backgroundColor: colors.accent }} /> : null}
                        </View>
                        <Badge label={cat.label} tone={cat.tone} />
                        <AppText variant="small" numberOfLines={2}>{n.body}</AppText>
                        <MetaRow icon="calendar-outline">{fmtDate(n.createdAt)}, {fmtTime(n.createdAt)}</MetaRow>
                      </View>
                    </Card>
                  );
                })
              )}
            </View>
          );
        }}
      </Async>
    </Screen>
  );
}
