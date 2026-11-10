import { View } from 'react-native';
import type { Invoice } from '@/api/types';
import { INVOICE_KIND, INVOICE_STATUS } from '@/lib/config';
import { downloadFile } from '@/lib/files';
import { fmtDate, fmtMoney } from '@/lib/format';
import { colors } from '@/theme';
import { AppText, Badge, Button, Card, IconCircle } from '@/ui/primitives';
import { useToast } from '@/ui/feedback';
import { errorMessage } from '@/api/errors';

export function InvoiceCard({
  invoice,
  onPay,
  paying,
}: {
  invoice: Invoice;
  onPay: (invoice: Invoice) => void;
  paying?: boolean;
}) {
  const toast = useToast();
  const kind = INVOICE_KIND[invoice.kind];
  const status = INVOICE_STATUS[invoice.status];
  const payable = invoice.status !== 'paid';
  const rest = invoice.amount - invoice.paidAmount;

  const download = async () => {
    try {
      if (invoice.pdfUrl) await downloadFile(invoice.pdfUrl);
    } catch (e) {
      toast(errorMessage(e), 'error');
    }
  };

  return (
    <Card style={{ gap: 12, backgroundColor: payable ? colors.accentSoft : colors.card }}>
      <View style={{ flexDirection: 'row', gap: 12, alignItems: 'center' }}>
        <IconCircle name={kind.icon} size={40} />
        <View style={{ flex: 1 }}>
          <AppText variant="h3">{kind.label}</AppText>
          <AppText variant="small" numberOfLines={2}>{invoice.purpose}</AppText>
        </View>
      </View>

      <View style={{ gap: 2 }}>
        <AppText variant="amount">{fmtMoney(payable ? rest : invoice.amount)}</AppText>
        {invoice.status === 'partial' || (payable && invoice.paidAmount > 0) ? (
          <AppText variant="caption">Осталось оплатить из {fmtMoney(invoice.amount)}</AppText>
        ) : null}
      </View>

      <View style={{ gap: 2 }}>
        <AppText variant="small">Счёт №{invoice.number} от {fmtDate(invoice.issuedAt)}</AppText>
        <AppText variant="small" color={invoice.status === 'overdue' ? colors.danger : undefined}>
          Оплатить до {fmtDate(invoice.dueDate)}
        </AppText>
      </View>

      {payable && invoice.canPay === false ? <AppText variant="small">Онлайн-оплата пока недоступна. Реквизиты уточняйте в бухгалтерии.</AppText> : null}
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
        <Badge label={status.label} tone={status.tone} />
        <View style={{ flex: 1 }} />
        {invoice.pdfUrl ? <Button small variant="secondary" icon="download-outline" title="Скачать" onPress={download} /> : null}
        {payable && invoice.canPay !== false ? <Button small title="Оплатить" loading={paying} onPress={() => onPay(invoice)} /> : null}
      </View>
    </Card>
  );
}
