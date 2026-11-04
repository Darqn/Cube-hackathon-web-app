import { View } from 'react-native';
import { PREMISE_STATUS, PREMISE_TYPE } from '@/lib/config';
import { fmtArea, fmtDate } from '@/lib/format';
import { useNav } from '@/lib/nav';
import { space } from '@/theme';
import { useMyPremises } from '@/features/hooks';
import { Async, EmptyState } from '@/ui/feedback';
import { BackBar, Screen } from '@/ui/layout';
import { AppText, Badge, Button, Card, Divider, IconCircle } from '@/ui/primitives';

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: space.md }}>
      <AppText variant="small">{label}</AppText>
      <AppText style={{ flexShrink: 1, textAlign: 'right' }}>{value}</AppText>
    </View>
  );
}

export default function MyPremises() {
  const nav = useNav();
  const query = useMyPremises();
  return (
    <Screen header={<BackBar title="Мои помещения" />} refreshing={query.isRefetching} onRefresh={() => void query.refetch()}>
      <Async
        query={query}
        isEmpty={(d) => d.length === 0}
        empty={<EmptyState icon="business-outline" title="Помещений пока нет" text="Когда администратор привяжет помещения к вашему аккаунту, они появятся здесь." />}
      >
        {(list) => (
          <View style={{ gap: space.lg }}>
            {list.map((p) => {
              const st = PREMISE_STATUS[p.status];
              return (
                <Card key={p.id} style={{ gap: space.md }}>
                  <View style={{ flexDirection: 'row', alignItems: 'center', gap: space.md }}>
                    <IconCircle name={PREMISE_TYPE[p.type].icon} />
                    <View style={{ flex: 1, gap: 4 }}>
                      <AppText variant="h2">{p.name}</AppText>
                      <Badge label={st.label} tone={st.tone} />
                    </View>
                  </View>
                  <Divider />
                  <Row label="Тип" value={p.subtype ?? PREMISE_TYPE[p.type].label} />
                  <Row label="Корпус" value={p.building} />
                  {p.floor ? <Row label="Этаж" value={String(p.floor)} /> : null}
                  <Row label="Площадь" value={fmtArea(p.area)} />
                  <Row label="Договор" value={p.contractNumber} />
                  <Row label="Дата заключения" value={fmtDate(p.contractDate)} />
                  <Row label="Срок действия" value={`${fmtDate(p.contractFrom)} – ${fmtDate(p.contractTo)}`} />
                  <View style={{ flexDirection: 'row', gap: space.sm, flexWrap: 'wrap', marginTop: 4 }}>
                    <Button small variant="secondary" icon="create-outline" title="Обращение" onPress={() => nav.push(`/requests/new?premiseId=${p.id}`)} />
                    <Button small variant="secondary" icon="speedometer-outline" title="Показания" onPress={() => nav.push('/meters')} />
                  </View>
                </Card>
              );
            })}
          </View>
        )}
      </Async>
    </Screen>
  );
}
