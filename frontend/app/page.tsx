'use client';
import Image from "next/image";
import Input from "./components/Input";
import Sidebar from "./components/SideBar";
import { useEffect, useRef } from "react";
import { SignedIn } from "@clerk/nextjs";

export default function Home() {
  const inputBarRef = useRef<HTMLDivElement>(null);

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
        </div>
      </div>
      <div
        ref={inputBarRef}
        className="fixed bottom-0 flex justify-center items-center z-50 py-4"
        style={{ left: 0, width: "100%" }}
      >
        <div className="w-full max-w-2xl">
          <Input />
        </div>
      </div>
    </div>
  );
}
