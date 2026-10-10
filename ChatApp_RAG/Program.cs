using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using ChatApp_RAG.Components;
using ChatApp_RAG.Services;
using ChatApp_RAG.Services.Ingestion;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

//var githubPat = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

var apiKey =
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException(
        "Missing Azure OpenAI API key.");

// ------------------------------------------------------------
// Azure OpenAI configuration
// ------------------------------------------------------------

var azureOpenAIEndpoint =
    builder.Configuration["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException(
        "Missing configuration: AzureOpenAI:Endpoint");

var chatDeployment =
    builder.Configuration["AzureOpenAI:ChatDeployment"]
    ?? throw new InvalidOperationException(
        "Missing configuration: AzureOpenAI:ChatDeployment");

var embeddingDeployment =
    builder.Configuration["AzureOpenAI:EmbeddingDeployment"]
    ?? throw new InvalidOperationException(
        "Missing configuration: AzureOpenAI:EmbeddingDeployment");

// ------------------------------------------------------------
// Azure authentication
// ------------------------------------------------------------

var credential = new DefaultAzureCredential();

// ------------------------------------------------------------
// Azure OpenAI client
// ------------------------------------------------------------

//var azureOpenAIClient = new AzureOpenAIClient(
//    new Uri(azureOpenAIEndpoint),
//    credential);

var azureOpenAIClient = new AzureOpenAIClient(
    new Uri(azureOpenAIEndpoint),
    new AzureKeyCredential(apiKey));

// ------------------------------------------------------------
// Chat model
// ------------------------------------------------------------

var chatClient = azureOpenAIClient
    .GetChatClient(chatDeployment)
    .AsIChatClient();

// ------------------------------------------------------------
// Embedding model
// ------------------------------------------------------------

var embeddingGenerator = azureOpenAIClient
    .GetEmbeddingClient(embeddingDeployment)
    .AsIEmbeddingGenerator();

var vectorStorePath = Path.Combine(AppContext.BaseDirectory, "vector-store.db");
var vectorStoreConnectionString = $"Data Source={vectorStorePath}";
builder.Services.AddSqliteVectorStore(_ => vectorStoreConnectionString);
builder.Services.AddSqliteCollection<string, IngestedChunk>(IngestedChunk.CollectionName, vectorStoreConnectionString);

builder.Services.AddSingleton<DataIngestor>();
builder.Services.AddSingleton<SemanticSearch>();
builder.Services.AddKeyedSingleton("ingestion_directory", new DirectoryInfo(Path.Combine(builder.Environment.WebRootPath, "Data")));
builder.Services.AddChatClient(chatClient).UseFunctionInvocation().UseLogging();
builder.Services.AddEmbeddingGenerator(embeddingGenerator);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.UseStaticFiles();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
