import { roleHome } from "@/management/api";
import { useState } from "react";
import { Platform, View } from "react-native";
import { Stack, useRouter, useSegments } from "expo-router";
import { useEffect } from "react";
import { SafeAreaProvider } from "react-native-safe-area-context";
import { StatusBar } from "expo-status-bar";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AuthProvider, useAuth } from "@/auth/AuthProvider";
import { ToastProvider, Loading } from "@/ui/feedback";
import { colors } from "@/theme";

export default function RootLayout() {
  useEffect(() => {
    if (Platform.OS === "web") document.documentElement.lang = "ru";
  }, []);
  const [client] = useState(
    () =>
      new QueryClient({
        defaultOptions: { queries: { retry: 1, staleTime: 15_000 } },
      }),
  );
  return (
    <SafeAreaProvider>
      <QueryClientProvider client={client}>
        <AuthProvider>
          <ToastProvider>
            <StatusBar style="dark" />
            <Gate />
          </ToastProvider>
        </AuthProvider>
      </QueryClientProvider>
    </SafeAreaProvider>
  );
}

function Gate() {
  const { ready, isAuthed, user } = useAuth();
  const router = useRouter();
  const segments = useSegments();
  useEffect(() => {
    if (!ready) return;
    const auth = segments[0] === "(auth)";
    if (!isAuthed && !auth) router.replace("/login");
    const managing = segments[0] === "manage";
    const staffing = segments[0] === "staff";
    if (
      isAuthed &&
      (auth ||
        (user?.role === "Admin" && !managing) ||
        (user?.role === "Staff" && !staffing))
    )
      router.replace(roleHome(user?.role) as never);
    if (isAuthed && user?.role === "Tenant" && (managing || staffing))
      router.replace("/");
  }, [ready, isAuthed, user?.role, segments, router]);
  if (!ready) {
    return (
      <View
        style={{
          flex: 1,
          justifyContent: "center",
          backgroundColor: colors.bg,
        }}
      >
        <Loading />
      </View>
    );
  }
  return (
    <Stack
      screenOptions={{
        headerShown: false,
        contentStyle: { backgroundColor: colors.bg },
      }}
    />
  );
}
