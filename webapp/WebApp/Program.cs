using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using WebApp.Components;
using AspNetClient = WebApp.Clients.AspNet.AspNetClient;
using TypeSpecClient = WebApp.Clients.TypeSpec.TypeSpecClient;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
// Aspire injects TaskApiUrl; falls back to the API's standalone port.
var apiUrl = builder.Configuration["TaskApiUrl"] ?? "http://localhost:5000";

// Two Kiota clients, each generated from a different OpenAPI document,
// both pointed at the same running Task API.
HttpClientRequestAdapter Adapter() =>
    new(new AnonymousAuthenticationProvider()) { BaseUrl = apiUrl };

builder.Services.AddSingleton(_ => new TypeSpecClient(Adapter()));
builder.Services.AddSingleton(_ => new AspNetClient(Adapter()));
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
