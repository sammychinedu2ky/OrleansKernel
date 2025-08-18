using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Text.Json;
using System.IO.Compression;
using SixLabors.ImageSharp.PixelFormats;
using API.Hubs;
using API.Util;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SixLabors.ImageSharp;
using ImageMagick;
using System.IO.Compression;
using System.IO.Compression;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using PDFtoImage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;
using PdfiumViewer; // Assuming PdfiumViewer for PDF processing
using System.Drawing.Imaging;

using Color = SixLabors.ImageSharp.Color;
using Image = SixLabors.ImageSharp.Image;
using PdfDocument = PdfSharpCore.Pdf.PdfDocument;
using API.Endpoints;

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
    [Id(0)]
    public ChatHistoryAgentThread Thread { get; set; }
}

public class FileUtilGrain : Grain, IFileUtilGrain
{
    private ChatCompletionAgent _agent;
    private readonly Microsoft.SemanticKernel.Kernel kernel;
    private readonly ILogger<FileUtilGrain> logger;

    public IPersistentState<ThreadState> threadState;
    public FileUtilGrain(
        [PersistentState("history", "default")] IPersistentState<ThreadState> history, Microsoft.SemanticKernel.Kernel _kernel, ILogger<FileUtilGrain> _logger)
    {
        this.threadState = history;
        this.kernel = _kernel;
        this.logger = _logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var agentKernel = kernel.Clone();
        agentKernel.Plugins.AddFromObject(this);
        _agent = new ChatCompletionAgent()
        {
            Name = "FileUtilAgent",
            Description = "A file utility agent",
            Instructions =
                "You are a helpful file utility agent that performs some file conversion workflows using the available tools",
            Kernel = agentKernel,
            Arguments = new(new AzureOpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                ResponseFormat = typeof(CustomClientMessage),
            })
        };
        await base.OnActivateAsync(cancellationToken);
    }

    public async Task<CustomClientMessage> SendMessageAsync(CustomClientMessage message)
    {
        List<AgentResponseItem<ChatMessageContent>> res = new();
        await foreach (var msg in _agent.InvokeAsync(message.ToString(), threadState.State.Thread))
        {
            res.Add(msg);
        }
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
        {
            return new FileMessage
            {
                FileId = pdfId,
                FileName = "Error",
                FileType = "text/plain",
                Text = $"File with ID {pdfId} not found."
            };
        }

        string outputFileId = Guid.NewGuid().ToString();
        string outputFileName;
        string outputFileType;
        string outputFilePath = Path.Combine(RetrieveBlobFolder.Get(), outputFileId);

        // Load all pages of the PDF with Magick.NET
        using var collection = new MagickImageCollection();
        collection.Read(filePath, new MagickReadSettings
        {
            Density = new Density(300, 300), // 300 DPI for good quality
            Format = MagickFormat.Pdf
        });

        int pageCount = collection.Count;

        if (pageCount == 1)
        {
            // Single page: save as PNG
            outputFileName = $"{outputFileId}.png";
            outputFileType = "image/png";
            outputFilePath = Path.Combine(RetrieveBlobFolder.Get(), outputFileName);

            using var pageImage = (MagickImage)collection[0].Clone();
            await pageImage.WriteAsync(outputFilePath, MagickFormat.Png);
        }
        else
        {
            // Multi-page: save each as PNG in a ZIP
            outputFileName = $"{outputFileId}.zip";
            outputFileType = "application/zip";
            outputFilePath = Path.Combine(RetrieveBlobFolder.Get(), outputFileName);

            using var zipStream = new FileStream(outputFilePath, FileMode.Create);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true);

            for (int i = 0; i < pageCount; i++)
            {
                var entry = archive.CreateEntry($"page_{i + 1}.png", CompressionLevel.Optimal);
                using var entryStream = entry.Open();

                using var pageImage = (MagickImage)collection[i].Clone();
                await pageImage.WriteAsync(entryStream, MagickFormat.Png);
            }
        }

        return new FileMessage
        {
            FileId = outputFileId,
            FileName = outputFileName,
            FileType = outputFileType,
            Text = $"Converted PDF with {pageCount} page(s) to {(pageCount == 1 ? "image" : "ZIP archive")}"
        };
    }

    [KernelFunction("convert_from_image_to_pdf")]
    [Description("Convert an image to a PDF. Cross-platform compatible.")]
    public async Task<FileMessage> ConvertFromImageToPdf(string fileId)
    {
        var filePath = Path.Combine(RetrieveBlobFolder.Get(), fileId);
        if (!File.Exists(filePath))
        {
            return new FileMessage
            {
                FileId = fileId,
                FileName = "Error",
                FileType = "text/plain",
                Text = $"File with ID {fileId} not found."
            };
        }

        // Generate output file details
        string outputFileId = Guid.NewGuid().ToString();
        string outputFileName = $"{outputFileId}.pdf";
        string outputFileType = "application/pdf";
        string outputFilePath = Path.Combine(RetrieveBlobFolder.Get(), outputFileName);

        // Load the image using ImageSharp
        using var image = await Image.LoadAsync<Rgba32>(filePath);

        // Create a new PDF document
        using var pdfDocument = new PdfDocument();
        PdfPage page = pdfDocument.AddPage();

        // Set page size to match image dimensions (in points, 1 point = 1/72 inch)
        float dpi = 300f;
        page.Width = XUnit.FromInch((double)image.Width / dpi);
        page.Height = XUnit.FromInch((double)image.Height / dpi);

        // Draw the image on the PDF page using the original file path
        using (var xGraphics = XGraphics.FromPdfPage(page))
        using (var xImage = XImage.FromFile(filePath))
        {
            xGraphics.DrawImage(xImage, 0, 0, page.Width, page.Height);
        }

        // Save the PDF
        pdfDocument.Save(outputFilePath);

        return new FileMessage
        {
            FileId = outputFileId,
            FileName = outputFileName,
            FileType = outputFileType,
            Text = $"Converted image to PDF with dimensions {image.Width}x{image.Height} pixels"
        };
    }


}
