# Deploying to Azure App Service & connecting to Copilot Studio

## Prerequisites

| Item | Detail |
|------|--------|
| Azure subscription | Contributor or Owner on the target Resource Group |
| Azure CLI | `az --version` ≥ 2.50 |
| .NET 8 SDK | `dotnet --version` ≥ 8.0 |
| Dataverse App Registration | An Azure AD app with a Client Secret and the **Dataverse user** role assigned |

---

## 1 — Create the Azure App Service

```bash
# Variables — replace with your own values
RG="rg-dataverse-mcp"
LOCATION="eastus"
PLAN="plan-dataverse-mcp"
APP="dataverse-mcp-server"   # must be globally unique → becomes <APP>.azurewebsites.net

# Resource group
az group create --name $RG --location $LOCATION

# App Service Plan (Linux, B1 is enough to start)
az appservice plan create \
  --name $PLAN \
  --resource-group $RG \
  --sku B1 \
  --is-linux

# Web App (.NET 8)
az webapp create \
  --name $APP \
  --resource-group $RG \
  --plan $PLAN \
  --runtime "DOTNETCORE:8.0"
```

---

## 2 — Configure Application Settings

Azure App Service injects these as environment variables.
Use `__` (double underscore) as the config section separator.

```bash
az webapp config appsettings set \
  --name $APP \
  --resource-group $RG \
  --settings \
    Dataverse__EnvironmentUrl="https://<org>.crm.dynamics.com" \
    Dataverse__TenantId="<tenant-id>" \
    Dataverse__ClientId="<client-id>" \
    Dataverse__ClientSecret="<client-secret>" \
    McpServer__ApiKey="<generate-a-secure-guid-here>"
```

> **Tip:** generate a secure API key with `[System.Guid]::NewGuid()` (PowerShell) or `uuidgen` (bash).

---

## 3 — Build and Deploy

### Option A — Azure CLI deploy (simplest)

```bash
# From the repository root
dotnet publish DataverseDevToolsMcpServer.Azure \
  -c Release \
  -o ./publish

cd publish
zip -r ../deploy.zip .
cd ..

az webapp deploy \
  --name $APP \
  --resource-group $RG \
  --src-path deploy.zip \
  --type zip
```

### Option B — VS Code Azure App Service extension

1. Install the **Azure App Service** extension.
2. Right-click `DataverseDevToolsMcpServer.Azure` → **Deploy to Web App…**
3. Select your subscription and the App Service created above.

### Option C — GitHub Actions (CI/CD)

Add the workflow below at `.github/workflows/azure-deploy.yml`:

```yaml
name: Deploy to Azure App Service

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.x'

      - run: dotnet publish DataverseDevToolsMcpServer.Azure -c Release -o ./publish

      - uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ secrets.AZURE_APP_NAME }}
          publish-profile: ${{ secrets.AZURE_PUBLISH_PROFILE }}
          package: ./publish
```

Store `AZURE_APP_NAME` and `AZURE_PUBLISH_PROFILE` as repository secrets (download the publish profile from the App Service Overview blade).

---

## 4 — Verify the endpoint

```bash
# Health check — should return 200 with MCP server info
curl -i \
  -H "X-Api-Key: <your-api-key>" \
  https://<APP>.azurewebsites.net/mcp
```

Expected response: `200 OK` with the MCP protocol negotiation payload.

---

## 5 — Connect Copilot Studio

1. Open **Copilot Studio** → your Copilot → **Actions** tab.
2. Click **Add an action** → **Model Context Protocol (MCP)**.
3. Fill in the details:

   | Field | Value |
   |-------|-------|
   | Server URL | `https://<APP>.azurewebsites.net/mcp` |
   | Authentication | **API Key** |
   | Header name | `X-Api-Key` |
   | API Key value | The value you set in `McpServer__ApiKey` |

4. Click **Connect** — Copilot Studio will discover all available tools automatically.
5. Enable the tools you want the copilot to use and **Save**.

---

## Architecture summary

```
Copilot Studio
     │  HTTPS POST /mcp
     │  Header: X-Api-Key: <key>
     ▼
Azure App Service (Linux, .NET 8)
DataverseDevToolsMcpServer.Azure
     │  MCP Streamable HTTP transport
     │  ApiKeyMiddleware validates key
     │
     │  ServiceClient (ClientSecret auth)
     ▼
Dataverse / Power Platform environment
```

---

## Troubleshooting

| Symptom | Check |
|---------|-------|
| 401 Unauthorized | Verify `X-Api-Key` header matches `McpServer__ApiKey` App Setting |
| 500 at startup | Check App Service logs — likely a missing Dataverse setting or wrong credentials |
| Dataverse connection fails | Ensure the App Registration has the **Dynamics CRM user_impersonation** API permission and a Dataverse application user is created |
| Tools not discovered | Verify the project reference to `DataverseDevToolsMcpServer` is included in the publish output |

### View App Service logs

```bash
az webapp log tail --name $APP --resource-group $RG
```
