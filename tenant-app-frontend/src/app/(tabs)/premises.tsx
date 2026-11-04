import { useState } from 'react';
import { Image, Switch, View } from 'react-native';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/api';
import type { Listing, PremiseType } from '@/api/types';
import { LISTING_STATUS, PREMISE_TYPE } from '@/lib/config';
import { fmtArea, fmtMoney, toNumber } from '@/lib/format';
import { useNav } from '@/lib/nav';
import { colors, radius, space } from '@/theme';
import { Async, EmptyState } from '@/ui/feedback';
import { SelectField, TextField } from '@/ui/form';
import { BrandBar, PageTitle, Screen } from '@/ui/layout';
import { AppText, Badge, Button, Card, FilterTabs, Icon, MetaRow } from '@/ui/primitives';

type TypeFilter = 'all' | PremiseType;
type Filters = {
  areaFrom: string; areaTo: string; priceFrom: string; priceTo: string;
  building: string; floor: string; onlyPhoto: boolean; onlyFree: boolean;
};
const EMPTY: Filters = { areaFrom: '', areaTo: '', priceFrom: '', priceTo: '', building: '', floor: '', onlyPhoto: false, onlyFree: false };

const num = (s: string) => (s.trim() === '' ? null : toNumber(s));

function applyFilters(items: Listing[], type: TypeFilter, f: Filters) {
  const af = num(f.areaFrom), at = num(f.areaTo), pf = num(f.priceFrom), pt = num(f.priceTo);
  return items.filter(
    (l) =>
      (type === 'all' || l.type === type) &&
      (af === null || l.area >= af) &&
      (at === null || l.area <= at) &&
      (pf === null || l.price >= pf) &&
      (pt === null || l.price <= pt) &&
      (!f.building || l.building === f.building) &&
      (!f.floor || String(l.floor) === f.floor) &&
      (!f.onlyPhoto || l.photos.length > 0) &&
      (!f.onlyFree || l.status === 'free'),
  );
}

export default function Premises() {
  const nav = useNav();
  const query = useQuery({ queryKey: ['listings'], queryFn: api.listings });
  const [type, setType] = useState<TypeFilter>('all');
  const [filters, setFilters] = useState<Filters>(EMPTY);
  const [open, setOpen] = useState(false);
  const set = <K extends keyof Filters>(k: K, v: Filters[K]) => setFilters((f) => ({ ...f, [k]: v }));

  return (
    <Screen tabs header={<BrandBar />} refreshing={query.isRefetching} onRefresh={() => void query.refetch()}>
      <PageTitle
        title="Актуальные помещения"
        subtitle="Свободные помещения и заявки на аренду"
        action={<Button small variant="secondary" icon="document-text-outline" title="Мои заявки" onPress={() => nav.push('/requests?category=rent')} />}
      />

      <FilterTabs
        value={type}
        onChange={setType}
        items={[
          { key: 'all', label: 'Все' },
          { key: 'office', label: PREMISE_TYPE.office.plural },
          { key: 'warehouse', label: PREMISE_TYPE.warehouse.plural },
          { key: 'retail', label: PREMISE_TYPE.retail.plural },
        ]}
      />

      <Async
        query={query}
        isEmpty={(d) => d.length === 0}
        empty={<EmptyState icon="business-outline" title="Свободных помещений нет" text="Загляните позже или создайте обращение по вопросу аренды." action="Создать обращение" onAction={() => nav.push('/requests/new?category=rent')} />}
      >
        {(items) => {
          const list = applyFilters(items, type, filters);
          const buildings = [...new Set(items.map((l) => l.building))];
          const floors = [...new Set(items.map((l) => l.floor))].sort((a, b) => a - b);
          return (
            <View style={{ gap: space.lg }}>
              <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
                <AppText variant="small">Найдено: {list.length}</AppText>
                <View style={{ flexDirection: 'row', gap: space.sm }}>
                  <Button small variant="ghost" title="Сбросить" onPress={() => { setFilters(EMPTY); setType('all'); }} />
                  <Button small variant="secondary" icon="options-outline" title={open ? 'Скрыть' : 'Фильтры'} onPress={() => setOpen((v) => !v)} />
                </View>
              </View>

              {open ? (
                <Card style={{ gap: space.md }}>
                  <View style={{ flexDirection: 'row', gap: space.md }}>
                    <View style={{ flex: 1 }}><TextField label="Площадь от, м²" value={filters.areaFrom} onChangeText={(t) => set('areaFrom', t)} keyboardType="decimal-pad" /></View>
                    <View style={{ flex: 1 }}><TextField label="до, м²" value={filters.areaTo} onChangeText={(t) => set('areaTo', t)} keyboardType="decimal-pad" /></View>
                  </View>
                  <View style={{ flexDirection: 'row', gap: space.md }}>
                    <View style={{ flex: 1 }}><TextField label="Цена от, ₽" value={filters.priceFrom} onChangeText={(t) => set('priceFrom', t)} keyboardType="number-pad" /></View>
                    <View style={{ flex: 1 }}><TextField label="до, ₽" value={filters.priceTo} onChangeText={(t) => set('priceTo', t)} keyboardType="number-pad" /></View>
                  </View>
                  <SelectField label="Корпус" value={filters.building} onChange={(v) => set('building', v)} placeholder="Любой"
                    options={[{ value: '', label: 'Любой' }, ...buildings.map((b) => ({ value: b, label: b }))]} />
                  <SelectField label="Этаж" value={filters.floor} onChange={(v) => set('floor', v)} placeholder="Любой"
                    options={[{ value: '', label: 'Любой' }, ...floors.map((f) => ({ value: String(f), label: `${f} этаж` }))]} />
                  <ToggleRow label="Только с фото" value={filters.onlyPhoto} onChange={(v) => set('onlyPhoto', v)} />
                  <ToggleRow label="Только свободные" value={filters.onlyFree} onChange={(v) => set('onlyFree', v)} />
                </Card>
              ) : null}

              {list.length === 0 ? (
                <EmptyState icon="search-outline" title="Ничего не найдено" text="Измените или сбросьте фильтры." />
              ) : (
                list.map((l) => <ListingCard key={l.id} item={l} />)
              )}
            </View>
          );
        }}
      </Async>
    </Screen>
  );
}

