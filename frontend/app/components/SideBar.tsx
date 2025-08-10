"use client";
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faCommentDots, faPlus } from '@fortawesome/free-solid-svg-icons';

export default function SideBar() {
    return (
        <aside className="h-screen w-64 bg-gray-900 text-white flex flex-col border-r border-gray-800">
            <div className="flex items-center justify-between px-4 py-4 border-b border-gray-800">
                <span className="text-lg font-bold">Chats</span>
                <button className="bg-gray-700 hover:bg-gray-600 rounded p-2">
                    <FontAwesomeIcon icon={faPlus} />
                </button>
            </div>
            <nav className="flex-1 overflow-y-auto px-2 py-4">
                {["How do I use Clerk?", "Upload PDF", "Summarize this file", "Generate code", "Chat with AI"].map((title, idx) => (
                    <button
                        key={idx}
                        className="w-full flex items-center gap-3 px-3 py-2 mb-2 rounded-lg bg-gray-800 hover:bg-gray-700 text-left"
                    >
                        <FontAwesomeIcon icon={faCommentDots} className="text-gray-400" />
                        <span className="truncate">{title}</span>
                    </button>
                ))}
            </nav>
            <div className="px-4 py-4 border-t border-gray-800 text-xs text-gray-400">
                <span>ChatGPT Sidebar Dummy</span>
            </div>
        </aside>
    );
}
