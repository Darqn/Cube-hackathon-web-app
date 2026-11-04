import { Image, ScrollView, View, useWindowDimensions } from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/api';
import type { Listing } from '@/api/types';
import { LISTING_STATUS, PREMISE_TYPE } from '@/lib/config';
import { fmtArea, fmtMoney } from '@/lib/format';
import { useNav } from '@/lib/nav';
import { colors, space } from '@/theme';
import { Async } from '@/ui/feedback';
import { BackBar, Screen } from '@/ui/layout';
import { AppText, Badge, Button, Card, Divider, Icon } from '@/ui/primitives';

export default function ListingDetails() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const query = useQuery({ queryKey: ['listing', id], queryFn: () => api.listing(id) });
  return (
    <Screen header={<BackBar title="Помещение" />}>
      <Async query={query}>{(l) => <Details item={l} />}</Async>
    </Screen>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: space.md }}>
      <AppText variant="small">{label}</AppText>
      <AppText style={{ flexShrink: 1, textAlign: 'right' }}>{value}</AppText>
    </View>
  );
}

function Details({ item }: { item: Listing }) {
  const nav = useNav();
  const { width } = useWindowDimensions();
  const photoWidth = width - space.lg * 2;
  const st = LISTING_STATUS[item.status];
  const canApply = item.status === 'free';

  const apply = () => {
    const subject = encodeURIComponent(`Заявка на помещение: ${item.name}`);
    const description = encodeURIComponent(
      `Прошу рассмотреть возможность аренды помещения «${item.name}» (${fmtArea(item.area)}, ${item.building}, ${item.floor} этаж).`,
    );
    nav.push(`/requests/new?category=rent&subject=${subject}&description=${description}`);
  };

  return (
    <View style={{ gap: space.lg }}>
      {item.photos.length > 0 ? (
        <ScrollView horizontal pagingEnabled={false} snapToInterval={photoWidth + space.md} decelerationRate="fast" showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: space.md }}>
          {item.photos.map((uri) => (
            <Image key={uri} source={{ uri }} style={{ width: photoWidth, height: 220, borderRadius: 16, backgroundColor: colors.neutralBg }} resizeMode="cover" />
          ))}
        </ScrollView>
      ) : (
        <View style={{ height: 140, borderRadius: 16, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.neutralBg }}>
          <Icon name={PREMISE_TYPE[item.type].icon} size={44} color={colors.inactive} />
        </View>
      )}

      <View style={{ gap: space.sm }}>
        <View style={{ flexDirection: 'row', gap: 6, flexWrap: 'wrap' }}>
          <Badge label={item.subtype ?? PREMISE_TYPE[item.type].label} tone="info" />
          <Badge label={st.label} tone={st.tone} />
        </View>
        <AppText variant="title">{item.name}</AppText>
        <AppText variant="amount" color={colors.accentDark}>{fmtMoney(item.price)} / мес.</AppText>
      </View>

      <Card style={{ gap: space.md }}>
        <Row label="Площадь" value={fmtArea(item.area)} />
        <Row label="Корпус" value={item.building} />
        <Row label="Этаж" value={String(item.floor)} />
        {item.utilities ? (
          <>
            <Divider />
            <Row label="Коммунальные условия" value={item.utilities} />
          </>
        ) : null}
      </Card>

      <Card style={{ gap: space.sm }}>
        <AppText variant="h3">Описание</AppText>
        <AppText>{item.description}</AppText>
      </Card>

      <Button title="Оставить заявку" icon="send" disabled={!canApply} onPress={apply} />
      {!canApply ? <AppText variant="caption" style={{ textAlign: 'center' }}>Заявки принимаются только на свободные помещения.</AppText> : null}
    </View>
  );
}
