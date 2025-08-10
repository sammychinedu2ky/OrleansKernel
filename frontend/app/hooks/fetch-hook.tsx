import { useAuth } from "@clerk/nextjs"

export default function useFetch() {
    // Use `useAuth()` to access the `getToken()` method
    const { getToken } = useAuth()
    // Use `getToken()` to get the current session token
    const authenticatedFetch = async (input: RequestInfo, init?: RequestInit) => {
        // const token = await getToken({ template: "utilgptjwt" });
        const token = await getToken();
        const apiUrl = process.env.NEXT_PUBLIC_API_URL;
        const urlString = typeof input === "string" ? input : (input as Request).url;
        var url = new URL(urlString, apiUrl);
        const authInit = {
            ...init,
            headers: {
                ...(init?.headers || {}),
                Authorization: `Bearer ${token}`,
            },
        };
        console.log("my token is", token);
        return fetch(url, authInit); // Return the Response object
    };
    return authenticatedFetch;
}


