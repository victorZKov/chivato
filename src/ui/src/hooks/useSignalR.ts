import { useState, useEffect, useCallback, useRef } from 'react';
import * as signalR from '@microsoft/signalr';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:7071/api';

interface UseSignalROptions {
  hubName?: string;
  autoConnect?: boolean;
  tenantId?: string;
}

interface UseSignalRReturn {
  connection: signalR.HubConnection | null;
  connectionState: signalR.HubConnectionState;
  connect: () => Promise<void>;
  disconnect: () => Promise<void>;
  joinGroup: (groupName: string) => Promise<void>;
  leaveGroup: (groupName: string) => Promise<void>;
  on: <T>(eventName: string, callback: (data: T) => void) => void;
  off: (eventName: string) => void;
  isConnected: boolean;
  error: string | null;
}

export function useSignalR(options: UseSignalROptions = {}): UseSignalRReturn {
  const { autoConnect = false, tenantId } = options;

  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [connectionState, setConnectionState] = useState<signalR.HubConnectionState>(
    signalR.HubConnectionState.Disconnected
  );
  const [error, setError] = useState<string | null>(null);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  const connect = useCallback(async () => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    try {
      setError(null);

      // Get negotiate info from API
      const negotiateResponse = await fetch(`${API_URL}/signalr/negotiate`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!negotiateResponse.ok) {
        const errorData = await negotiateResponse.json();
        throw new Error(errorData.error || 'Failed to negotiate SignalR connection');
      }

      const negotiateData = await negotiateResponse.json();

      // Build connection
      const newConnection = new signalR.HubConnectionBuilder()
        .withUrl(negotiateData.url, {
          accessTokenFactory: () => negotiateData.accessToken,
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Information)
        .build();

      // Set up connection state change handlers
      newConnection.onclose((err) => {
        setConnectionState(signalR.HubConnectionState.Disconnected);
        if (err) {
          setError(err.message);
        }
      });

      newConnection.onreconnecting((err) => {
        setConnectionState(signalR.HubConnectionState.Reconnecting);
        if (err) {
          console.warn('SignalR reconnecting:', err.message);
        }
      });

      newConnection.onreconnected(() => {
        setConnectionState(signalR.HubConnectionState.Connected);
        // Rejoin tenant group on reconnect
        if (tenantId) {
          joinGroupInternal(newConnection, `tenant-${tenantId}`);
        }
      });

      // Start connection
      await newConnection.start();
      connectionRef.current = newConnection;
      setConnection(newConnection);
      setConnectionState(signalR.HubConnectionState.Connected);

      // Join tenant group if specified
      if (tenantId) {
        await joinGroupInternal(newConnection, `tenant-${tenantId}`);
      }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to connect to SignalR';
      setError(errorMessage);
      console.error('SignalR connection error:', err);
    }
  }, [tenantId]);

  const disconnect = useCallback(async () => {
    if (connectionRef.current) {
      await connectionRef.current.stop();
      connectionRef.current = null;
      setConnection(null);
      setConnectionState(signalR.HubConnectionState.Disconnected);
    }
  }, []);

  const joinGroupInternal = async (conn: signalR.HubConnection, groupName: string) => {
    try {
      await fetch(`${API_URL}/signalr/groups/join`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          connectionId: conn.connectionId,
          groupName,
        }),
      });
    } catch (err) {
      console.error('Failed to join group:', err);
    }
  };

  const joinGroup = useCallback(async (groupName: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await joinGroupInternal(connectionRef.current, groupName);
    }
  }, []);

  const leaveGroup = useCallback(async (groupName: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      try {
        await fetch(`${API_URL}/signalr/groups/leave`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            connectionId: connectionRef.current.connectionId,
            groupName,
          }),
        });
      } catch (err) {
        console.error('Failed to leave group:', err);
      }
    }
  }, []);

  const on = useCallback(<T,>(eventName: string, callback: (data: T) => void) => {
    if (connectionRef.current) {
      connectionRef.current.on(eventName, callback);
    }
  }, []);

  const off = useCallback((eventName: string) => {
    if (connectionRef.current) {
      connectionRef.current.off(eventName);
    }
  }, []);

  // Auto-connect if enabled
  useEffect(() => {
    if (autoConnect) {
      connect();
    }

    return () => {
      disconnect();
    };
  }, [autoConnect, connect, disconnect]);

  return {
    connection,
    connectionState,
    connect,
    disconnect,
    joinGroup,
    leaveGroup,
    on,
    off,
    isConnected: connectionState === signalR.HubConnectionState.Connected,
    error,
  };
}

export default useSignalR;
