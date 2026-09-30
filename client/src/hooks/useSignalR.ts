import { useEffect, useState, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuth } from '../context/AuthContext';

export interface SignalRCallbacks {
  onLeadStageChanged?: (data: { leadId: number; companyName: string; previousStatus: string; newStatus: string; priorityScore: number; updatedBy: string }) => void;
  onLeadScored?: (data: { leadId: number; priorityScore: number }) => void;
  onLeadAssigned?: (data: { leadId: number; title: string; message: string }) => void;
  onActivityCreated?: (data: { leadId: number; lead: string; type: string; message: string; occurredUtc: string }) => void;
}

export function useSignalR(callbacks: SignalRCallbacks = {}) {
  const { token, isAuthenticated } = useAuth();
  const [connectionState, setConnectionState] = useState<'connected' | 'reconnecting' | 'disconnected'>('disconnected');
  const callbacksRef = useRef(callbacks);
  callbacksRef.current = callbacks;

  useEffect(() => {
    if (!isAuthenticated) {
      setConnectionState('disconnected');
      return;
    }

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/crm', {
        accessTokenFactory: () => token || ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.onreconnecting(() => setConnectionState('reconnecting'));
    connection.onreconnected(() => setConnectionState('connected'));
    connection.onclose(() => setConnectionState('disconnected'));

    connection.on('leadStageChanged', (data) => {
      callbacksRef.current.onLeadStageChanged?.(data);
    });

    connection.on('leadScored', (data) => {
      callbacksRef.current.onLeadScored?.(data);
    });

    connection.on('leadAssigned', (data) => {
      callbacksRef.current.onLeadAssigned?.(data);
    });

    connection.on('activityCreated', (data) => {
      callbacksRef.current.onActivityCreated?.(data);
    });

    let isMounted = true;

    async function start() {
      try {
        await connection.start();
        if (isMounted) setConnectionState('connected');
      } catch {
        if (isMounted) setConnectionState('disconnected');
      }
    }

    start();

    return () => {
      isMounted = false;
      connection.stop();
    };
  }, [token, isAuthenticated]);

  return { connectionState };
}
