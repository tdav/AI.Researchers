using AiResearchers.Core.Agents;
using AiResearchers.Core.Interview;
using AiResearchers.Infrastructure;
using AiResearchers.Infrastructure.Llm;
using AiResearchers.Infrastructure.Orchestration;
using AiResearchers.Infrastructure.Reporting;
using AiResearchers.Infrastructure.Research;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddLlm(builder.Configuration);
builder.Services.AddInterview();
builder.Services.AddResearchSources(builder.Configuration);
builder.Services.AddAgents();
builder.Services.AddOrchestration();
builder.Services.AddReporting();
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapGet("/", () => Results.Redirect("/Dashboard"));

app.Run();
