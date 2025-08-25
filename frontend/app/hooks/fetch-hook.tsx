import { useAuth } from "@clerk/nextjs"
import { useCallback } from "react";

export default function useFetch() {
    const { getToken } = useAuth();
    const authenticatedFetch = useCallback(
        async (input: RequestInfo, init?: RequestInit) => {
            const token = await getToken();
            const apiUrl = process.env.NEXT_PUBLIC_API_URL;
            const urlString = typeof input === "string" ? input : (input as Request).url;
            const url = new URL(urlString, apiUrl);
            const authInit = {
                ...init,
                headers: {
                    ...(init?.headers || {}),
                    Authorization: `Bearer ${token}`,
                },
            };
            return fetch(url, authInit);
        },
        [getToken]
    );
    return authenticatedFetch;
}


