'use client'
import { SignInButton, SignUpButton, SignedIn, SignedOut, UserButton } from '@clerk/nextjs';

export default function AuthButtons() {
    return (
        <>
            <SignedOut>
                <SignInButton>
                    <button className="rounded-sm font-medium text-sm sm:text-base h-10 sm:h-12 px-4 sm:px-5 cursor-pointer">
                        Sign In
                    </button>
                </SignInButton>
                <SignUpButton>
                    <button className="bg-amber-600 rounded-sm font-medium text-sm sm:text-base h-10 sm:h-12 px-4 sm:px-5 cursor-pointer">
                        Sign Up
                    </button>
                </SignUpButton>
            </SignedOut>
            <SignedIn>
                <UserButton />
            </SignedIn>
        </>
    );
}
