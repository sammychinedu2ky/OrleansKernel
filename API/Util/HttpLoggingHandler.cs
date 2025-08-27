using System.Text;
using Microsoft.Extensions.Http;

namespace API.Util;

public class HttpLoggingHandler : DelegatingHandler
{
    private readonly ILogger<HttpLoggingHandler> _logger;

    public HttpLoggingHandler(ILogger<HttpLoggingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Log the request
        await LogRequestAsync(request);

        // Send the request and get the response
        var response = await base.SendAsync(request, cancellationToken);

        // Log the response
        await LogResponseAsync(response);

        return response;
    }

    private async Task LogRequestAsync(HttpRequestMessage request)
    {
        var logBuilder = new StringBuilder();
        logBuilder.AppendLine("=== Outgoing HTTP Request ===Custom");
        logBuilder.AppendLine($"Method: {request.Method}");
        logBuilder.AppendLine($"URI: {request.RequestUri}");
        logBuilder.AppendLine("Headers:");
        // foreach (var header in request.Headers)
        // {
        //     logBuilder.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
        // }

        if (request.Content != null)
        {
            var content = await request.Content.ReadAsStringAsync(default);
            logBuilder.AppendLine("Body:");
            logBuilder.AppendLine(content);
        }
        else
        {
            logBuilder.AppendLine("Body: <none>");
        }

        _logger.LogInformation(logBuilder.ToString());
    }

    private async Task LogResponseAsync(HttpResponseMessage response)
    {
        var logBuilder = new StringBuilder();
        logBuilder.AppendLine("=== HTTP Response ===Custom");
        logBuilder.AppendLine($"Status Code: {response.StatusCode} ({(int)response.StatusCode})");
        logBuilder.AppendLine("Headers:");
        // foreach (var header in response.Headers)
        // {
        //     logBuilder.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
        // }

        if (response.Content != null)
        {
            var content = await response.Content.ReadAsStringAsync(default);
            logBuilder.AppendLine("Body:");
            logBuilder.AppendLine(content);
        }
        else
        {
            logBuilder.AppendLine("Body: <none>");
        }

        _logger.LogInformation(logBuilder.ToString());
    }
}

public class CustomHttpMessageHandlerBuilder : HttpMessageHandlerBuilder
{
    private readonly ILogger<HttpLoggingHandler> _logger;

    public CustomHttpMessageHandlerBuilder(ILogger<HttpLoggingHandler> logger, IServiceProvider services)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public override string Name { get; set; } = string.Empty;

    public override HttpMessageHandler PrimaryHandler { get; set; } = new HttpClientHandler();

    public override IList<DelegatingHandler> AdditionalHandlers { get; } = new List<DelegatingHandler>();

    public override IServiceProvider Services { get; }

    public override HttpMessageHandler Build()
    {
        var loggingHandler = new HttpLoggingHandler(_logger)
        {
            InnerHandler = PrimaryHandler ?? new HttpClientHandler()
        };

        HttpMessageHandler handler = loggingHandler;
        foreach (var additionalHandler in AdditionalHandlers)
        {
            additionalHandler.InnerHandler = handler;
            handler = additionalHandler;
        }

        return handler;
    }
}