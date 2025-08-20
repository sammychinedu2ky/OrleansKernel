using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace API.Plugins;

public class ChatPlugin
{
    [KernelFunction("get_age")]
    [Description("Get the age of the user.")]
    public Task<int> MyAgeAsync()
    {
        Console.WriteLine("get_age function called");
        return Task.FromResult(23);
    }

    [KernelFunction("who_am_i")]
    [Description("Get the name of the user.")]
    public Task<string> WhoAmIAsync()
    {
        Console.WriteLine("who_am_i function called");
        return Task.FromResult("I an swacblooms");
    }
}