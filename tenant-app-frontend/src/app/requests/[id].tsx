import { useRef, useState } from 'react';
import { Pressable, ScrollView, Text, TextInput, View } from 'react-native';

type Scroller = { scrollToEnd: (options?: { animated?: boolean }) => void };
import { useLocalSearchParams } from 'expo-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/api';
import { errorMessage } from '@/api/errors';
import type { Attachment, LocalFile, RequestItem, RequestMessage } from '@/api/types';
import { REQUEST_STATUS, categoryByKey } from '@/lib/config';
import { viewFile } from '@/lib/files';
import { fmtDate, fmtDateTime, fmtTime } from '@/lib/format';
import { colors, radius, space } from '@/theme';
import { Async, useToast } from '@/ui/feedback';
import { AttachmentPicker } from '@/ui/form';
import { BackBar, Screen } from '@/ui/layout';
import { AppText, Badge, Card, Icon, IconCircle } from '@/ui/primitives';

export default function RequestDetails() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const qc = useQueryClient();
  const toast = useToast();
  const scroller = useRef<Scroller | null>(null);
  const [text, setText] = useState('');
  const [files, setFiles] = useState<LocalFile[]>([]);
  const [showFiles, setShowFiles] = useState(false);

  const request = useQuery({ queryKey: ['request', id], queryFn: () => api.request(id), refetchInterval: 15_000 });
  const messages = useQuery({ queryKey: ['request-messages', id], queryFn: () => api.requestMessages(id), refetchInterval: 8_000 });

  const send = useMutation({
    mutationFn: () => api.sendMessage(id, text.trim(), files),
    onSuccess: () => {
      setText('');
      setFiles([]);
      setShowFiles(false);
      toast('Сообщение отправлено');
      void qc.invalidateQueries({ queryKey: ['request-messages', id] });
      void qc.invalidateQueries({ queryKey: ['request', id] });
      setTimeout(() => scroller.current?.scrollToEnd({ animated: true }), 300);
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  });

  const closed = request.data?.status === 'closed' || request.data?.status === 'rejected';
  const canSend = (text.trim().length > 0 || files.length > 0) && !send.isPending;

  const composer = (
    <View style={{ backgroundColor: colors.card, borderTopWidth: 1, borderTopColor: colors.border, padding: space.md, gap: space.sm }}>
      {closed ? (
        <AppText variant="small" style={{ textAlign: 'center' }}>
          Обращение закрыто. Чтобы задать новый вопрос, создайте новое обращение.
        </AppText>
      ) : (
        <>
          {showFiles ? <AttachmentPicker files={files} onChange={setFiles} label="Вложения к сообщению" /> : null}
          <View style={{ flexDirection: 'row', alignItems: 'flex-end', gap: space.sm }}>
            <Pressable
              onPress={() => setShowFiles((v) => !v)}
              accessibilityLabel="Прикрепить файл"
              style={{ height: 44, width: 44, alignItems: 'center', justifyContent: 'center' }}
            >
              <Icon name="attach" size={26} color={files.length ? colors.accent : colors.muted} />
            </Pressable>
            <TextInput
              value={text}
              onChangeText={setText}
              multiline
              placeholder="Написать сообщение"
              placeholderTextColor={colors.inactive}
              accessibilityLabel="Сообщение"
              style={{
                flex: 1,
                maxHeight: 120,
                minHeight: 44,
                borderWidth: 1,
                borderColor: colors.border,
                borderRadius: radius.md,
                paddingHorizontal: space.md,
                paddingTop: 11,
                paddingBottom: 11,
                fontSize: 15,
                color: colors.text,
                backgroundColor: colors.bg,
              }}
            />
            <Pressable
              onPress={() => send.mutate()}
              disabled={!canSend}
              accessibilityLabel="Отправить"
              style={{
                height: 44,
                width: 44,
                borderRadius: 22,
                alignItems: 'center',
                justifyContent: 'center',
                backgroundColor: canSend ? colors.accent : colors.neutralBg,
              }}
            >
              <Icon name="send" size={20} color={canSend ? colors.text : colors.inactive} />
            </Pressable>
          </View>
        </>
      )}
    </View>
  );

  return (
    <Screen scroll={false} header={<BackBar title={request.data ? `Обращение №${request.data.number}` : 'Обращение'} />} footer={composer}>
      <ScrollView
        ref={(r) => {
          scroller.current = r as unknown as Scroller | null;
        }}
        keyboardShouldPersistTaps="handled"
        contentContainerStyle={{ padding: space.lg, gap: space.lg }}
      >
        <Async query={request}>{(r) => <Summary item={r} />}</Async>
        <AppText variant="h2">Переписка</AppText>
        <Async
          query={messages}
          isEmpty={(m) => m.length === 0}
          empty={<AppText variant="small">Сообщений пока нет.</AppText>}
        >
          {(list) => <Thread items={list} />}
        </Async>
      </ScrollView>
    </Screen>
  );
}

