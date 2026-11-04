import { useState } from 'react';
import { View } from 'react-native';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api';
import { USE_MOCK } from '@/api/http';
import { errorMessage } from '@/api/errors';
import type { LocalFile, MeterType, MyPremise } from '@/api/types';
import { fmtDate, toNumber } from '@/lib/format';
import { colors, space } from '@/theme';
import { Async, EmptyState, useToast } from '@/ui/feedback';
import { AttachmentPicker, SelectField, TextField } from '@/ui/form';
import { BackBar, Screen } from '@/ui/layout';
import { AppText, Button, Card, Divider, IconCircle, Segmented } from '@/ui/primitives';
import { useMyPremises } from '@/features/hooks';

const METER: Record<MeterType, { label: string; unit: string }> = {
  electricity: { label: 'Электричество', unit: 'кВт·ч' },
  water: { label: 'Вода', unit: 'м³' },
};

export default function Meters() {
  const premises = useMyPremises();
  const history = useQuery({ queryKey: ['meter-history'], queryFn: api.meterHistory });
  const refresh = () => {
    void premises.refetch();
    void history.refetch();
  };
  return (
    <Screen header={<BackBar title="Показания счётчиков" />} refreshing={history.isRefetching} onRefresh={refresh}>
      <Async
        query={premises}
        isEmpty={(d) => d.length === 0}
        empty={<EmptyState icon="speedometer-outline" title="Нет помещений" text="Передавать показания можно после привязки помещения к вашему аккаунту." />}
      >
        {(list) => <MeterForm premises={list} />}
      </Async>

      <View style={{ gap: space.md }}>
        <AppText variant="h2">История показаний</AppText>
        <Async
          query={history}
          isEmpty={(d) => d.length === 0}
          empty={<EmptyState icon="time-outline" title="Показаний пока нет" text="Переданные показания появятся здесь." />}
        >
          {(items) => (
            <Card style={{ padding: 0 }}>
              {items.map((m, i) => (
                <View key={m.id}>
                  {i > 0 ? <Divider /> : null}
                  <View style={{ flexDirection: 'row', gap: space.md, padding: space.md, alignItems: 'center' }}>
                    <IconCircle name={m.meterType === 'water' ? 'water-outline' : 'flash-outline'} size={40} />
                    <View style={{ flex: 1, gap: 2 }}>
                      <AppText variant="h3">{m.premiseName}, {METER[m.meterType].label.toLowerCase()}</AppText>
                      <AppText variant="small">{fmtDate(m.date)}</AppText>
                      <AppText variant="small">Было {m.previous}, стало {m.value} {METER[m.meterType].unit}</AppText>
                    </View>
                    <View style={{ alignItems: 'flex-end' }}>
                      <AppText variant="h3">+{Math.max(0, m.value - m.previous)}</AppText>
                      <AppText variant="caption">{METER[m.meterType].unit}</AppText>
                    </View>
                  </View>
                </View>
              ))}
            </Card>
          )}
        </Async>
      </View>
    </Screen>
  );
}

function MeterForm({ premises }: { premises: MyPremise[] }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [premiseId, setPremiseId] = useState(premises[0].id);
  const [meterType, setMeterType] = useState<MeterType>('electricity');
  const [value, setValue] = useState('');
  const meters = useQuery({ queryKey: ['meters'], queryFn: api.meters, enabled: !USE_MOCK });
  const [chosen, setChosen] = useState('');
  const available = meters.data?.filter(m => m.active && m.premisesId === premiseId && m.type === (meterType === 'water' ? 'Water' : 'Electricity')) ?? [];
  const meter = available.find(m => m.id === chosen) ?? available[0];
  const [photo, setPhoto] = useState<LocalFile[]>([]);
  const [error, setError] = useState<string | undefined>();

  const previous = useQuery({
    queryKey: ['meter-prev', premiseId, meterType, meter?.id],
    queryFn: async () => USE_MOCK ? api.meterPrevious(premiseId, meterType) : (await api.meters()).find(m => m.id === meter?.id)?.lastValue ?? Promise.reject(new Error('Активный счётчик не найден')),
    enabled: USE_MOCK || !!meter,
  });

  const send = useMutation({
    mutationFn: (num: number) =>
      api.sendMeter({ premiseId, meterId: meter?.id, meterType, value: num, date: new Date().toISOString(), photo: photo[0] ?? null }),
    onSuccess: () => {
      toast('Показания отправлены ответственному сотруднику');
      setValue('');
      setPhoto([]);
      void qc.invalidateQueries({ queryKey: ['meter-history'] });
      void qc.invalidateQueries({ queryKey: ['meter-prev'] });
      void qc.invalidateQueries({ queryKey: ['meters'] });
      void qc.invalidateQueries({ queryKey: ['notifications'] });
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  });

  const submit = () => {
    const num = toNumber(value);
    if (value.trim() === '' || !Number.isFinite(num) || num < 0) return setError('Введите новое показание числом');
    if (previous.data !== undefined && num < previous.data)
      return setError(`Показание не может быть меньше предыдущего (${previous.data})`);
    setError(undefined);
    send.mutate(num);
  };

  return (
    <Card style={{ gap: space.lg }}>
      <SelectField
        label="Помещение"
        value={premiseId}
        onChange={setPremiseId}
        options={premises.map((p) => ({ value: p.id, label: p.name, hint: p.building }))}
      />
      <View style={{ gap: 6 }}>
        <AppText variant="small" color={colors.heading} style={{ fontWeight: '600' }}>Тип счётчика</AppText>
        <Segmented
          value={meterType}
          onChange={setMeterType}
          items={[
            { key: 'electricity', label: 'Электричество' },
            { key: 'water', label: 'Вода' },
          ]}
        />
      </View>
      {!USE_MOCK ? <Async query={meters}>{() => available.length ? <SelectField label="Счётчик" value={meter?.id ?? ''} onChange={setChosen} options={available.map(m => ({ value: m.id, label: m.serialNumber }))} /> : <AppText variant="small">Нет активных счётчиков этого типа. Обратитесь к администратору.</AppText>}</Async> : null}
      <TextField
        label="Предыдущее показание"
        value={previous.isPending ? 'Загрузка…' : String(previous.data ?? '—')}
        editable={false}
        error={previous.isError ? 'Не удалось получить предыдущее показание' : undefined}
      />
      <TextField
        label={`Новое показание, ${METER[meterType].unit}`}
        value={value}
        onChangeText={(t) => {
          setValue(t);
          setError(undefined);
        }}
        keyboardType="decimal-pad"
        error={error}
      />
      <TextField label="Дата передачи" value={fmtDate(new Date().toISOString())} editable={false} />
      <AttachmentPicker files={photo} onChange={setPhoto} max={1} label="Фото счётчика (необязательно)" />
      <Button title="Отправить" icon="send" loading={send.isPending} disabled={previous.isPending || previous.isError || (!USE_MOCK && !meter)} onPress={submit} />
    </Card>
  );
}
