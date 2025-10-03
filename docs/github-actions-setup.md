# GitHub Actions CI/CD Setup for Azure Functions

This guide provides complete, step-by-step instructions for setting up continuous deployment from GitHub to Azure Functions using OpenID Connect (OIDC) federated credentials. This approach is more secure than using publish profiles or service principal secrets.

## Prerequisites

- Azure CLI authenticated (`az login`)
- Existing Azure Function App (see `azure-provisioning.md`)
- GitHub repository at `JeffMelton/LandingPage`
- Appropriate Azure permissions (Contributor or Owner on subscription/resource group)

## Overview

The setup creates a **user-assigned managed identity** that GitHub Actions will use to authenticate to Azure. The identity uses **federated credentials** that trust GitHub's OIDC provider, allowing GitHub Actions to get Azure tokens without storing any secrets.

## Step 1: Set Variables

Set these based on your existing deployment:

```bash
RESOURCE_GROUP="rg-landingpage-prod"
LOCATION="southcentralus"
FUNCTION_APP="func-landingpage-prod"
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
MANAGED_IDENTITY_NAME="id-landingpage-github"
GITHUB_ORG="JeffMelton"
GITHUB_REPO="LandingPage"
```

Verify your subscription:
```bash
echo "Subscription ID: $SUBSCRIPTION_ID"
az account show --query "{Name:name, ID:id, TenantID:tenantId}" -o table
```

## Step 2: Create User-Assigned Managed Identity

Create the managed identity that GitHub Actions will use:

```bash
az identity create \
  --name $MANAGED_IDENTITY_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION
```

