import { ScrollView, View } from 'react-native';
import { PREMISE_STATUS, PREMISE_TYPE, type IconName } from '@/lib/config';
import { fmtArea } from '@/lib/format';
import { useNav } from '@/lib/nav';
import { colors, radius, space } from '@/theme';
import { InvoiceCard } from '@/features/InvoiceCard';
import { useInvoices, useMyPremises, useNews, useProfile, usePay } from '@/features/hooks';
import { Async, EmptyState } from '@/ui/feedback';
import { BrandBar, Screen } from '@/ui/layout';
import { AppText, Badge, Button, Card, Icon, IconCircle, SectionHeader } from '@/ui/primitives';

const QUICK: { title: string; icon: IconName; href: string }[] = [
  { title: 'Передать показания', icon: 'speedometer-outline', href: '/meters' },
  { title: 'Создать обращение', icon: 'create-outline', href: '/requests/new' },
  { title: 'Документы', icon: 'folder-open-outline', href: '/documents' },
  { title: 'Актуальные помещения', icon: 'business-outline', href: '/premises' },
];

export default function Home() {
  const nav = useNav();
  const profile = useProfile();
  const premises = useMyPremises();
  const invoices = useInvoices();
  const news = useNews();
  const { pay, payingId } = usePay();

  const queries = [profile, premises, invoices, news];
  const refreshing = queries.some((q) => q.isRefetching);
  const refresh = () => {
    queries.forEach((q) => void q.refetch());
  };

  const urgent = news.data?.find((n) => n.urgent && !n.acknowledged);

  return (
    <Screen tabs header={<BrandBar />} refreshing={refreshing} onRefresh={refresh}>
      <View style={{ gap: 4 }}>
        <AppText variant="small">Здравствуйте,</AppText>
        <AppText variant="title">{profile.data?.orgName ?? ' '}</AppText>
        <AppText variant="small">Помещения, счета и обращения в одном месте</AppText>
      </View>

      {urgent ? (
        <Card
          onPress={() => nav.push(`/news/${urgent.id}`)}
          style={{ borderColor: colors.accent, backgroundColor: colors.accentSoft, flexDirection: 'row', gap: space.md }}
        >
          <IconCircle name="warning-outline" tone="warning" />
          <View style={{ flex: 1, gap: 4 }}>
            <AppText variant="small" color={colors.accentDark} style={{ fontWeight: '700' }}>Важное уведомление</AppText>
            <AppText variant="h3">{urgent.title}</AppText>
            <AppText variant="small" numberOfLines={3}>{urgent.body}</AppText>
            <Button small title="Подробнее" onPress={() => nav.push(`/news/${urgent.id}`)} style={{ alignSelf: 'flex-start', marginTop: 4 }} />
          </View>
        </Card>
      ) : null}

      <View style={{ gap: space.md }}>
        <SectionHeader title="Мои помещения" action="Все помещения" onAction={() => nav.push('/my-premises')} />
        <Async
          query={premises}
          isEmpty={(d) => d.length === 0}
          empty={<EmptyState icon="business-outline" title="Помещений пока нет" text="Когда администратор привяжет помещения к вашему аккаунту, они появятся здесь." />}
        >
          {(list) => (
            <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: space.md }}>
              {list.map((p) => (
                <Card key={p.id} onPress={() => nav.push('/my-premises')} style={{ width: 230, gap: space.sm }}>
                  <View style={{ flexDirection: 'row', alignItems: 'center', gap: space.sm }}>
                    <IconCircle name={PREMISE_TYPE[p.type].icon} size={36} />
                    <AppText variant="h3" numberOfLines={1} style={{ flex: 1 }}>{p.name}</AppText>
                  </View>
                  <AppText variant="small">{fmtArea(p.area)}, {p.building}</AppText>
                  <View style={{ flexDirection: 'row', gap: 6, flexWrap: 'wrap' }}>
                    <Badge label={p.subtype ?? PREMISE_TYPE[p.type].label} />
                    <Badge label={PREMISE_STATUS[p.status].label} tone={PREMISE_STATUS[p.status].tone} />
                  </View>
                  <AppText variant="small">Договор {p.contractNumber}</AppText>
                </Card>
              ))}
            </ScrollView>
          )}
        </Async>
      </View>

      <View style={{ gap: space.md }}>
        <SectionHeader title="Счета к оплате" action="Все счета" onAction={() => nav.push('/invoices')} />
        <Async
          query={invoices}
          isEmpty={(d) => d.filter((i) => i.status !== 'paid').length === 0}
          empty={<EmptyState icon="checkmark-circle-outline" title="Неоплаченных счетов нет" text="Все выставленные счета оплачены." />}
        >
          {(list) => (
            <View style={{ gap: space.md }}>
              {list
                .filter((i) => i.status !== 'paid')
                .slice(0, 3)
                .map((i) => (
                  <InvoiceCard key={i.id} invoice={i} onPay={pay} paying={payingId === i.id} />
                ))}
            </View>
          )}
        </Async>
      </View>

      <View style={{ gap: space.md }}>
        <SectionHeader title="Быстрые действия" />
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: space.md }}>
          {QUICK.map((q) => (
            <Card key={q.title} onPress={() => nav.push(q.href)} style={{ width: '47.5%', alignItems: 'center', gap: space.sm, paddingVertical: space.lg, borderRadius: radius.lg }}>
              <Icon name={q.icon} size={30} />
              <AppText variant="small" color={colors.heading} style={{ textAlign: 'center', fontWeight: '600' }}>{q.title}</AppText>
            </Card>
          ))}
        </View>
      </View>

    </Screen>
  );
}