function ToggleRow({ label, value, onChange }: { label: string; value: boolean; onChange: (v: boolean) => void }) {
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
      <AppText>{label}</AppText>
      <Switch value={value} onValueChange={onChange} trackColor={{ true: colors.accent, false: colors.border }} thumbColor={colors.card} />
    </View>
  );
}

function ListingCard({ item }: { item: Listing }) {
  const nav = useNav();
  const st = LISTING_STATUS[item.status];
  return (
    <Card onPress={() => nav.push(`/premises/${item.id}`)} style={{ padding: 0, overflow: 'hidden' }}>
      {item.photos[0] ? (
        <Image source={{ uri: item.photos[0] }} style={{ width: '100%', height: 170, backgroundColor: colors.neutralBg }} resizeMode="cover" accessibilityLabel={item.name} />
      ) : (
        <View style={{ height: 120, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.neutralBg }}>
          <Icon name={PREMISE_TYPE[item.type].icon} size={40} color={colors.inactive} />
        </View>
      )}
      <View style={{ padding: space.lg, gap: space.sm }}>
        <View style={{ flexDirection: 'row', gap: 6, flexWrap: 'wrap' }}>
          <Badge label={item.subtype ?? PREMISE_TYPE[item.type].label} tone="info" />
          <Badge label={st.label} tone={st.tone} />
          {item.isNew ? <Badge label="Новое" tone="warning" /> : null}
        </View>
        <AppText variant="h2">{item.name}</AppText>
        <View style={{ flexDirection: 'row', gap: space.lg, flexWrap: 'wrap' }}>
          <MetaRow icon="resize-outline">{fmtArea(item.area)}</MetaRow>
          <MetaRow icon="business-outline">{item.building}</MetaRow>
          <MetaRow icon="layers-outline">{item.floor} этаж</MetaRow>
        </View>
        <AppText variant="small" numberOfLines={2}>{item.description}</AppText>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: space.sm, marginTop: 4 }}>
          <AppText variant="h2" color={colors.accentDark} style={{ flex: 1 }}>{fmtMoney(item.price)} / мес.</AppText>
          <Button small variant="secondary" title="Подробнее" onPress={() => nav.push(`/premises/${item.id}`)} />
        </View>
      </View>
    </Card>
  );
}
