import { useRouter, usePathname, type Href } from 'expo-router';

const target = (href: string) => {
  const [path, query] = href.split('?');
  return (['/', '/requests', '/documents', '/premises', '/news'].includes(path) ? `/(tabs)${path === '/' ? '' : path}` : path) + (query ? `?${query}` : '');
};

/** Обёртка над router: принимает обычные строки-пути, без привязки к сгенерированным типам. */
export function useNav() {
  const router = useRouter();
  const path = usePathname();
  return {
    push: (href: string) => router.push(target(href) as Href),
    replace: (href: string) => router.replace(target(href) as Href),
    back: () => {
      const parent = path.startsWith('/requests/') ? '/requests' : path.startsWith('/premises/') ? '/premises' : path.startsWith('/news/') ? '/news' : ['/register', '/forgot'].includes(path) ? '/login' : '/';
      router.replace(target(parent) as Href);
    },
  };
}
