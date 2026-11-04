import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import * as SecureStore from 'expo-secure-store';
import { useQueryClient } from '@tanstack/react-query';
import { api } from '@/api';
import { setSessionHandlers, setTokens } from '@/api/http';

const ACCESS = 'access_token';
const REFRESH = 'refresh_token';

async function save(key: string, value: string | null) {
  try {
    if (value === null) await SecureStore.deleteItemAsync(key);
    else await SecureStore.setItemAsync(key, value);
  } catch {
    /* хранилище недоступно (например, веб-версия) — работаем без сохранения */
  }
}

type AuthState = {
  ready: boolean;
  isAuthed: boolean;
  signIn: (login: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
};

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(false);
  const [isAuthed, setAuthed] = useState(false);
  const queryClient = useQueryClient();

  const signOut = useCallback(async () => {
    setTokens(null, null);
    await Promise.all([save(ACCESS, null), save(REFRESH, null)]);
    queryClient.clear();
    setAuthed(false);
  }, [queryClient]);

  useEffect(() => {
    setSessionHandlers({
      onUnauthorized: () => {
        void signOut();
      },
      onTokens: (access, refresh) => {
        void save(ACCESS, access);
        if (refresh) void save(REFRESH, refresh);
      },
    });
  }, [signOut]);

  useEffect(() => {
    (async () => {
      try {
        const access = await SecureStore.getItemAsync(ACCESS);
        const refresh = await SecureStore.getItemAsync(REFRESH);
        if (access) {
          setTokens(access, refresh);
          setAuthed(true);
        }
      } catch {
        /* нет сохранённой сессии */
      } finally {
        setReady(true);
      }
    })();
  }, []);

  const signIn = useCallback(async (login: string, password: string) => {
    const res = await api.login(login, password);
    setTokens(res.accessToken, res.refreshToken ?? null);
    await save(ACCESS, res.accessToken);
    if (res.refreshToken) await save(REFRESH, res.refreshToken);
    setAuthed(true);
  }, []);

  const value = useMemo(() => ({ ready, isAuthed, signIn, signOut }), [ready, isAuthed, signIn, signOut]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth должен вызываться внутри AuthProvider');
  return ctx;
}
