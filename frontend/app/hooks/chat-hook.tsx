import { useEffect, useRef, useState, useCallback } from "react";
import {
  HubConnectionBuilder,
  HubConnection,
  HubConnectionState,
  HttpTransportType,
} from "@microsoft/signalr";
import { useAuth } from "@clerk/nextjs"
// Match your backend message type
export type FileMessage = {
  fileId: string;
  fileName: string;
  fileType: string;
  text?: string;
};

export type CustomClientMessage = {
  text: string;
  role: "user" | "assistant";
  files: FileMessage[];
};

// Hook callback signatures
export type ChatHubCallbacks = {
  onReceiveMessage?: (chatId: string, message: CustomClientMessage) => void;
  onConnected?: () => void;
  onDisconnected?: () => void;
};

export function useChatHub(
  callbacks: ChatHubCallbacks
) {
  const [isConnected, setIsConnected] = useState(false);
  const connectionRef = useRef<HubConnection | null>(null);
  const { getToken } = useAuth();
  const [token, setToken] = useState<string | null>(null);

  useEffect(() => {
    getToken().then(setToken);
  }, [getToken]);

  useEffect(() => {
    

    const connection = new HubConnectionBuilder()
      .withUrl(`${process.env.NEXT_PUBLIC_API_URL}/api/hubs/chat`, {
        accessTokenFactory: () => token!,
        skipNegotiation: true,
        transport: HttpTransportType.WebSockets,
        withCredentials: false,
      })
      .withAutomaticReconnect()
      .build();

    connectionRef.current = connection;

    // Register server event
    connection.on(
      "ReceiveMessage",
      (chatRoomId: string, message: CustomClientMessage) => {
        callbacks.onReceiveMessage?.(chatRoomId, message);
      }
    );

    connection
      .start()
      .then(() => {
        console.log("✅ Connected to ChatHub");
        setIsConnected(true);
        callbacks.onConnected?.();
      })
      .catch((err) => {
        console.error("❌ SignalR connection failed: ", err);
      });

    connection.onclose(() => {
      console.warn("🔌 Disconnected from ChatHub");
      setIsConnected(false);
      callbacks.onDisconnected?.();
    });

    return () => {
      connection.stop();
    };
  }, [token]);

  const sendMessageToModel = useCallback(
    (chatId: string, message: CustomClientMessage) => {
        console.log("state of connectionRef:", connectionRef.current?.state);
        if (connectionRef.current?.state === HubConnectionState.Connected) {
          console.log("Sending message to model:", message);
        connectionRef.current
          .invoke("SendMessageToModel", chatId, message)
          .catch((err) => {
            console.error("SendMessageToModel error:", err);
          });
      }
    },
    []
  );

  return {
    sendMessageToModel,
    isConnected,
  };
}
