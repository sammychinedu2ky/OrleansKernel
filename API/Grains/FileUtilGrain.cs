using System.ComponentModel;
using System.IO.Compression;
using System.Linq.Expressions;
using System.Text.Json;
using API.Hubs;
using API.Util;
using Azure.AI.OpenAI;
using ImageMagick;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Functions;
using Microsoft.SemanticKernel.Memory;
using BindingFlags = System.Reflection.BindingFlags; // Add this if InMemoryVectorStore is from Semantic Kernel Memory
using Kernel = Microsoft.SemanticKernel.Kernel;

#pragma warning disable SKEXP0130 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
namespace API.Grains;

public interface IFileUtilGrain : IGrainWithStringKey
{
    Task<FileMessage> ConvertFromImageToPdf(string fileId);
    Task<FileMessage> ConvertFromPdfToImage(string fileId);
    Task<CustomClientMessage> SendMessageAsync(CustomClientMessage message);
}

[GenerateSerializer]
public class ThreadState
{
    [Id(0)] public ChatHistoryAgentThread Thread { get; set; }
}

public class FileUtilGrain(
    [PersistentState("history", "default")]
        IPersistentState<ThreadState> history, Kernel _kernel, ILogger<FileUtilGrain> _logger) : Grain, IFileUtilGrain

