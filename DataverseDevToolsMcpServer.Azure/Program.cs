using DataverseDevToolsMcpServer.Azure.Middleware;
using DataverseDevToolsMcpServer.Tools;
using Microsoft.PowerPlatform.Dataverse.Client;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------
// Dataverse configuration — read from environment variables or
// appsettings.json.  In Azure App Service use the Application
// Settings panel (double-underscore separator):
//   Dataverse__EnvironmentUrl, Dataverse__TenantId, etc.
// ---------------------------------------------------------------
var environmentUrl = builder.Configuration["Dataverse:EnvironmentUrl"]
    ?? throw new InvalidOperationException(
        "Required configuration 'Dataverse:EnvironmentUrl' is missing. " +
        "Set it in appsettings.json or as an Azure App Service Application Setting.");

var tenantId = builder.Configuration["Dataverse:TenantId"]
    ?? throw new InvalidOperationException(
        "Required configuration 'Dataverse:TenantId' is missing.");

var clientId = builder.Configuration["Dataverse:ClientId"]
    ?? throw new InvalidOperationException(
        "Required configuration 'Dataverse:ClientId' is missing.");

var clientSecret = builder.Configuration["Dataverse:ClientSecret"]
    ?? throw new InvalidOperationException(
        "Required configuration 'Dataverse:ClientSecret' is missing.");

// ---------------------------------------------------------------
// MCP Server — scans the existing DataverseDevToolsMcpServer
// assembly to discover all [McpServerTool] decorated methods.
// app.MapMcp() below exposes the Streamable HTTP endpoint at /mcp.
// ---------------------------------------------------------------
builder.Services
    .AddMcpServer()
    .WithToolsFromAssembly(typeof(DataManagementTools).Assembly)
    .WithResourcesFromAssembly(typeof(DataManagementTools).Assembly);

// ---------------------------------------------------------------
// Dataverse ServiceClient — Service Principal (ClientSecret) only.
// Interactive OAuth is not supported in a cloud-hosted scenario.
// ---------------------------------------------------------------
builder.Services.AddSingleton(_ =>
{
    var connectionString =
        $"AuthType=ClientSecret;" +
        $"Url={environmentUrl};" +
        $"ClientId={clientId};" +
        $"ClientSecret={clientSecret};" +
        $"TenantId={tenantId}";

    var crm = new ServiceClient(connectionString);

    if (!crm.IsReady)
        throw new InvalidOperationException(
            $"Failed to connect to Dataverse at '{environmentUrl}'. " +
            $"Check your Client ID, Client Secret and Tenant ID. " +
            $"Inner error: {crm.LastError}");

    return crm;
});

var app = builder.Build();

// ---------------------------------------------------------------
// API Key middleware — validates X-Api-Key header on every request.
// If McpServer:ApiKey is empty the check is skipped (dev/test mode).
// ---------------------------------------------------------------
app.UseMiddleware<ApiKeyMiddleware>();

// Exposes the MCP Streamable HTTP endpoint at /mcp
app.MapMcp();

await app.RunAsync();
