import React, { createContext, useContext, useState, useEffect } from 'react';
import { api } from '../services/api';

interface UserInfo {
  email: string;
  fullName: string;
  roles: string[];
}

interface AuthContextType {
  user: UserInfo | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  hasRole: (role: string) => boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<UserInfo | null>(() => {
    const saved = localStorage.getItem('crm_user');
    return saved ? JSON.parse(saved) : null;
  });
  const [token, setToken] = useState<string | null>(() => localStorage.getItem('crm_jwt_token'));
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    const handleUnauthorized = () => {
      setUser(null);
      setToken(null);
    };
    window.addEventListener('auth:unauthorized', handleUnauthorized);
    return () => window.removeEventListener('auth:unauthorized', handleUnauthorized);
  }, []);

  const login = async (email: string, password: string) => {
    setIsLoading(true);
    try {
      const response = await api.login(email, password);
      const userInfo: UserInfo = {
        email: response.email,
        fullName: response.fullName,
        roles: response.roles
      };
      setToken(response.token);
      setUser(userInfo);
      localStorage.setItem('crm_jwt_token', response.token);
      localStorage.setItem('crm_user', JSON.stringify(userInfo));
    } finally {
      setIsLoading(false);
    }
  };

  const logout = async () => {
    setIsLoading(true);
    try {
      await api.logout();
    } finally {
      setUser(null);
      setToken(null);
      setIsLoading(false);
    }
  };

  const hasRole = (role: string) => {
    return user?.roles.includes(role) ?? false;
  };

  return (
    <AuthContext.Provider value={{ user, token, isAuthenticated: !!token, isLoading, login, logout, hasRole }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
