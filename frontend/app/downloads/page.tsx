

const downloads = [
    {
        name: "User Guide PDF",
        url: "/downloads/user-guide.pdf",
    },
    {
        name: "Release Notes",
        url: "/downloads/release-notes.txt",
    },
    {
        name: "Setup Installer",
        url: "/downloads/setup-installer.exe",
    },
];

export default function DownloadsPage() {
    return (
        <main style={{ padding: "2rem" }}>
            <h1>Downloads</h1>
            <ul>
                {downloads.map((file) => (
                    <li key={file.url}>
                        <a href={file.url} download>
                            {file.name}
                        </a>
                    </li>
                ))}
            </ul>
        </main>
    );
}