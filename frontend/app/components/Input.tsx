
'use client';
import { use, useCallback, useEffect, useState } from "react";
import { SignedIn, useAuth } from "@clerk/nextjs";
import { useRouter, usePathname } from "next/navigation";
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faPause, faPaperPlane, faPaperclip } from '@fortawesome/free-solid-svg-icons';
import { v7 as uuidv7 } from 'uuid';
import useFetch from "../hooks/fetch-hook";
import { CustomClientMessage, useChatHub } from "../hooks/chat-hook";
import { send } from "process";

interface InputProps {
    messageReceived?: boolean;
    inputData?: CustomClientMessage;
    chatId: string;
    sendMessageToModel: (chatId: string, message: CustomClientMessage) => void;
    isConnectedToWebSocket?: boolean;
}

export default function Input({ messageReceived, inputData, chatId,sendMessageToModel,isConnectedToWebSocket }: InputProps) {
    const [files, setFiles] = useState<File[]>([]);
    const [text, setText] = useState("");
    // bind a state to inputData if provided
    const [inputState, setInputState] = useState<CustomClientMessage | null>(inputData || null);
    const fetcher = useFetch();
   
    const [loading, setLoading] = useState(false);
    const router = useRouter();

    const pathName = usePathname();
    const { isSignedIn } = useAuth();
    // Stop loading if messageReceived changes to true
    useEffect(() => {
        if (messageReceived) {
            setLoading(false);
        }
    }, [messageReceived]);

    
    const removeFile = (index: number) => {
        setFiles((prev) => prev.filter((_, i) => i !== index));
    };

    const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        if (e.target.files && e.target.files.length > 0) {
            setFiles((prev) => [...prev, ...Array.from(e.target.files!)]);
        }
    };

    useEffect(() => {
        if (inputState &&  isConnectedToWebSocket) {
            sendMessageToModel( chatId, inputState);
        }
    }, [inputState, sendMessageToModel, isConnectedToWebSocket]);

    const handleSend = async () => {
        setLoading(true);
        console.log(files, text);
        const formData = new FormData();
        formData.append('chatId', chatId);
        files.forEach((file) => {
            formData.append('files', file);
        });
        console.log("done appending files to formData");
        try {
            let response = await fetcher('/api/chat', {
                method: 'POST',
                body: formData,
            });
            // CONSOLE STATUS
            console.log("Response status:", response.status);
            let data = await response.json();
            // got a sample request of this format
            let CustomClientMessage: CustomClientMessage = {
                text,
                role: "user", // default role is user
                files: data
            }
            console.log(data);
            setText("");
            setFiles([]);
            if (isSignedIn && !pathName.includes(`/chats/${chatId}`)) {
                // If the user is signed in and not already on the chat page, redirect to unique chat page
                sessionStorage.setItem('trigger', chatId);
                sessionStorage.setItem('message', JSON.stringify(CustomClientMessage));
                router.push(`/chats/${chatId}`);
            }
            else{
                setInputState(CustomClientMessage);
            }
        } catch (error) {
            console.error("Error sending message:", error);
            setLoading(false);
        }

    };

    return (
        <div className="flex items-center gap-2 border border-gray-300 rounded-lg px-3 py-2 w-full bg-white">
            {/* Text input */}
            <input
                type="text"
                value={text}
                onChange={(e) => setText(e.target.value)}
                placeholder="Ask anything"
                className="flex-1 outline-none bg-transparent text-gray-800"
                disabled={loading}
            />

            {/* File previews */}
            {files.length > 0 && (
                <div className="flex items-center gap-2 flex-wrap">
                    {files.map((file, index) => (
                        <div
                            key={index}
                            className="flex items-center gap-1 bg-gray-100 px-2 py-1 rounded-md text-sm"
                        >
                            <span className="truncate max-w-[120px]">{file.name}</span>
                            <button
                                onClick={() => removeFile(index)}
                                className="text-gray-500 hover:text-gray-800 cursor-pointer"
                                disabled={loading}
                            >
                                ✕
                            </button>
                        </div>
                    ))}
                </div>
            )}

            {/* File upload button */}
            <label className="cursor-pointer text-gray-600 hover:text-gray-800">
                <FontAwesomeIcon icon={faPaperclip} />
                <input
                    type="file"
                    name="files"
                    multiple
                    className="hidden"
                    onChange={handleFileChange}
                    disabled={loading}
                />
            </label>

            {/* Send button */}
            <button
                onClick={handleSend}
                className="bg-blue-500 text-white px-3 py-1 rounded-md hover:bg-blue-600 flex items-center justify-center cursor-pointer"
                disabled={loading}
            >
                {loading ? (
                    <FontAwesomeIcon icon={faPause} />
                ) : (
                    <FontAwesomeIcon icon={faPaperPlane} />
                )}
            </button>
        </div>
    );
}