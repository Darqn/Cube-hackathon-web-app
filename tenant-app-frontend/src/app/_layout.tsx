import { useState } from 'react';
import { View } from 'react-native';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider, useAuth } from '@/auth/AuthProvider';
import { ToastProvider, Loading } from '@/ui/feedback';
import { colors } from '@/theme';

export default function RootLayout() {
  const [client] = useState(
    () => new QueryClient({ defaultOptions: { queries: { retry: 1, staleTime: 15_000 } } }),
  );
  return (
    <QueryClientProvider client={client}>
      <AuthProvider>
        <ToastProvider>
          <StatusBar style="dark" />
          <Gate />
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider>
  );
}

function Gate() {
  const { ready, isAuthed } = useAuth();
  if (!ready) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', backgroundColor: colors.bg }}>
        <Loading />
      </View>
    );
  }
  return (
    <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: colors.bg } }}>
      <Stack.Protected guard={!isAuthed}>
        <Stack.Screen name="(auth)" />
      </Stack.Protected>
      <Stack.Protected guard={isAuthed}>
        <Stack.Screen name="(tabs)" />
        <Stack.Screen name="requests/new" />
        <Stack.Screen name="requests/[id]" />
        <Stack.Screen name="meters" />
        <Stack.Screen name="invoices" />
        <Stack.Screen name="my-premises" />
        <Stack.Screen name="premises/[id]" />
        <Stack.Screen name="news/[id]" />
        <Stack.Screen name="notifications" />
        <Stack.Screen name="profile" />
      </Stack.Protected>
    </Stack>
  );
}