{
    private readonly Kernel kernel = _kernel;
    private readonly ILogger<FileUtilGrain> logger = _logger;
    private ChatCompletionAgent _agent;

    public IPersistentState<ThreadState> threadState = history;

    public async Task<CustomClientMessage> SendMessageAsync(CustomClientMessage message)
    {
        List<AgentResponseItem<ChatMessageContent>> res = new();
        await foreach (var msg in _agent.InvokeAsync(message.ToString(), threadState.State.Thread)) res.Add(msg);
        var response = res.LastOrDefault().Message.Content;
        threadState.State.Thread = (ChatHistoryAgentThread)res.LastOrDefault().Thread;
        await threadState.WriteStateAsync();
        logger.LogInformation(JsonSerializer.Serialize(threadState.State.Thread.ChatHistory));
        return JsonSerializer.Deserialize<CustomClientMessage>(response);
    }


    [KernelFunction("convert_from_pdf_to_image")]
    [Description("Convert a PDF to an image for single-page PDFs or a ZIP file for multi-page PDFs")]
    public async Task<FileMessage> ConvertFromPdfToImage(string pdfId)
    {
        var filePath = Path.Combine(RetrieveBlobFolder.Get(), pdfId);
        if (!File.Exists(filePath))
            return new FileMessage
            {
                FileId = pdfId,
                FileName = "Error",
                FileType = "text/plain",
                Text = $"File with ID {pdfId} not found."
            };

        var outputFileId = Guid.NewGuid().ToString();
        string outputFileName;
        string outputFileType;
        var outputFilePath = Path.Combine(RetrieveBlobFolder.Get(), outputFileId);

        // Load all pages of the PDF with Magick.NET
        using var collection = new MagickImageCollection();
        collection.Read(filePath, new MagickReadSettings
        {
            Density = new Density(300, 300), // 300 DPI for good quality
            Format = MagickFormat.Pdf
        });

        var pageCount = collection.Count;

        if (pageCount == 1)
        {
            // Single page: save as PNG
            outputFileName = $"{outputFileId}.png";
            outputFileId = outputFileName;
            outputFileType = "image/png";
            outputFilePath = Path.Combine(RetrieveBlobFolder.Get(), outputFileName);

            using var pageImage = (MagickImage)collection[0].Clone();
            await pageImage.WriteAsync(outputFilePath, MagickFormat.Png);
        }
        else
        {
            // Multi-page: save each as PNG in a ZIP
            outputFileName = $"{outputFileId}.zip";
            outputFileId = outputFileName;
            outputFileType = "application/zip";
            outputFilePath = Path.Combine(RetrieveBlobFolder.Get(), outputFileName);

            using var zipStream = new FileStream(outputFilePath, FileMode.Create);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true);

            for (var i = 0; i < pageCount; i++)
            {
                var entry = archive.CreateEntry($"page_{i + 1}.png", CompressionLevel.Optimal);
                using var entryStream = entry.Open();

                using var pageImage = (MagickImage)collection[i].Clone();
                await pageImage.WriteAsync(entryStream, MagickFormat.Png);
            }
        }

        return new FileMessage
        {
            FileId = $"{outputFileId}",
            FileName = outputFileName,
            FileType = outputFileType,
            Text = $"Converted PDF with {pageCount} page(s) to {(pageCount == 1 ? "image" : "ZIP archive")}"
        };
    }

    [KernelFunction("convert_from_image_to_pdf")]
    [Description("Convert an image to a PDF using ImageMagick.")]
    public async Task<FileMessage> ConvertFromImageToPdf(string fileId)
    {
        var filePath = Path.Combine(RetrieveBlobFolder.Get(), fileId);
        if (!File.Exists(filePath))
            return new FileMessage
            {
                FileId = fileId,
                FileName = "Error",
                FileType = "text/plain",
                Text = $"File with ID {fileId} not found."
            };

        // Generate output file details
        var outputFileId = Guid.NewGuid().ToString();
        var outputFileName = $"{outputFileId}.pdf";
        outputFileId = outputFileName;
        var outputFileType = "application/pdf";
        var outputFilePath = Path.Combine(RetrieveBlobFolder.Get(), outputFileName);

        try
        {
            // Load the image using Magick.NET
            using var image = new MagickImage(filePath);

            // Set DPI for the output PDF (optional, for quality control)
            image.Density = new Density(300, 300); // 300 DPI for good quality

            // Create a MagickImageCollection to handle the conversion
            using var collection = new MagickImageCollection();
            collection.Add(image);

            // Save the image as a PDF
            await collection.WriteAsync(outputFilePath, MagickFormat.Pdf);

            return new FileMessage
            {
                FileId = outputFileId,
                FileName = outputFileName,
                FileType = outputFileType,
                Text = $"Converted image to PDF with dimensions {image.Width}x{image.Height} pixels"
            };
        }
        catch (MagickException ex)
        {
            logger.LogError($"Error converting image to PDF: {ex.Message}");
            return new FileMessage
            {
                FileId = fileId,
                FileName = "Error",
                FileType = "text/plain",
                Text = $"Failed to convert image to PDF: {ex.Message}"
            };
        }
    }


    [KernelFunction("merge_pdfs")]
    [Description("Merge multiple PDF files into a single PDF.")]
    public async Task<FileMessage> MergePdfs(List<string> pdfIds)
    {
        if (pdfIds == null || pdfIds.Count == 0)
        {
            return new FileMessage
            {
                FileId = Guid.NewGuid().ToString(),
                FileName = "Error",
                FileType = "text/plain",
                Text = "No PDF IDs provided for merging."
            };
        }

        var outputFileId = Guid.NewGuid().ToString();
        var outputFileName = $"{outputFileId}.pdf";
        outputFileId = outputFileName;
        var outputFileType = "application/pdf";
        var outputFilePath = Path.Combine(RetrieveBlobFolder.Get(), outputFileName);

        try
        {
            using var outputDocument = new MagickImageCollection();

            foreach (var pdfId in pdfIds)
            {
                var filePath = Path.Combine(RetrieveBlobFolder.Get(), pdfId);
                if (!File.Exists(filePath))
                {
                    return new FileMessage
                    {
                        FileId = Guid.NewGuid().ToString(),
                        FileName = "Error",
                        FileType = "text/plain",
                        Text = $"File with ID {pdfId} not found."
                    };
                }

                var readSettings = new MagickReadSettings
                {
                    Density = new Density(300, 300), // 300 DPI for high quality
                    Format = MagickFormat.Pdf
                };
                using var inputDocument = new MagickImageCollection();
                inputDocument.Read(filePath, readSettings);

                foreach (var page in inputDocument)
                {
                    outputDocument.Add(page.Clone());
                }
            }

            await outputDocument.WriteAsync(outputFilePath, MagickFormat.Pdf);

            return new FileMessage
            {
                FileId = outputFileId,
                FileName = outputFileName,
                FileType = outputFileType,
                Text = $"Merged {pdfIds.Count} PDFs into a single document."
            };
        }
        catch (MagickException ex)
        {
            logger.LogError($"Error merging PDFs: {ex.Message}");
            return new FileMessage
            {
                FileId = Guid.NewGuid().ToString(),
                FileName = "Error",
                FileType = "text/plain",
                Text = $"Failed to merge PDFs: {ex.Message}"
            };
        }
    }
    [KernelFunction("convert_docx_to_pdf")]
    [Description("Fake: Convert a DOCX file to PDF (dummy implementation)")]
    public Task<FileMessage> ConvertDocxToPdf(string fileId)
    {
        return Task.FromResult(new FileMessage
        {
            FileId = fileId,
            FileName = "dummy.pdf",
            FileType = "application/pdf",
            Text = $"[FAKE] Converted DOCX file {fileId} to PDF."
        });
    }

    [KernelFunction("extract_text_from_pdf")]
    [Description("Fake: Extract text from a PDF file (dummy implementation)")]
    public Task<FileMessage> ExtractTextFromPdf(string fileId)
    {
        return Task.FromResult(new FileMessage
        {
            FileId = fileId,
            FileName = "dummy.txt",
            FileType = "text/plain",
            Text = $"[FAKE] Extracted text from PDF file {fileId}."
        });
    }

    [KernelFunction("summarize_file_content")]
    [Description("Fake: Summarize the content of a file (dummy implementation)")]
    public Task<FileMessage> SummarizeFileContent(string fileId)
    {
        return Task.FromResult(new FileMessage
        {
            FileId = fileId,
            FileName = "summary.txt",
            FileType = "text/plain",
            Text = $"[FAKE] Summary for file {fileId}: This is a dummy summary."
        });
    }

    [KernelFunction("convert_txt_to_csv")]
    [Description("Fake: Convert a TXT file to CSV (dummy implementation)")]
    public Task<FileMessage> ConvertTxtToCsv(string fileId)
    {
        return Task.FromResult(new FileMessage
        {
            FileId = fileId,
            FileName = "dummy.csv",
            FileType = "text/csv",
            Text = $"[FAKE] Converted TXT file {fileId} to CSV."
        });
    }

    [KernelFunction("detect_file_language")]
    [Description("Fake: Detect the language of a file's content (dummy implementation)")]
    public Task<FileMessage> DetectFileLanguage(string fileId)
    {
        return Task.FromResult(new FileMessage
        {
            FileId = fileId,
            FileName = "language.txt",
            FileType = "text/plain",
            Text = $"[FAKE] Detected language for file {fileId}: English (dummy)."
        });
    }
    [KernelFunction("split_pdf")]
    [Description("Fake: Split a PDF into individual pages (dummy implementation)")]
    public Task<FileMessage> SplitPdf(string fileId)
    {
        return Task.FromResult(new FileMessage
        {
            FileId = fileId,
            FileName = "split_pages.zip",
            FileType = "application/zip",
            Text = $"[FAKE] Split PDF {fileId} into individual pages."
        });
    }

    [KernelFunction("compress_image")]
    [Description("Fake: Compress an image file (dummy implementation)")]
    public Task<FileMessage> CompressImage(string fileId)
    {
        return Task.FromResult(new FileMessage
        {
            FileId = fileId,
            FileName = "compressed.jpg",
            FileType = "image/jpeg",
            Text = $"[FAKE] Compressed image {fileId}."
        });
    }

    [KernelFunction("resize_image")]
    [Description("Fake: Resize an image file (dummy implementation)")]
    public Task<FileMessage> ResizeImage(string fileId)
    {
        return Task.FromResult(new FileMessage
        {
            FileId = fileId,
            FileName = "resized.png",
            FileType = "image/png",
            Text = $"[FAKE] Resized image {fileId} to 512x512."
        });
    }

    [KernelFunction("convert_csv_to_xlsx")]
    [Description("Fake: Convert a CSV file to XLSX (dummy implementation)")]
    public Task<FileMessage> ConvertCsvToXlsx(string fileId)
    {
        return Task.FromResult(new FileMessage
        {
            FileId = fileId,
            FileName = "dummy.xlsx",
            FileType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            Text = $"[FAKE] Converted CSV {fileId} to XLSX."
        });
    }

    [KernelFunction("extract_metadata")]
    [Description("Fake: Extract metadata from a file (dummy implementation)")]
    public Task<FileMessage> ExtractMetadata(string fileId)
    {
        return Task.FromResult(new FileMessage
        {
            FileId = fileId,
            FileName = "metadata.json",
            FileType = "application/json",
            Text = $"[FAKE] Extracted metadata from file {fileId}."
        });
    }
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var agentKernel = kernel.Clone();
        var embeddingGenerator = agentKernel.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var httpLogger = agentKernel.Services.GetRequiredService<ILogger<HttpLoggingHandler>>();
        agentKernel.Plugins.AddFromObject(this);
        _agent = new ChatCompletionAgent
        {
            Name = "FileUtilAgent",
            Description = "A file utility agent",
            Instructions =
                "You are a helpful file utility agent that performs some file conversion workflows using the available tools. No matter the question asked always return using the CustomClientMessage format. ",
            Kernel = agentKernel,
            Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                ResponseFormat = typeof(CustomClientMessage)
            }),
            UseImmutableKernel = true,
        };
        if (threadState.State.Thread == null) threadState.State.Thread = new();
        threadState.State.Thread.AIContextProviders.Add(new ContextualFunctionProvider(
        vectorStore: new InMemoryVectorStore(new InMemoryVectorStoreOptions() { EmbeddingGenerator = embeddingGenerator }),
        vectorDimensions: 1536,
        maxNumberOfFunctions: 5,
        functions: AvailableFunctions()
        // options: new ContextualFunctionProviderOptions
        // {
        //     NumberOfRecentMessagesInContext = 2
           
        // }
       ));
        await base.OnActivateAsync(cancellationToken);
    }

    private List<AIFunction> AvailableFunctions()
    {
        var functions = this.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttributes(typeof(KernelFunctionAttribute), false).Any())
            .Select(m =>
            {
                var attr = (KernelFunctionAttribute)m.GetCustomAttributes(typeof(KernelFunctionAttribute), false).FirstOrDefault();
                var descAttr = (DescriptionAttribute)m.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault();
                var name = attr?.Name ?? m.Name;
                var description = descAttr?.Description ?? "";
                // Create delegate for the method
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description))
                    throw new InvalidOperationException($"KernelFunction '{m.Name}' must have both a name and a description.");

                // Handle methods with any number of parameters (including zero)
                var parameterTypes = m.GetParameters().Select(p => p.ParameterType).ToList();
                parameterTypes.Add(m.ReturnType); // Add return type at the end

                var delegateType = Expression.GetDelegateType(parameterTypes.ToArray());

                var del = Delegate.CreateDelegate(delegateType, this, m, false);

                return AIFunctionFactory.Create(del, name, description);
            })
            .ToList();

        return functions;
    }


}
