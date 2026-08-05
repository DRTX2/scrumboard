#!/usr/bin/env bash
set -euo pipefail

repository="${GITHUB_REPOSITORY:-DRTX2/scrumboard}"
application_name="${AZURE_APPLICATION_NAME:-scrumboard-github-actions}"
infrastructure_location="${AZURE_INFRA_LOCATION:-southcentralus}"
app_location="${AZURE_APP_LOCATION:-westus3}"
container_environment_name="${AZURE_CONTAINER_ENV_NAME:-env-mplink}"
container_environment_resource_group="${AZURE_CONTAINER_ENV_RESOURCE_GROUP:-rg-app-container}"

subscription_id=$(az account show --query id --output tsv)
tenant_id=$(az account show --query tenantId --output tsv)

az containerapp env show \
  --name "$container_environment_name" \
  --resource-group "$container_environment_resource_group" \
  --output none

for deployment_environment in staging production; do
  resource_suffix="$deployment_environment"
  if [ "$deployment_environment" = "production" ]; then resource_suffix="prod"; fi
  resource_group="scrumboard-${resource_suffix}-rg-south"
  az group create --name "$resource_group" --location "$infrastructure_location" --output none
done

application_id=$(az ad app list --display-name "$application_name" --query '[0].appId' --output tsv)
if [ -z "$application_id" ]; then
  application_id=$(az ad app create --display-name "$application_name" --query appId --output tsv)
fi
application_object_id=$(az ad app show --id "$application_id" --query id --output tsv)

service_principal_object_id=$(az ad sp list --filter "appId eq '$application_id'" --query '[0].id' --output tsv)
if [ -z "$service_principal_object_id" ]; then
  service_principal_object_id=$(az ad sp create --id "$application_id" --query id --output tsv)
fi

for deployment_environment in staging production; do
  credential_name="scrumboard-${deployment_environment}"
  subject="repo:${repository}:environment:${deployment_environment}"
  existing=$(az ad app federated-credential list \
    --id "$application_object_id" \
    --query "[?name=='$credential_name'].name | [0]" \
    --output tsv)
  if [ -z "$existing" ]; then
    credential=$(jq -n \
      --arg name "$credential_name" \
      --arg subject "$subject" \
      '{name:$name,issuer:"https://token.actions.githubusercontent.com",subject:$subject,audiences:["api://AzureADTokenExchange"]}')
    az ad app federated-credential create \
      --id "$application_object_id" \
      --parameters "$credential" \
      --output none
  fi
done

for resource_group in \
  scrumboard-staging-rg-south \
  scrumboard-prod-rg-south \
  "$container_environment_resource_group"; do
  scope="/subscriptions/$subscription_id/resourceGroups/$resource_group"
  assignment=$(az role assignment list \
    --assignee "$service_principal_object_id" \
    --scope "$scope" \
    --role Contributor \
    --query '[0].id' \
    --output tsv)
  if [ -z "$assignment" ]; then
    az role assignment create \
      --assignee-object-id "$service_principal_object_id" \
      --assignee-principal-type ServicePrincipal \
      --role Contributor \
      --scope "$scope" \
      --output none
  fi
done

for deployment_environment in staging production; do
  resource_suffix="$deployment_environment"
  if [ "$deployment_environment" = "production" ]; then resource_suffix="prod"; fi
  resource_group="scrumboard-${resource_suffix}-rg-south"
  environment_prefix="scrumboard-${resource_suffix}"
  printf '%s' "$application_id" | gh secret set AZURE_CLIENT_ID --repo "$repository" --env "$deployment_environment"
  printf '%s' "$tenant_id" | gh secret set AZURE_TENANT_ID --repo "$repository" --env "$deployment_environment"
  printf '%s' "$subscription_id" | gh secret set AZURE_SUBSCRIPTION_ID --repo "$repository" --env "$deployment_environment"
  gh variable set AZURE_RESOURCE_GROUP --repo "$repository" --env "$deployment_environment" --body "$resource_group"
  gh variable set AZURE_APP_LOCATION --repo "$repository" --env "$deployment_environment" --body "$app_location"
  gh variable set AZURE_ENVIRONMENT_NAME --repo "$repository" --env "$deployment_environment" --body "$environment_prefix"
  gh variable set AZURE_CONTAINER_ENV_NAME --repo "$repository" --env "$deployment_environment" --body "$container_environment_name"
  gh variable set AZURE_CONTAINER_ENV_RESOURCE_GROUP --repo "$repository" --env "$deployment_environment" --body "$container_environment_resource_group"
  gh variable set BOOTSTRAP_ADMIN_NAME --repo "$repository" --env "$deployment_environment" --body "ScrumBoard Administrator"
  gh variable set MIN_REPLICAS --repo "$repository" --env "$deployment_environment" --body "0"
  remove_demo_workspace="false"
  if [ "$deployment_environment" = "production" ]; then remove_demo_workspace="true"; fi
  gh variable set REMOVE_DEMO_WORKSPACE --repo "$repository" --env "$deployment_environment" --body "$remove_demo_workspace"
done

echo "Azure OIDC and non-secret GitHub environment configuration completed."
echo "Application client id: $application_id"
