using System.ComponentModel;
using System.IO.Compression;
using System.Text.Json;
using API.Hubs;
using API.Util;
using ImageMagick;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Kernel = Microsoft.SemanticKernel.Kernel;

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

public class FileUtilGrain : Grain, IFileUtilGrain
{
    private readonly Kernel kernel;
    private readonly ILogger<FileUtilGrain> logger;
    private ChatCompletionAgent _agent;

    public IPersistentState<ThreadState> threadState;

    public FileUtilGrain(
        [PersistentState("history", "default")]
        IPersistentState<ThreadState> history, Kernel _kernel, ILogger<FileUtilGrain> _logger)
    {
        threadState = history;
        kernel = _kernel;
        logger = _logger;
    }

    public async Task<CustomClientMessage> SendMessageAsync(CustomClientMessage message)
    {
        List<AgentResponseItem<ChatMessageContent>> res = new();
        await foreach (var msg in _agent.InvokeAsync(message.ToString(), threadState.State.Thread)) res.Add(msg);
        var response = res.LastOrDefault().Message.Content;
        threadState.State.Thread = (ChatHistoryAgentThread)res.LastOrDefault().Thread;
        Console.WriteLine("swacky");
        Console.WriteLine(response);
        logger.LogInformation(JsonSerializer.Serialize(threadState.State.Thread.ChatHistory));
        await threadState.WriteStateAsync();
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
    [Description("Convert an image to a PDF using ImageMagick. Cross-platform compatible.")]
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

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var agentKernel = kernel.Clone();
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
            })
        };
        await base.OnActivateAsync(cancellationToken);
    }
}