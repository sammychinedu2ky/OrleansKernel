'use client';
import Image from "next/image";
import Input from "./components/Input";
import Sidebar from "./components/SideBar";
import { useEffect, useRef, useState } from "react";
import { SignedIn } from "@clerk/nextjs";
import { CustomClientMessage, useChatHub } from "./hooks/chat-hook";
import useFetch from "./hooks/fetch-hook";
import { v7 as uuidv7 } from 'uuid';
import { json } from "stream/consumers";
export default function Home() {
  const fetcher = useFetch();
    const [chatId, setChatId] = useState<string>(uuidv7());
    const inputBarRef = useRef<HTMLDivElement>(null);
    const [messageReceived, setMessageReceived] = useState(false);
    const [isConnectedToWebSocket, setIsConnectedToWebSocket] = useState(false);
    const [messages, setMessages] = useState<CustomClientMessage[]>([]);
    const {sendMessageToModel} = useChatHub({
        onReceiveMessage: (chatId: string, message: CustomClientMessage) => {
            setMessages((prev) => [...prev, message]);
            setMessageReceived(true);
        },
        onConnected: () => {
            setIsConnectedToWebSocket(true);
        },
        onDisconnected: () => {
            setIsConnectedToWebSocket(false);
        },
    });
      const addMessage = (message: CustomClientMessage) => {
        setMessages(prev => [...prev, message]);
    };
    useEffect(() => {
        function updateInputBarPosition() {
            const chatCard = document.getElementById("chat-card");
            const inputBar = inputBarRef.current;
            if (chatCard && inputBar) {
                const rect = chatCard.getBoundingClientRect();
                inputBar.style.left = rect.left + "px";
                inputBar.style.width = rect.width + "px";
            }
        }
        updateInputBarPosition();
        window.addEventListener("resize", updateInputBarPosition);
        return () => window.removeEventListener("resize", updateInputBarPosition);
    }, []);


    // useEffect( () => {
    //     if (chatId) {
    //         fetcher('/api/chat/' + chatId, {
    //       method: 'GET',
    //         })
    //         .then(response => response.json())
    //         .then(data => {
    //       // Handle the fetched data
    //       console.log(data);
    //       setMessages(data || []);
    //         });
    //     }
    // }, [chatId, fetcher]);

    return (
        <div className="text-black">
            <div className="flex justify-between container m-auto border-2 min-h-screen">
                <SignedIn>
                    <Sidebar />
                </SignedIn>
                <div
                    id="chat-card"
                    className="bg-white rounded-lg shadow-lg p-8 w-full relative flex flex-col"
                >
                    <h1 className="text-3xl font-bold mb-4">Welcome to UtilGPT</h1>
                    <p className="mb-4">Tell me what you'll like to do with your files.</p>
                    <hr className="my-6 border-gray-300" />
                    {/* ...other content... */}
                    {/* I need a chat component that renders the messages between the user and assistant */}
                    <div className="flex-1 overflow-y-auto">
                        {messages.map((message, index) => (
                            <div key={index} className={`mb-4 ${message.role === 'user' ? 'text-right' : 'text-left'}`}>
                                <div className={`inline-block px-4 py-2 rounded-lg ${message.role === 'user' ? 'bg-blue-500 text-white' : 'bg-gray-200 text-black'}`}>
                                    {message.text}
                                </div>
                                {message.files && message.files.length > 0 && (
                                    <div className="mt-2">
                                        {message.files.map((file, fileIndex) => (
                                            <a key={fileIndex} href={file.fileId} className="text-blue-600 hover:underline">
                                                {file.fileName}
                                            </a>
                                        ))}
                                    </div>
                                )}
                            </div>
                        ))}
                    </div>
                </div>
                <div
                    ref={inputBarRef}
                    className="fixed bottom-0 flex justify-center items-center z-50 py-4"
                    style={{ left: 0, width: "100%" }}
                >
                    <div className="w-full max-w-2xl">
                        <Input chatId={chatId} messageReceived={messageReceived} sendMessageToModel={sendMessageToModel} isConnectedToWebSocket={isConnectedToWebSocket}
                         onMessageHandled={() => setMessageReceived(false)} />
                    </div>
                </div>
            </div>
        </div>
    );
}

