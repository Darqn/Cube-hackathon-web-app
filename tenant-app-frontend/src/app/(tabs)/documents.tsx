import { useState } from 'react';
import { View } from 'react-native';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api';
import { errorMessage } from '@/api/errors';
import type { DocCategory, DocumentItem } from '@/api/types';
import { DOC_STATUS, DOC_TABS } from '@/lib/config';
import { downloadFile, pickDocuments, viewFile } from '@/lib/files';
import { fmtDate } from '@/lib/format';
import { space } from '@/theme';
import { Async, EmptyState, useToast } from '@/ui/feedback';
import { BrandBar, PageTitle, Screen } from '@/ui/layout';
import { AppText, Badge, Button, Card, FilterTabs, IconCircle } from '@/ui/primitives';

export default function Documents() {
  const toast = useToast();
  const qc = useQueryClient();
  const query = useQuery({ queryKey: ['documents'], queryFn: api.documents });
  const [tab, setTab] = useState<DocCategory>('contract');

  const upload = useMutation({
    mutationFn: async () => {
      const [file] = await pickDocuments(false);
      if (!file) return false;
      await api.uploadStatement(file);
      return true;
    },
    onSuccess: (done) => {
      if (!done) return;
      toast('Заявление загружено и передано на рассмотрение');
      setTab('statement');
      void qc.invalidateQueries({ queryKey: ['documents'] });
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  });

  const template = useMutation({
    mutationFn: async () => downloadFile(await api.statementTemplate()),
    onError: (e) => toast(errorMessage(e), 'error'),
  });

  return (
    <Screen tabs header={<BrandBar />} refreshing={query.isRefetching} onRefresh={() => void query.refetch()}>
      <PageTitle
        title="Мои документы"
        subtitle="Документы, относящиеся к вашей аренде"
        action={<Button small variant="secondary" icon="cloud-upload-outline" title="Заявление" loading={upload.isPending} onPress={() => upload.mutate()} />}
      />

      <Async query={query}>
        {(docs) => {
          const list = docs.filter((d) => d.category === tab).sort((a, b) => b.date.localeCompare(a.date));
          return (
            <View style={{ gap: space.lg }}>
              <FilterTabs
                value={tab}
                onChange={setTab}
                items={DOC_TABS.map((t) => ({ key: t.key, label: t.label, count: docs.filter((d) => d.category === t.key).length }))}
              />
              {tab === 'statement' ? (
                <Card style={{ gap: space.sm }}>
                  <AppText variant="h3">Нужно новое заявление?</AppText>
                  <AppText variant="small">Скачайте шаблон, заполните его и загрузите кнопкой «Заявление» выше.</AppText>
                  <Button small variant="secondary" icon="download-outline" title="Скачать шаблон" loading={template.isPending} onPress={() => template.mutate()} style={{ alignSelf: 'flex-start' }} />
                </Card>
              ) : null}
              {list.length === 0 ? (
                <EmptyState icon="folder-open-outline" title="Документов нет" text="Когда администратор загрузит документы этого раздела, они появятся здесь." />
              ) : (
                list.map((d) => <DocCard key={d.id} doc={d} />)
              )}
            </View>
          );
        }}
      </Async>
    </Screen>
  );
}

function DocCard({ doc }: { doc: DocumentItem }) {
  const toast = useToast();
  const icon = DOC_TABS.find((t) => t.key === doc.category)?.icon ?? 'document-outline';
  const st = doc.status ? DOC_STATUS[doc.status] : null;
  const run = (fn: (u: string) => Promise<void>) => fn(doc.url).catch((e) => toast(errorMessage(e), 'error'));
  return (
    <Card style={{ gap: space.md }}>
      <View style={{ flexDirection: 'row', gap: space.md }}>
        <IconCircle name={icon} size={44} />
        <View style={{ flex: 1, gap: 4 }}>
          <AppText variant="h3">{doc.title}</AppText>
          {doc.premiseName ? <AppText variant="small">Помещение: {doc.premiseName}</AppText> : null}
          {doc.contractNumber && doc.category !== 'contract' ? <AppText variant="small">К договору {doc.contractNumber}</AppText> : null}
          <AppText variant="small">
            {doc.validTo ? `${fmtDate(doc.date)} – ${fmtDate(doc.validTo)}` : fmtDate(doc.date)}
          </AppText>
          {st ? <Badge label={st.label} tone={st.tone} /> : null}
        </View>
      </View>
      <View style={{ flexDirection: 'row', gap: space.sm }}>
        <Button small variant="secondary" icon="eye-outline" title="Смотреть" onPress={() => run(viewFile)} style={{ flex: 1 }} />
        <Button small variant="secondary" icon="download-outline" title="Скачать" onPress={() => run(downloadFile)} style={{ flex: 1 }} />
      </View>
    </Card>
  );
}
