using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using API.Hubs;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Magentic;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace API.Grains;

public interface IChatGrain : IGrainWithStringKey
{
    Task<int> MyAgeAsync();
    Task<string> WhoAmIAsync();

    // Instead of returning the agent, let Orleans do the work
    Task<CustomClientMessage> SendMessageAsync(string userId, CustomClientMessage message);
}

public class ChatGrain(Kernel kernel) : Grain, IChatGrain
{
    private ChatCompletionAgent _agent;

    
    [Experimental("SKEXP0120")]
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var agentKernel = kernel.Clone();
        
        

        agentKernel.Plugins.AddFromObject(this);
        _agent = new ChatCompletionAgent()
        {
            Name = "Chat Agent",
            Instructions = "You are a helpful assistant. When the user asks a question, check the available tools and provide a response.",
            Kernel = agentKernel,
            Arguments = new(new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            })
        };

        return Task.CompletedTask;
    }
    [KernelFunction("get_age")]
    [Description("Get the age of the user.")]
    public Task<int> MyAgeAsync() => Task.FromResult(25);
    
    [KernelFunction("who_am_i")]
    [Description("Get the identity of the user.")]
    public Task<string> WhoAmIAsync() => Task.FromResult("I am a Swacblooms.");

    public async Task<CustomClientMessage> SendMessageAsync(string userId, CustomClientMessage message)
    {
        // Process with the internal agent
        var chatCompletionService = _agent.Kernel.GetRequiredService<IChatCompletionService>();

#pragma warning disable SKEXP0110
        var manager = new StandardMagenticManager(
            chatCompletionService,
            new OpenAIPromptExecutionSettings())
        {
            MaximumInvocationCount = 5,
            
        };

        var orchestration = new MagenticOrchestration<string, CustomClientMessage>(manager, _agent)
        {
            ResponseCallback = (ChatMessageContent response) =>
            {
                Console.WriteLine(response);
                Console.WriteLine("chickekekek");
                return ValueTask.CompletedTask;
            }
        };
        var runTime = new InProcessRuntime();
        await runTime.StartAsync();

        var result = await orchestration.InvokeAsync(message.Text, runTime);
        var resulty = await result.GetValueAsync();
        Console.WriteLine(resulty.Text);
        return await result.GetValueAsync();
#pragma warning restore SKEXP0110
    }
}
