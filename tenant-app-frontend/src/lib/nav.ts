import { useRouter, type Href } from 'expo-router';

/** Обёртка над router: принимает обычные строки-пути, без привязки к сгенерированным типам. */
export function useNav() {
  const router = useRouter();
  return {
    push: (href: string) => router.push(href as Href),
    replace: (href: string) => router.replace(href as Href),
    back: () => (router.canGoBack() ? router.back() : router.replace('/' as Href)),
  };
}
