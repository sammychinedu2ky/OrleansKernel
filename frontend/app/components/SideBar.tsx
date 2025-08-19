"use client";
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faCommentDots, faPlus } from '@fortawesome/free-solid-svg-icons';
import useFetch from '../hooks/fetch-hook';
import { useAuth } from '@clerk/nextjs';
import { useEffect, useState } from 'react';

export default function SideBar() {
    // use my fetch-hook to make a request to /api/chat/chatpages, but do that only if the user is authenticated
    type ChatPage = {
        title?: string;
        chatId?: string;
    };
    const fetcher = useFetch();
    const { isSignedIn } = useAuth();
    const [chatPages, setChatPages] = useState<ChatPage[]>([]);
    useEffect(() => {
        if (isSignedIn) {
            fetcher('/api/chat/chatpages')
            .then((data) =>  data.json())
            .then((data) => {
                console.log(data);
                setChatPages(data);
            });
        }
    }, [isSignedIn, fetcher]);
    return (
        <>
            {isSignedIn && (
            <aside className="h-screen w-64 bg-gray-900 text-white flex flex-col border-r border-gray-800 sticky top-0">
                <div className="flex items-center justify-between px-4 py-4 border-b border-gray-800">
                <span className="text-lg font-bold">Chats</span>
                <button className="bg-gray-700 hover:bg-gray-600 rounded p-2">
                    <FontAwesomeIcon icon={faPlus} />
                </button>
                </div>
                <nav className="flex-1 overflow-y-auto px-2 py-4">
                {chatPages.length === 0 ? (
                    <span className="text-gray-400">No chats available.</span>
                ) : (
                    chatPages.map((chat) => (
                    <a
                        key={chat.chatId}
                        href={`/chats/${chat.chatId}`}
                        className="w-full flex items-center gap-3 px-3 py-2 mb-2 rounded-lg bg-gray-800 hover:bg-gray-700 text-left"
                    >
                        <FontAwesomeIcon icon={faCommentDots} className="text-gray-400" />
                        <span className="truncate">{chat.title || "Untitled Chat"}</span>
                    </a>
                    ))
                )}
                </nav>
                <div className="px-4 py-4 border-t border-gray-800 text-xs text-gray-400">
                <span> Sidebar Dummy</span>
                </div>
            </aside>
            )}
        </>
    );
}
