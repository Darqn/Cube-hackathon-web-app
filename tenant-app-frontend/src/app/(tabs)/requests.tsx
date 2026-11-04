import { useState } from 'react';
import { View } from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/api';
import type { RequestItem } from '@/api/types';
import { REQUEST_CATEGORIES, REQUEST_FILTERS, REQUEST_STATUS, categoryByKey } from '@/lib/config';
import { fmtDate } from '@/lib/format';
import { useNav } from '@/lib/nav';
import { colors, space } from '@/theme';
import { Async, EmptyState } from '@/ui/feedback';
import { BrandBar, PageTitle, Screen } from '@/ui/layout';
import { AppText, Badge, Button, Card, Divider, FilterTabs, IconCircle, MetaRow, SectionHeader } from '@/ui/primitives';

const PREVIEW = 5;

export default function Requests() {
  const nav = useNav();
  const { category } = useLocalSearchParams<{ category?: string }>();
  const query = useQuery({ queryKey: ['requests'], queryFn: api.requests });
  const [filter, setFilter] = useState('all');
  const [showAll, setShowAll] = useState(false);

  const filtered = (items: RequestItem[]) => {
    const f = REQUEST_FILTERS.find((x) => x.key === filter);
    return items.filter(
      (r) => (!f?.statuses || f.statuses.includes(r.status)) && (!category || r.category === category),
    );
  };

  return (
    <Screen tabs header={<BrandBar />} refreshing={query.isRefetching} onRefresh={() => void query.refetch()}>
      <PageTitle
        title="Обращения"
        subtitle="Создавайте обращения и отслеживайте их статус"
        action={<Button small icon="add" title="Создать" onPress={() => nav.push('/requests/new')} />}
      />

      <View style={{ gap: space.md }}>
        <SectionHeader title="Категории" />
        <View style={{ gap: space.sm }}>
          {REQUEST_CATEGORIES.map((c) => (
            <Card
              key={c.key}
              onPress={() => nav.push(`/requests/new?category=${c.key}`)}
              style={{ flexDirection: 'row', alignItems: 'center', gap: space.md, padding: space.md }}
            >
              <IconCircle name={c.icon} size={40} />
              <View style={{ flex: 1 }}>
                <AppText variant="h3">{c.title}</AppText>
                <AppText variant="caption">{c.assignee}</AppText>
              </View>
              <AppText color={colors.accentDark}>›</AppText>
            </Card>
          ))}
        </View>
      </View>

      <View style={{ gap: space.md }}>
        <SectionHeader title="Мои обращения" />
        {category ? (
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: space.sm }}>
            <Badge label={`Категория: ${categoryByKey(category as RequestItem['category']).short}`} tone="info" />
            <Button small variant="ghost" title="Сбросить" onPress={() => nav.replace('/requests')} />
          </View>
        ) : null}
        <FilterTabs items={REQUEST_FILTERS.map((f) => ({ key: f.key, label: f.label }))} value={filter} onChange={(k) => { setFilter(k); setShowAll(false); }} />

        <Async
          query={query}
          isEmpty={(d) => filtered(d).length === 0}
          empty={
            <EmptyState
              icon="chatbubbles-outline"
              title="Обращений нет"
              text="Здесь появятся ваши обращения. Выберите категорию выше или создайте новое."
              action="Создать обращение"
              onAction={() => nav.push('/requests/new')}
            />
          }
        >
          {(items) => {
            const list = filtered(items);
            const shown = showAll ? list : list.slice(0, PREVIEW);
            return (
              <View style={{ gap: space.md }}>
                <Card style={{ padding: 0 }}>
                  {shown.map((r, i) => {
                    const cat = categoryByKey(r.category);
                    const st = REQUEST_STATUS[r.status];
                    return (
                      <View key={r.id}>
                        {i > 0 ? <Divider /> : null}
                        <RequestRow onPress={() => nav.push(`/requests/${r.id}`)}>
                          <IconCircle name={cat.icon} size={40} />
                          <View style={{ flex: 1, gap: 4 }}>
                            <AppText variant="h3" numberOfLines={2}>{r.subject}</AppText>
                            <AppText variant="caption">{cat.short}</AppText>
                            <MetaRow icon="calendar-outline">{fmtDate(r.createdAt)}, №{r.number}</MetaRow>
                          </View>
                          <Badge label={st.label} tone={st.tone} />
                        </RequestRow>
                      </View>
                    );
                  })}
                </Card>
                {list.length > PREVIEW ? (
                  <Button
                    variant="secondary"
                    title={showAll ? 'Свернуть' : `Показать все обращения (${list.length})`}
                    onPress={() => setShowAll((v) => !v)}
                  />
                ) : null}
              </View>
            );
          }}
        </Async>
      </View>
    </Screen>
  );
}

function RequestRow({ children, onPress }: { children: React.ReactNode; onPress: () => void }) {
  return (
    <Card
      onPress={onPress}
      style={{ flexDirection: 'row', alignItems: 'center', gap: space.md, borderWidth: 0, shadowOpacity: 0, elevation: 0, borderRadius: 0 }}
    >
      {children}
    </Card>
  );
}
