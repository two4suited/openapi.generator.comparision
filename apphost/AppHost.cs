using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = DistributedApplication.CreateBuilder(args);

var repoRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, ".."));
var typespecDir = Path.Combine(repoRoot, "typespec");
var aspnetSpecPath = Path.Combine(repoRoot, "aspnet", "TaskApi", "TaskApi.json");

var api = builder.AddProject<Projects.TaskApi>("taskapi")
    .WithHttpEndpoint(name: "http")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

api
    // Command 1: (re)generate the OpenAPI document from the TypeSpec contract.
    .WithCommand(
        name: "generate-from-typespec",
        displayName: "Generate OpenAPI (TypeSpec)",
        executeCommand: async _ =>
        {
            var install = await RunAsync("npm", "install", typespecDir);
            if (!install.ok) return CommandResults.Failure(install.output);
            var build = await RunAsync("npm", "run build", typespecDir);
            return build.ok
                ? CommandResults.Success()
                : CommandResults.Failure(build.output);
        },
        commandOptions: new CommandOptions { IconName = "DocumentArrowRight", Description = "Runs `tsp compile` -> typespec/openapi.yaml" })
    // Command 2: generate the OpenAPI document from the running ASP.NET API.
    .WithCommand(
        name: "generate-from-api",
        displayName: "Generate OpenAPI (from API)",
        executeCommand: async ctx =>
        {
            var url = await api.GetEndpoint("http").GetValueAsync(ctx.CancellationToken);
            using var http = new HttpClient();
            var json = await http.GetStringAsync($"{url}/openapi/v1.json", ctx.CancellationToken);
            await File.WriteAllTextAsync(aspnetSpecPath, json, ctx.CancellationToken);
            return CommandResults.Success();
        },
        commandOptions: new CommandOptions
        {
            IconName = "DocumentArrowDown",
            Description = "Fetches /openapi/v1.json -> aspnet/TaskApi/TaskApi.json",
            UpdateState = c => c.ResourceSnapshot.HealthStatus is HealthStatus.Healthy
                ? ResourceCommandState.Enabled
                : ResourceCommandState.Disabled,
        });

builder.AddProject<Projects.WebApp>("webapp")
    .WithHttpEndpoint(name: "http")
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("TaskApiUrl", api.GetEndpoint("http"))
    .WaitFor(api);

builder.Build().Run();

static async Task<(bool ok, string output)> RunAsync(string file, string args, string workDir)
{
    var psi = new ProcessStartInfo(file, args)
    {
        WorkingDirectory = workDir,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    using var p = Process.Start(psi)!;
    var stdout = p.StandardOutput.ReadToEndAsync();
    var stderr = p.StandardError.ReadToEndAsync();
    await p.WaitForExitAsync();
    return (p.ExitCode == 0, await stdout + await stderr);
}