function Summary({ item }: { item: RequestItem }) {
  const cat = categoryByKey(item.category);
  const st = REQUEST_STATUS[item.status];
  return (
    <Card style={{ gap: space.md }}>
      <View style={{ flexDirection: 'row', gap: space.md, alignItems: 'center' }}>
        <IconCircle name={cat.icon} size={44} />
        <View style={{ flex: 1, gap: 4 }}>
          <AppText variant="h3">{item.subject}</AppText>
          <AppText variant="small">{cat.title}</AppText>
        </View>
      </View>
      <Badge label={st.label} tone={st.tone} />
      <AppText>{item.description}</AppText>
      <View style={{ gap: 2 }}>
        <AppText variant="small">Создано: {fmtDate(item.createdAt)}</AppText>
        {item.premiseName ? <AppText variant="small">Помещение: {item.premiseName}</AppText> : null}
        <AppText variant="small">Ответственный: {cat.assignee}</AppText>
        <AppText variant="small">Контакт: {item.contactName}, {item.phone}</AppText>
      </View>
      <Files items={item.attachments} />
    </Card>
  );
}

function Files({ items }: { items: Attachment[] }) {
  const toast = useToast();
  if (!items.length) return null;
  return (
    <View style={{ gap: 6 }}>
      {items.map((a) => (
        <Pressable
          key={a.id}
          onPress={() => viewFile(a.url).catch((e) => toast(errorMessage(e), 'error'))}
          style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}
        >
          <Icon name="document-attach-outline" size={18} />
          <Text style={{ color: colors.accentDark, fontSize: 14, flex: 1 }} numberOfLines={1}>{a.name}</Text>
        </Pressable>
      ))}
    </View>
  );
}

function Thread({ items }: { items: RequestMessage[] }) {
  return (
    <View style={{ gap: space.md }}>
      {items.map((m) => {
        if (m.kind === 'status') {
          const st = m.status ? REQUEST_STATUS[m.status] : null;
          return (
            <View key={m.id} style={{ alignItems: 'center', gap: 4 }}>
              <View style={{ flexDirection: 'row', gap: 8, alignItems: 'center' }}>
                {st ? <Badge label={st.label} tone={st.tone} /> : null}
              </View>
              <AppText variant="caption">{m.text}, {fmtDateTime(m.createdAt)}</AppText>
            </View>
          );
        }
        const mine = m.author === 'tenant';
        return (
          <View key={m.id} style={{ alignItems: mine ? 'flex-end' : 'flex-start' }}>
            <View
              style={{
                maxWidth: '86%',
                gap: 6,
                padding: space.md,
                borderRadius: radius.lg,
                backgroundColor: mine ? colors.accentSoft : colors.card,
                borderWidth: 1,
                borderColor: mine ? colors.accent : colors.border,
              }}
            >
              <AppText variant="caption" color={colors.heading} style={{ fontWeight: '700' }}>{m.authorName}</AppText>
              {m.text ? <AppText>{m.text}</AppText> : null}
              <Files items={m.attachments} />
              <AppText variant="caption" style={{ alignSelf: 'flex-end' }}>{fmtDate(m.createdAt)} {fmtTime(m.createdAt)}</AppText>
            </View>
          </View>
        );
      })}
    </View>
  );
}
