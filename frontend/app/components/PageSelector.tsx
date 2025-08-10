"use client";
import { usePathname } from "next/navigation";
import Link from "next/link";
import { SignedIn } from "@clerk/nextjs";
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faComments, faDownload } from '@fortawesome/free-solid-svg-icons';

export default function PageSelector() {
    const pathname = usePathname();
    return (
        <span className="ml-8 text-sm text-gray-700">
            <Link
                href="/"
                className={`inline-flex items-center gap-2 mr-6 text-base font-medium cursor-pointer pb-1 ${pathname === "/" ? "border-b-2 border-amber-600" : ""} hover:border-b-2 hover:border-amber-600`}
            >
                <FontAwesomeIcon icon={faComments} className="h-5 w-5 text-gray-500" aria-hidden="true" />
                Chat
            </Link>
            <SignedIn>
                <Link
                    href="/downloads"
                    className={`inline-flex items-center gap-2 text-base font-medium cursor-pointer pb-1 ${pathname === "/downloads" ? "border-b-2 border-amber-600" : ""} hover:border-b-2 hover:border-amber-600`}
                >
                    <FontAwesomeIcon icon={faDownload} className="h-5 w-5 text-gray-500" aria-hidden="true" />
                    Downloads
                </Link>
            </SignedIn>
        </span>
    );
}
