import { useState } from 'react';
import { Platform, View } from 'react-native';
import { Stack, useRouter, useSegments } from 'expo-router';
import { useEffect } from 'react';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { StatusBar } from 'expo-status-bar';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider, useAuth } from '@/auth/AuthProvider';
import { ToastProvider, Loading } from '@/ui/feedback';
import { colors } from '@/theme';

export default function RootLayout() {
  useEffect(() => { if (Platform.OS === 'web') document.documentElement.lang = 'ru'; }, []);
  const [client] = useState(
    () => new QueryClient({ defaultOptions: { queries: { retry: 1, staleTime: 15_000 } } }),
  );
  return (
    <SafeAreaProvider><QueryClientProvider client={client}>
      <AuthProvider>
        <ToastProvider>
          <StatusBar style="dark" />
          <Gate />
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider></SafeAreaProvider>
  );
}

function Gate() {
  const { ready, isAuthed } = useAuth();
  const router = useRouter();
  const segments = useSegments();
  useEffect(() => {
    if (!ready) return;
    const auth = segments[0] === '(auth)';
    if (!isAuthed && !auth) router.replace('/login');
    if (isAuthed && auth) router.replace('/');
  }, [ready, isAuthed, segments, router]);
  if (!ready) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', backgroundColor: colors.bg }}>
        <Loading />
      </View>
    );
  }
  return <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: colors.bg } }} />;
}