Capture the identity details (you'll need these):

```bash
# Get Client ID (Application ID)
IDENTITY_CLIENT_ID=$(az identity show \
  --name $MANAGED_IDENTITY_NAME \
  --resource-group $RESOURCE_GROUP \
  --query clientId -o tsv)

# Get Principal ID (Object ID)
IDENTITY_PRINCIPAL_ID=$(az identity show \
  --name $MANAGED_IDENTITY_NAME \
  --resource-group $RESOURCE_GROUP \
  --query principalId -o tsv)

# Get Resource ID
IDENTITY_RESOURCE_ID=$(az identity show \
  --name $MANAGED_IDENTITY_NAME \
  --resource-group $RESOURCE_GROUP \
  --query id -o tsv)

# Get Tenant ID
TENANT_ID=$(az account show --query tenantId -o tsv)
```

Display the values (save these for GitHub secrets):

```bash
echo "=== Identity Information ==="
echo "Client ID: $IDENTITY_CLIENT_ID"
echo "Principal ID: $IDENTITY_PRINCIPAL_ID"
echo "Resource ID: $IDENTITY_RESOURCE_ID"
echo "Tenant ID: $TENANT_ID"
echo "Subscription ID: $SUBSCRIPTION_ID"
```

## Step 3: Configure Federated Credentials

Federated credentials tell Azure to trust tokens from GitHub Actions running in your repository.

### 3.1: Create Credential for Main Branch

This allows deployments from the `main` branch:

```bash
az identity federated-credential create \
  --name "github-main-branch" \
  --identity-name $MANAGED_IDENTITY_NAME \
  --resource-group $RESOURCE_GROUP \
  --issuer "https://token.actions.githubusercontent.com" \
  --subject "repo:${GITHUB_ORG}/${GITHUB_REPO}:ref:refs/heads/main" \
  --audiences "api://AzureADTokenExchange"
```

### 3.2: Create Credential for Pull Requests (Optional)

If you want to test deployments from PRs:

```bash
az identity federated-credential create \
  --name "github-pull-requests" \
  --identity-name $MANAGED_IDENTITY_NAME \
  --resource-group $RESOURCE_GROUP \
  --issuer "https://token.actions.githubusercontent.com" \
  --subject "repo:${GITHUB_ORG}/${GITHUB_REPO}:pull_request" \
  --audiences "api://AzureADTokenExchange"
```

### 3.3: Verify Federated Credentials

```bash
az identity federated-credential list \
  --identity-name $MANAGED_IDENTITY_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "[].{Name:name, Subject:subject}" -o table
```

## Step 4: Assign RBAC Permissions

The managed identity needs permissions to deploy to the Function App.

### 4.1: Get Function App Resource ID

```bash
FUNCTION_APP_RESOURCE_ID=$(az functionapp show \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --query id -o tsv)

echo "Function App Resource ID: $FUNCTION_APP_RESOURCE_ID"
```

### 4.2: Assign Website Contributor Role

This role allows deployment without granting excessive permissions:

```bash
az role assignment create \
  --assignee $IDENTITY_PRINCIPAL_ID \
  --role "Website Contributor" \
  --scope $FUNCTION_APP_RESOURCE_ID
```

### 4.3: Verify Role Assignment

```bash
az role assignment list \
  --assignee $IDENTITY_PRINCIPAL_ID \
  --scope $FUNCTION_APP_RESOURCE_ID \
  --query "[].{Role:roleDefinitionName, Scope:scope}" -o table
```

**Expected output:**
```
Role                  Scope
--------------------  -------------------------------------------------------
Website Contributor   /subscriptions/.../resourceGroups/.../providers/...
```

## Step 5: Create GitHub Actions Workflow

Create the workflow file in your repository:

```bash
mkdir -p .github/workflows
```

Create `.github/workflows/deploy.yml`:

```yaml
name: Deploy to Azure Functions

on:
  push:
    branches:
      - main
  workflow_dispatch:

permissions:
  id-token: write
  contents: read

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest

    steps:
    - name: Checkout code
      uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '9.0.x'

    - name: Restore dependencies
      run: dotnet restore

    - name: Build project
      run: dotnet build --configuration Release --no-restore

    - name: Publish project
      run: dotnet publish --configuration Release --no-build --output ./publish

    - name: Azure Login (OIDC)
      uses: azure/login@v2
      with:
        client-id: ${{ secrets.AZURE_CLIENT_ID }}
        tenant-id: ${{ secrets.AZURE_TENANT_ID }}
        subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

    - name: Deploy to Azure Functions
      uses: Azure/functions-action@v1
      with:
        app-name: ${{ vars.AZURE_FUNCTION_APP_NAME }}
        package: ./publish
```

## Step 6: Configure GitHub Repository

### 6.1: Add GitHub Secrets

Navigate to your GitHub repository:
```
https://github.com/JeffMelton/LandingPage/settings/secrets/actions
```

Click **"New repository secret"** and add these three secrets:

| Secret Name | Value |
|-------------|-------|
| `AZURE_CLIENT_ID` | `$IDENTITY_CLIENT_ID` from Step 2 |
| `AZURE_TENANT_ID` | `$TENANT_ID` from Step 2 |
| `AZURE_SUBSCRIPTION_ID` | `$SUBSCRIPTION_ID` from Step 2 |

### 6.2: Add GitHub Variables

Navigate to:
```
https://github.com/JeffMelton/LandingPage/settings/variables/actions
```

Click **"New repository variable"** and add:

| Variable Name | Value |
|---------------|-------|
| `AZURE_FUNCTION_APP_NAME` | `func-landingpage-prod` |

### 6.3: Alternative - Get Values from Azure

If you need to retrieve the values again:

```bash
# Get all values at once
echo "=== Copy these to GitHub Secrets ==="
echo "AZURE_CLIENT_ID: $(az identity show --name $MANAGED_IDENTITY_NAME --resource-group $RESOURCE_GROUP --query clientId -o tsv)"
echo "AZURE_TENANT_ID: $(az account show --query tenantId -o tsv)"
echo "AZURE_SUBSCRIPTION_ID: $(az account show --query id -o tsv)"
echo ""
echo "=== Copy this to GitHub Variables ==="
echo "AZURE_FUNCTION_APP_NAME: $FUNCTION_APP"
```

## Step 7: Test the Deployment

### 7.1: Commit and Push Workflow

```bash
git add .github/workflows/deploy.yml
git commit -m "Add GitHub Actions deployment workflow"
git push origin main
```

### 7.2: Monitor Workflow Execution

Navigate to:
```
https://github.com/JeffMelton/LandingPage/actions
```

You should see the workflow running. Click on it to view logs.

### 7.3: Verify Deployment

After successful deployment:

```bash
# Check Function App status
az functionapp show \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --query "{Name:name, State:state, URL:defaultHostName}" -o table
```

Test the endpoint:
```bash
FUNCTION_URL=$(az functionapp show \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --query defaultHostName -o tsv)

curl -I "https://${FUNCTION_URL}/api/redirect"
```

## Troubleshooting

### Error: "AADSTS70021: No matching federated identity record found"

**Cause:** The federated credential subject doesn't match the GitHub context.

**Solution:** Verify the subject pattern:

```bash
# List existing credentials
az identity federated-credential list \
  --identity-name $MANAGED_IDENTITY_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "[].{Name:name, Subject:subject}" -o table

# Expected subject for main branch:
# repo:JeffMelton/LandingPage:ref:refs/heads/main
```

If incorrect, delete and recreate:
```bash
az identity federated-credential delete \
  --name "github-main-branch" \
  --identity-name $MANAGED_IDENTITY_NAME \
  --resource-group $RESOURCE_GROUP

# Then recreate with correct subject (see Step 3.1)
```

### Error: "AuthorizationFailed" during deployment

**Cause:** Managed identity lacks sufficient permissions.

**Solution:** Verify and re-assign role:

```bash
# Check current assignments
az role assignment list \
  --assignee $IDENTITY_PRINCIPAL_ID \
  --all \
  --query "[].{Role:roleDefinitionName, Scope:scope}" -o table

# If missing, reassign (see Step 4.2)
```

### Workflow doesn't trigger

**Cause:** Workflow file not in correct location or syntax error.

**Solution:**
1. Verify file is at `.github/workflows/deploy.yml`
2. Validate YAML syntax: https://www.yamllint.com/
3. Check GitHub Actions is enabled in repository settings

### Deployment succeeds but function doesn't work

**Cause:** Authentication configuration may have been overwritten.

**Solution:** Verify auth is still enabled:

```bash
az webapp auth show \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --query "properties.identityProviders.azureActiveDirectory.enabled"
```

If disabled, reconfigure (see `azure-provisioning.md`, Step 7).

## Advanced: Environment-Based Deployment

To support multiple environments (dev/staging/prod), create separate:

1. Managed identities per environment
2. Function Apps per environment
3. Federated credentials for different branches
4. GitHub environments with protection rules

Example for dev environment:

```bash
# Create dev identity
az identity create \
  --name "id-landingpage-github-dev" \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION

# Create federated credential for dev branch
az identity federated-credential create \
  --name "github-dev-branch" \
  --identity-name "id-landingpage-github-dev" \
  --resource-group $RESOURCE_GROUP \
  --issuer "https://token.actions.githubusercontent.com" \
  --subject "repo:${GITHUB_ORG}/${GITHUB_REPO}:ref:refs/heads/dev" \
  --audiences "api://AzureADTokenExchange"

# Assign permissions to dev Function App
# (create dev Function App first)
```

## Security Best Practices

1. **Least Privilege:** Use "Website Contributor" role, not "Contributor" or "Owner"
2. **No Secrets:** OIDC federated credentials avoid storing secrets in GitHub
3. **Branch Protection:** Enable branch protection rules on `main`
4. **Environment Protection:** Use GitHub environments with approval workflows for production
5. **Audit:** Regularly review managed identity permissions:
   ```bash
   az role assignment list \
     --assignee $IDENTITY_PRINCIPAL_ID \
     --all -o table
   ```

## Cleanup (if needed)

To remove the GitHub Actions setup:

```bash
# Delete role assignment
az role assignment delete \
  --assignee $IDENTITY_PRINCIPAL_ID \
  --scope $FUNCTION_APP_RESOURCE_ID

# Delete managed identity (also deletes federated credentials)
az identity delete \
  --name $MANAGED_IDENTITY_NAME \
  --resource-group $RESOURCE_GROUP
```

## References

- [Azure Functions GitHub Actions Documentation](https://learn.microsoft.com/en-us/azure/azure-functions/functions-how-to-github-actions?tabs=linux%2Cdotnet&pivots=method-cli)
- [Configuring OpenID Connect in Azure](https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/configuring-openid-connect-in-azure)
- [Azure RBAC Built-in Roles](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles)

## Notes

- Federated credentials can take 1-2 minutes to propagate after creation
- The `workflow_dispatch` trigger allows manual deployment via GitHub UI
- The workflow builds on Ubuntu for consistency with Azure Functions Linux runtime
- Authentication settings (Entra ID) should persist across deployments
