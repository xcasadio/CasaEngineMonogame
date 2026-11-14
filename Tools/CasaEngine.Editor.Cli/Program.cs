using CasaEngine.Editor.Cli;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

return await CliApp.RunAsync(args, Console.Out, Console.Error, cancellationToken: cancellation.Token);
