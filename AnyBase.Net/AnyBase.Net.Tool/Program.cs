using AnyBase.Net.Tool;

return await CliApplication.RunAsync(
    args,
    Console.IsInputRedirected ? Console.OpenStandardInput() : null,
    Console.OpenStandardOutput(),
    Console.Error);
