# Despliegue en Azure Container Apps

## Topología

ScrumBoard usa el entorno compartido de Azure Container Apps `env-mplink` en `westus3` y mantiene recursos de aplicación separados por ambiente:

| GitHub environment | Rama | Resource group | Prefijo Container Apps | Base Neon |
| --- | --- | --- | --- | --- |
| `staging` | `develop` | `scrumboard-staging-rg-south` | `scrumboard-staging` | `scrumboard_staging` |
| `production` | `main` | `scrumboard-prod-rg-south` | `scrumboard-prod` | `scrumboard_production` |

Cada ambiente contiene una API pública, un frontend público y un job manual `<prefijo>-migrations`. Las imágenes se publican en GHCR con el SHA completo del commit:

```text
ghcr.io/drtx2/scrumboard-api:<sha>
ghcr.io/drtx2/scrumboard-web:<sha>
ghcr.io/drtx2/scrumboard-migrator:<sha>
```

Los paquetes GHCR son públicos para permitir pulls anónimos desde Container Apps. No se almacenan tokens de registro en Azure. API y web usan revision mode `Single`; la API queda limitada a una réplica hasta incorporar un backplane distribuido para SignalR.

## Ciclo de ramas

1. Una rama `feat/*`, `fix/*` o `chore/*` abre PR hacia `develop`.
2. CI, CodeQL y Dependency Review deben finalizar correctamente antes del merge.
3. El push resultante a `develop` activa `Deploy Azure` y despliega `staging` solo después de un CI correcto para el mismo SHA.
4. La promoción se realiza mediante PR de `develop` hacia `main`; no se reconstruye código distinto para una revisión concreta y todas las imágenes mantienen tags inmutables.
5. El merge a `main` ejecuta nuevamente los gates y despliega `production` mediante su GitHub Environment y sus secretos aislados.

`workflow_dispatch` permite repetir un despliegue, pero valida que `staging` use `develop` y `production` use `main`. Los grupos de concurrencia evitan dos rollouts simultáneos del mismo ambiente.

## Orden de despliegue

`.github/workflows/deploy-azure.yml` aplica este orden:

1. Resuelve el SHA que produjo CI.
2. Construye API, frontend y migrador.
3. Bloquea vulnerabilidades HIGH/CRITICAL corregibles con Trivy.
4. Genera SBOM CycloneDX y provenance attestations.
5. Publica imágenes SHA en GHCR.
6. Actualiza exclusivamente el job de migración mediante `deploy/azure/migration-job.bicep`.
7. Ejecuta y sondea el job hasta `Succeeded`.
8. Despliega API y web mediante `deploy/azure/apps.bicep`.
9. Espera que `latestRevisionName` sea igual a `latestReadyRevisionName`.
10. Comprueba live, readiness, frontend y login bootstrap.

La API nunca aplica migraciones durante su arranque. Un fallo de migración impide que se despliegue la nueva revisión de aplicación.

## Migraciones y bootstrap

El job usa la misma imagen `ScrumBoard.Migrator` que se valida localmente. Tras aplicar las migraciones EF Core, reconcilia la cuenta propietaria determinista con credenciales suministradas por secretos:

- genera una nueva sal PBKDF2 en cada reconciliación;
- usa el pepper específico del ambiente;
- normaliza el correo;
- desactiva la cuenta demo secundaria;
- conserva el workspace de ejemplo en `staging`;
- elimina el workspace de ejemplo en `production`.

Esto permite rotar contraseña o pepper actualizando GitHub Secrets y relanzando el workflow, sin incluir hashes o credenciales cloud en migraciones ni en el repositorio.

## Configuración GitHub

Variables requeridas en cada GitHub Environment:

```text
AZURE_RESOURCE_GROUP
AZURE_APP_LOCATION
AZURE_ENVIRONMENT_NAME
AZURE_CONTAINER_ENV_NAME
AZURE_CONTAINER_ENV_RESOURCE_GROUP
BOOTSTRAP_ADMIN_NAME
MIN_REPLICAS
REMOVE_DEMO_WORKSPACE
```

Secrets requeridos:

```text
AZURE_CLIENT_ID
AZURE_TENANT_ID
AZURE_SUBSCRIPTION_ID
DATABASE_CONNECTION_STRING
JWT_SIGNING_KEY
PASSWORD_PEPPER
BOOTSTRAP_ADMIN_EMAIL
BOOTSTRAP_ADMIN_PASSWORD
```

`DATABASE_CONNECTION_STRING` debe usar sintaxis Npgsql de pares clave/valor, TLS obligatorio y una base diferente por ambiente. No use directamente una URI PostgreSQL con opciones no soportadas por Npgsql.

## Aprovisionamiento inicial

Con `az` y `gh` autenticados:

```bash
./scripts/provision-azure-oidc.sh
```

El script crea los dos resource groups, reutiliza `env-mplink`, crea o reconcilia la app registration `scrumboard-github-actions`, registra subjects OIDC por GitHub Environment y asigna `Contributor` únicamente en los tres resource groups necesarios. No crea client secrets de Azure ni concede `Owner` o `User Access Administrator`.

Los secretos funcionales se cargan por stdin desde variables del shell:

```bash
export DATABASE_CONNECTION_STRING='...'
export JWT_SIGNING_KEY='...'
export PASSWORD_PEPPER='...'
export BOOTSTRAP_ADMIN_EMAIL='...'
export BOOTSTRAP_ADMIN_PASSWORD='...'
./scripts/configure-github-secrets.sh staging
```

Repita con valores independientes para `production`. Después del primer push de imágenes, marque `scrumboard-api`, `scrumboard-web` y `scrumboard-migrator` como paquetes públicos en GHCR.

## Operación

Estado de migraciones:

```bash
az containerapp job execution list \
  --resource-group scrumboard-staging-rg-south \
  --name scrumboard-staging-migrations \
  --output table
```

Revisión activa:

```bash
az containerapp show \
  --resource-group scrumboard-prod-rg-south \
  --name scrumboard-prod-api \
  --query '{latest:properties.latestRevisionName,ready:properties.latestReadyRevisionName}'
```

Para una rotación, actualice el GitHub Environment secret y relance `Deploy Azure`; Container Apps recibe el secreto mediante una nueva revisión. Para rollback de aplicación, revierta el commit mediante PR y deje que el pipeline despliegue un nuevo SHA. No revierta migraciones destructivamente: prepare una migración forward-fix compatible antes de promoverla.

## Controles

- OIDC sustituye credenciales Azure persistentes.
- Los secretos Bicep son `@secure` y se convierten en secret refs de Container Apps.
- Staging y production no comparten base, JWT, pepper ni cuenta bootstrap.
- El job de migración precede al rollout.
- Las revisiones se validan por nombre, no mediante una respuesta de una revisión anterior.
- Branch protection exige PR y checks antes de integrar en `develop` o `main`.
- Production queda adicionalmente restringido por el GitHub Environment `production`.
