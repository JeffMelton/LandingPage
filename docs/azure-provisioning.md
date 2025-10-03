# Azure Resource Provisioning Guide

This document contains the step-by-step Azure CLI commands to provision all resources for the LandingPage Function App.

## Prerequisites

- Azure CLI installed and authenticated (`az login`)
- Azure subscription with appropriate permissions
- PowerShell with Az module installed (for deployment)

## Variables

Set these variables first:

```bash
RESOURCE_GROUP="rg-landingpage-prod"
LOCATION="southcentralus"
STORAGE_ACCOUNT="stlandingpageprod"  # Must be globally unique, lowercase, no hyphens
FUNCTION_APP="func-landingpage-prod"  # Must be globally unique
APP_INSIGHTS="appi-landingpage-prod"
APP_NAME="LandingPage-Auth"
```

## Step 1: Create Resource Group

```bash
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION
```

## Step 2: Create Storage Account

```bash
az storage account create \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard_LRS \
  --kind StorageV2
```

## Step 3: Create Application Insights

```bash
az monitor app-insights component create \
  --app $APP_INSIGHTS \
  --location $LOCATION \
  --resource-group $RESOURCE_GROUP \
  --application-type web
```

## Step 4: Create Function App

```bash
az functionapp create \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --storage-account $STORAGE_ACCOUNT \
  --consumption-plan-location $LOCATION \
  --runtime dotnet-isolated \
  --runtime-version 9 \
  --functions-version 4 \
  --os-type Linux
```

## Step 5: Update Runtime Configuration

```bash
az functionapp config set \
  --resource-group $RESOURCE_GROUP \
  --name $FUNCTION_APP \
  --linux-fx-version "DOTNET-ISOLATED|9"
```

## Step 6: Configure Application Insights

```bash
# Get Application Insights connection string
APPINSIGHTS_CONNECTION=$(az monitor app-insights component show \
  --app $APP_INSIGHTS \
  --resource-group $RESOURCE_GROUP \
  --query connectionString -o tsv)

# Configure Function App to use Application Insights
az functionapp config appsettings set \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --settings "APPLICATIONINSIGHTS_CONNECTION_STRING=$APPINSIGHTS_CONNECTION"
```

## Step 7: Set Up Entra ID Authentication

### 7.1: Get Function App URL

```bash
FUNCTION_URL=$(az functionapp show \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --query defaultHostName -o tsv)

echo "Function App URL: https://$FUNCTION_URL"
```

### 7.2: Create App Registration

```bash
# Create App Registration with redirect URI
az ad app create \
  --display-name "$APP_NAME" \
  --web-redirect-uris "https://$FUNCTION_URL/.auth/login/aad/callback" \
  --sign-in-audience AzureADMyOrg

# Get the App Client ID (save this)
APP_CLIENT_ID=$(az ad app list --display-name "$APP_NAME" --query "[0].appId" -o tsv)
echo "App Client ID: $APP_CLIENT_ID"

# Get Tenant ID
TENANT_ID=$(az account show --query tenantId -o tsv)
echo "Tenant ID: $TENANT_ID"
```

### 7.3: Configure App Registration Permissions

```bash
# Enable ID token issuance
az ad app update \
  --id $APP_CLIENT_ID \
  --enable-id-token-issuance true

# Add Microsoft Graph User.Read permission
az ad app permission add \
  --id $APP_CLIENT_ID \
  --api 00000003-0000-0000-c000-000000000000 \
  --api-permissions e1fe6dd8-ba31-4d61-89e7-88639da4683d=Scope

# Create service principal
az ad sp create --id $APP_CLIENT_ID

# Grant admin consent
az ad app permission admin-consent --id $APP_CLIENT_ID
```

### 7.4: Upgrade to Auth V2

```bash
az webapp auth config-version upgrade \
  --resource-group $RESOURCE_GROUP \
  --name $FUNCTION_APP
```

### 7.5: Configure Function App Authentication

```bash
# Enable Entra ID authentication
az webapp auth update \
  --resource-group $RESOURCE_GROUP \
  --name $FUNCTION_APP \
  --enabled true \
  --action RedirectToLoginPage \
  --set identityProviders.azureActiveDirectory.enabled=true \
  --set identityProviders.azureActiveDirectory.registration.clientId=$APP_CLIENT_ID \
  --set identityProviders.azureActiveDirectory.registration.openIdIssuer=https://sts.windows.net/$TENANT_ID/
```

### 7.6: Verify Authentication Configuration

```bash
az webapp auth show \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --query "properties.identityProviders.azureActiveDirectory"
```

## Step 8: Deploy Function App

From PowerShell (after running `Connect-AzAccount`):

```powershell
func azure functionapp publish $FUNCTION_APP
```

Or from bash/zsh with Azure CLI authentication:

```bash
func azure functionapp publish $FUNCTION_APP
```

## Step 9: Test

Open in browser:
```
https://<function-app-url>.azurewebsites.net/api/redirect
```

You should be redirected to Microsoft login, then after authentication, redirected to either:
- **xkcd.com** (Tue, Thu, Sat)
- **Random APOD** (Sun, Mon, Wed, Fri)

## Step 10: Configure Custom Domain (Optional)

1. Create CNAME record pointing to your Function App:
   ```
   CNAME: your-subdomain -> <function-app-url>.azurewebsites.net
   ```

2. Add custom domain to Function App:
   ```bash
   az functionapp config hostname add \
     --webapp-name $FUNCTION_APP \
     --resource-group $RESOURCE_GROUP \
     --hostname your-domain.com
   ```

3. Update App Registration redirect URI to include custom domain:
   ```bash
   az ad app update \
     --id $APP_CLIENT_ID \
     --web-redirect-uris \
       "https://$FUNCTION_URL/.auth/login/aad/callback" \
       "https://your-domain.com/.auth/login/aad/callback"
   ```

## Troubleshooting

### Authentication Issues

If getting 401 errors:
1. Verify redirect URI matches Function App URL exactly
2. Check that ID token issuance is enabled
3. Verify API permissions are granted and consented
4. Ensure client ID is configured in auth settings

### View Authentication Configuration

```bash
az webapp auth show \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP
```

### View App Registration Details

```bash
az ad app show --id $APP_CLIENT_ID
```

### Check Function App Logs

```bash
az functionapp log tail \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP
```

Or view in Application Insights via Azure Portal.

## Notes

- The storage account name must be globally unique and contain only lowercase letters and numbers
- The Function App name must also be globally unique
- The Consumption plan keeps costs at ~$0/month for personal use
- Entra ID authentication with `AzureADMyOrg` restricts access to your tenant only
- APOD archive is scraped once on first request and cached in blob storage
