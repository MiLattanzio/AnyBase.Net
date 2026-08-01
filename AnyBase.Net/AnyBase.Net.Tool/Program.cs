using AnyBase.Net.Tool;

return await CliApplication.RunAsync(
    args,
    Console.IsInputRedirected ? Console.In : null,
    Console.Out,
    Console.Error);
