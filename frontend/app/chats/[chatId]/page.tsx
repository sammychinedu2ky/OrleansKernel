'use client';
import { SignedIn } from '@clerk/nextjs';
import { useParams } from 'next/navigation';

import { useEffect, useRef, useState } from 'react';
import Input from '@/app/components/Input';
import Sidebar from '@/app/components/SideBar';
import { CustomClientMessage, useChatHub } from '@/app/hooks/chat-hook';
import useFetch from '@/app/hooks/fetch-hook';
import { use } from 'react';


export default function ChatPage({
    params,
}: {
    params: Promise<{ chatId: string }>
}) {
    const { chatId } = use(params);
    const fetcher = useFetch();
    const fetchedRef = useRef(false);
    const inputBarRef = useRef<HTMLDivElement>(null);
    const [messageReceived, setMessageReceived] = useState(false);
    const [isConnectedToWebSocket, setIsConnectedToWebSocket] = useState(false);
    const [messages, setMessages] = useState<CustomClientMessage[]>([]);
    const [inputData, setInputData] = useState<CustomClientMessage | undefined>(undefined);
    const { sendMessageToModel } = useChatHub({
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
    const addMessage = (message: CustomClientMessage) => {
        setMessages(prev => [...prev, message]);
    };
    useEffect(() => {
        if (chatId && !fetchedRef.current) {
            fetchedRef.current = true; // Set the flag to true after the first fetch
            fetcher('/api/chat/' + chatId, {
                method: 'GET',
            })
                .then(response => response.json())
                .then(data => {
                    // Handle the fetched data
                    setMessages((prev) => [...data, ...prev]);
                });
        }
    }, [chatId]);
    useEffect(() => {
        if (sessionStorage.getItem('trigger') === chatId) {
            let message = JSON.parse(sessionStorage.getItem('message') || '{}');
            if (message && message.text) {
                setMessages((prev) => [...prev, message]);
                setMessageReceived(false);
                setInputData(message);
            }
            // remove from session Storage
            sessionStorage.removeItem('trigger');
            sessionStorage.removeItem('message');
        }
    }, [chatId]);
    const getDownloadLink  = (fileId: string) => {
       return `${process.env.NEXT_PUBLIC_API_URL}/api/download/${fileId}`;
    };
    return (
        <div className="text-black">
            <div className="flex justify-between container m-auto border-2 min-h-screen">
                <SignedIn>
                    <Sidebar />
                </SignedIn>
                <div
                    id="chat-card"
                    className="bg-white rounded-lg shadow-lg p-8  mt-10 w-full relative flex flex-col"
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
                                            <div key={fileIndex}>
                                                <a href={getDownloadLink(file.fileId)} className="text-blue-600 hover:underline">
                                                    {file.fileName}
                                                </a>
                                            </div>
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
                        <Input chatId={chatId} messageReceived={messageReceived} inputData={inputData === null ? undefined : inputData} sendMessageToModel={sendMessageToModel}
                            onUserMessage={addMessage}
                            isConnectedToWebSocket={isConnectedToWebSocket}
                            onMessageHandled={() => setMessageReceived(false)}
                        />
                    </div>
                </div>
            </div>
        </div>
    );
}