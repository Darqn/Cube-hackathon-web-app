import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { useQueryClient } from "@tanstack/react-query";
import { api } from "@/api";
import { management, type Identity } from "@/management/api";
import { USE_MOCK } from "@/api/http";
import { setSessionHandlers, setTokens } from "@/api/http";

type AuthState = {
  ready: boolean;
  isAuthed: boolean;
  user: Identity | null;
  signIn: (login: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
};
const AuthContext = createContext<AuthState | null>(null);
export function AuthProvider({ children }: { children: ReactNode }) {
  const [isAuthed, setAuthed] = useState(false);
  const [user, setUser] = useState<Identity | null>(null);
  const client = useQueryClient();
  const clear = useCallback(() => {
    setTokens(null);
    client.clear();
    setAuthed(false);
    setUser(null);
  }, [client]);
  useEffect(() => {
    setSessionHandlers({ onUnauthorized: clear });
  }, [clear]);
  const signOut = useCallback(async () => {
    try {
      await api.logout();
    } finally {
      clear();
    }
  }, [clear]);
  const signIn = useCallback(
    async (login: string, password: string) => {
      const res = await api.login(login, password);
      setTokens(res.accessToken);
      try {
        setUser(
          USE_MOCK
            ? {
                id: "demo",
                userName: login,
                fullName: "Демо-арендатор",
                email: "",
                phoneNumber: "",
                role: "Tenant",
                tenantId: null,
              }
            : await management.identity(),
        );
      } catch (e) {
        try {
          await api.logout();
        } finally {
          clear();
        }
        throw e;
      }
      client.clear();
      setAuthed(true);
    },
    [clear, client],
  );
  const value = useMemo(
    () => ({ ready: true, isAuthed, user, signIn, signOut }),
    [isAuthed, user, signIn, signOut],
  );
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error("Нет AuthProvider");
  return value;
}
