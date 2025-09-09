# UtilGPT

UtilGPT leverages [Semantic Kernel](https://github.com/microsoft/semantic-kernel) and [Microsoft Orleans](https://github.com/dotnet/orleans) to build a distributed agent workflow for various file manipulation tasks. This architecture enables scalable, resilient, and intelligent automation across different file processing scenarios.

## Requirements

- [.NET 9+](https://dotnet.microsoft.com/)
- [Ghostscript](https://www.ghostscript.com/) (for file manipulation tasks)

### Installing Ghostscript

On macOS, you can install Ghostscript using Homebrew:

```sh
brew install ghostscript
```

To verify if Ghostscript is installed, run:

```sh
gs --version
```

If you see a version number, Ghostscript is installed correctly.

#APIs Keys needed:
- Clerk
- Azure OpenAI
