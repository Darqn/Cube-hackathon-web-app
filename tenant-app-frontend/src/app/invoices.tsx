import { useState } from 'react';
import { View } from 'react-native';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/api';
import { space } from '@/theme';
import { InvoiceCard } from '@/features/InvoiceCard';
import { usePay } from '@/features/hooks';
import { Async, EmptyState } from '@/ui/feedback';
import { BackBar, Screen } from '@/ui/layout';
import { FilterTabs } from '@/ui/primitives';

type Filter = 'all' | 'due' | 'paid';

export default function Invoices() {
  const query = useQuery({ queryKey: ['invoices'], queryFn: api.invoices });
  const [filter, setFilter] = useState<Filter>('all');
  const { pay, payingId } = usePay();

  return (
    <Screen header={<BackBar title="Счета и оплата" />} refreshing={query.isRefetching} onRefresh={() => void query.refetch()}>
      <Async
        query={query}
        isEmpty={(d) => d.length === 0}
        empty={<EmptyState icon="receipt-outline" title="Счетов пока нет" text="Выставленные счета появятся здесь." />}
      >
        {(items) => {
          const due = items.filter((i) => i.status !== 'paid');
          const paid = items.filter((i) => i.status === 'paid');
          const list = filter === 'all' ? items : filter === 'due' ? due : paid;
          return (
            <View style={{ gap: space.lg }}>
              <FilterTabs
                value={filter}
                onChange={setFilter}
                items={[
                  { key: 'all', label: 'Все', count: items.length },
                  { key: 'due', label: 'К оплате', count: due.length },
                  { key: 'paid', label: 'Оплаченные', count: paid.length },
                ]}
              />
              {list.length === 0 ? (
                <EmptyState icon="checkmark-circle-outline" title="Здесь пусто" text="В этом разделе нет счетов." />
              ) : (
                list.map((i) => <InvoiceCard key={i.id} invoice={i} onPay={pay} paying={payingId === i.id} />)
              )}
            </View>
          );
        }}
      </Async>
    </Screen>
  );
}
