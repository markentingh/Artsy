import React, { createContext, useContext, useState, useCallback, useEffect } from 'react';
import { useSession } from '@/context/session';
import { AITokens } from '@/api/user/aiTokens';

const DashboardContext = createContext({
  tokens: null,
  refreshTokens: async () => {},
});

export function DashboardProvider({ children }) {
  const { user } = useSession();
  const [tokens, setTokens] = useState(null);

  const refreshTokens = useCallback(async () => {
    if (!user) return;
    const session = { token: localStorage.getItem('token') };
    const api = AITokens(session);
    try {
      const res = await api.getBalance();
      if (res.data.success) {
        setTokens(res.data.data);
      }
    } catch {
      // ignore
    }
  }, [user]);

  useEffect(() => {
    refreshTokens();
  }, [refreshTokens]);

  return (
    <DashboardContext.Provider value={{ tokens, refreshTokens }}>
      {children}
    </DashboardContext.Provider>
  );
}

export function useDashboard() {
  return useContext(DashboardContext);
}
