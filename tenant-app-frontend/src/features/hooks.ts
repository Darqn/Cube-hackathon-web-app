import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as WebBrowser from 'expo-web-browser';
import { api } from '@/api';
import { errorMessage } from '@/api/errors';
import type { Invoice } from '@/api/types';
import { useToast } from '@/ui/feedback';

export const useProfile = () => useQuery({ queryKey: ['profile'], queryFn: api.profile });
export const useMyPremises = () => useQuery({ queryKey: ['my-premises'], queryFn: api.myPremises });
export const useInvoices = () => useQuery({ queryKey: ['invoices'], queryFn: api.invoices });
export const useNews = () => useQuery({ queryKey: ['news'], queryFn: api.news });

/** Оплата счёта: открываем страницу платёжного сервиса и после возврата обновляем счета. */
export function usePay() {
  const qc = useQueryClient();
  const toast = useToast();
  const m = useMutation({
    mutationFn: async (invoice: Invoice) => {
      const session = await api.createPayment(invoice.id);
      await WebBrowser.openBrowserAsync(session.paymentUrl);
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['invoices'] });
      toast('Статус счёта обновляется. Это может занять минуту.');
    },
    onError: (e) => toast(errorMessage(e), 'error'),
  });
  return {
    pay: (invoice: Invoice) => m.mutate(invoice),
    payingId: m.isPending ? m.variables?.id : undefined,
  };
}
