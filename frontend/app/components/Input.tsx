
'use client';
import { useState } from "react";
import { useRouter } from "next/navigation";
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faPause, faPaperPlane, faPaperclip } from '@fortawesome/free-solid-svg-icons';
import { v7 as uuidv7 } from 'uuid';
import useFetch from "../hooks/fetch-hook";

export default function Input() {
    const [files, setFiles] = useState<File[]>([]);
    const [text, setText] = useState("");
    const fetcher = useFetch();
    const [loading, setLoading] = useState(false);
    const router = useRouter();

    // uuidv7 will be used for chat id

    const removeFile = (index: number) => {
        setFiles((prev) => prev.filter((_, i) => i !== index));
    };

    const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        if (e.target.files && e.target.files.length > 0) {
            setFiles((prev) => [...prev, ...Array.from(e.target.files!)]);
        }
    };

    const handleSend = async () => {
        const chatId = uuidv7();
        setLoading(true);
        console.log(files, text);
        // window.history.replaceState(null, '', `/chat/${chatId}`);
        const formData = new FormData();
        formData.append('text', text);
        formData.append('chatId', chatId);
        files.forEach((file) => {
            formData.append('files', file);
        });
        console.log("done appending files to formData");
        let response = await fetcher('/', {
            method: 'POST',
            body: formData,
        });
        let data = await response.json();
        console.log(data);
        //await new Promise((resolve) => setTimeout(resolve, 2000));
        setLoading(false);
        setText("");
        setFiles([]);
        // Generate a uuidv7 and update the URL to /chat/{id} without leaving the page or triggering navigation
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