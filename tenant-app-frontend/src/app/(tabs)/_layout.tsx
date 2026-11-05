import { useAuth } from "@/auth/AuthProvider";
import { roleHome } from "@/management/api";
import { Redirect } from "expo-router";
import { Tabs } from "expo-router";
import { Ionicons } from "@expo/vector-icons";
import { colors } from "@/theme";
import type { IconName } from "@/lib/config";
import { useNews } from "@/features/hooks";

const TABS: { name: string; title: string; on: IconName; off: IconName }[] = [
  { name: "index", title: "Главная", on: "home", off: "home-outline" },
  {
    name: "requests",
    title: "Обращения",
    on: "chatbubbles",
    off: "chatbubbles-outline",
  },
  {
    name: "documents",
    title: "Документы",
    on: "document-text",
    off: "document-text-outline",
  },
  {
    name: "premises",
    title: "Помещения",
    on: "business",
    off: "business-outline",
  },
  { name: "news", title: "Новости", on: "megaphone", off: "megaphone-outline" },
];

export default function TabsLayout() {
  const { user } = useAuth();
  if (!user) return <Redirect href="/login" />;
  if (user.role !== "Tenant")
    return <Redirect href={roleHome(user?.role) as never} />;
  return <TenantTabs />;
}
function TenantTabs() {
  const news = useNews();
  const unreadNews = news.data?.filter((n) => !n.read).length ?? 0;
  return (
    <Tabs
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: colors.accentDark,
        tabBarInactiveTintColor: colors.inactive,
        tabBarLabelStyle: { fontSize: 11, fontWeight: "600" },
        tabBarStyle: {
          backgroundColor: colors.card,
          borderTopColor: colors.border,
        },
        tabBarBadgeStyle: {
          backgroundColor: colors.accent,
          color: colors.text,
        },
      }}
    >
      {TABS.map((t) => (
        <Tabs.Screen
          key={t.name}
          name={t.name}
          options={{
            title: t.title,
            tabBarBadge:
              t.name === "news" && unreadNews > 0 ? unreadNews : undefined,
            tabBarIcon: ({ color, size, focused }) => (
              <Ionicons
                name={focused ? t.on : t.off}
                size={size}
                color={String(color)}
              />
            ),
          }}
        />
      ))}
    </Tabs>
  );
}
