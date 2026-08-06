# Despliegue en Azure Container Apps

## Topología

ScrumBoard usa el entorno compartido de Azure Container Apps `env-mplink` en `westus3` y mantiene recursos de aplicación separados por ambiente:

| GitHub environment | Rama | Resource group | Prefijo Container Apps | Base Neon |
| --- | --- | --- | --- | --- |
| `staging` | `develop` | `scrumboard-staging-rg-south` | `scrumboard-staging` | `scrumboard_staging` |
| `production` | `main` | `scrumboard-prod-rg-south` | `scrumboard-prod` | `scrumboard_production` |

Cada ambiente contiene una API pública, un frontend público y un job manual `<prefijo>-migrations`. Las imágenes se publican en GHCR con el SHA completo del commit como tag de trazabilidad:

```text
ghcr.io/drtx2/scrumboard-api:<sha>
ghcr.io/drtx2/scrumboard-web:<sha>
ghcr.io/drtx2/scrumboard-migrator:<sha>
```

El workflow obtiene el digest producido por cada build y entrega a Azure exclusivamente referencias `ghcr.io/...@sha256:...`; nunca despliega los tags SHA mutables. Los paquetes GHCR permanecen privados y se vinculan al repositorio mediante OCI source labels. Cada GitHub Environment conserva un `GHCR_READ_TOKEN`, que Azure almacena como secret ref de registro. API y web usan revision mode `Single`; la API queda limitada a una réplica porque grupos, presencia y versiones de presencia SignalR viven en memoria del proceso. Dos réplicas producirían vistas parciales aunque compartieran PostgreSQL.

El escalado requiere dos piezas coordinadas: Azure SignalR Service o Redis como backplane para fan-out, y presencia/versionado distribuidos. Para que una notificación sobreviva a una caída posterior al commit se necesita además un outbox transaccional con publicación reintentable; ni el backplane ni la presencia distribuida aportan esa durabilidad por sí solos.

## Ciclo de ramas

1. Una rama `feat/*`, `fix/*` o `chore/*` abre PR hacia `develop`.
2. CI, CodeQL y Dependency Review deben finalizar correctamente antes del merge.
3. El push resultante a `develop` activa `Deploy Azure` y despliega `staging` solo después de un CI correcto para el mismo SHA.
4. La promoción se realiza mediante PR de `develop` hacia `main`; no se reconstruye código distinto para una revisión concreta y todas las imágenes mantienen tags inmutables.
5. El merge a `main` ejecuta nuevamente los gates y prepara `production` mediante su GitHub Environment y sus secretos aislados; el rollout requiere aprobación explícita del propietario.

`workflow_dispatch` permite repetir un despliegue, pero valida que `staging` use `develop` y `production` use `main`, resuelve el HEAD inmutable y exige un workflow `CI` completado correctamente para ese SHA. El Environment de staging también admite `main` como rama controladora porque GitHub ejecuta los jobs `workflow_run` desde la default branch; la validación del workflow sigue exigiendo que el CI origen y el SHA desplegado pertenezcan a `develop`. Los triggers manual y `workflow_run` resuelven el mismo grupo de concurrencia `staging` o `production`, por lo que un ambiente nunca ejecuta dos rollouts simultáneos.

## Orden de despliegue

`.github/workflows/deploy-azure.yml` aplica este orden:

1. Resuelve el SHA que produjo CI.
2. Construye API, frontend y migrador.
3. Bloquea vulnerabilidades HIGH/CRITICAL corregibles con Trivy.
4. Genera SBOM CycloneDX y provenance attestations.
5. Publica las imágenes en GHCR, conserva sus digests como artefactos y despliega por digest.
6. Actualiza la definición del job de migración mediante `deploy/azure/migration-job.bicep`, sin ejecutarlo todavía.
7. Despliega API y web mediante `deploy/azure/apps.bicep` con `MaintenanceMode=true`.
8. Espera que la revisión de API nueva sea ready y la única activa en modo `Single`, y comprueba el 503 de mantenimiento y ambos health checks.
9. Ejecuta y sondea el job hasta `Succeeded`.
10. Despliega exactamente los mismos digests de API y web con `MaintenanceMode=false`; el cambio de variable crea una revisión de API.
11. Espera una única revisión activa y ready por aplicación.
12. Comprueba live, readiness, frontend y login bootstrap.

La API nunca aplica migraciones durante su arranque. Durante mantenimiento, cualquier ruta distinta de `/health/live` y `/health/ready`, incluidos API, hubs y documentación, responde RFC Problem Details en español con HTTP 503, código `maintenance_mode` y `Retry-After: 60`. Si la migración falla o expira, el workflow se detiene y esa revisión de mantenimiento permanece como la única activa; no vuelve a exponer la revisión incompatible anterior. La historia actual termina en `20260806021724_RequireTaskAssigneeAndAddChecks`, que repara responsables nulos/no miembros antes de exigir la FK compuesta; el job actualiza la base en sitio y no recrea bases ni volúmenes.

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
GHCR_USERNAME
```

Secrets requeridos en cada GitHub Environment:

```text
AZURE_CLIENT_ID
AZURE_TENANT_ID
AZURE_SUBSCRIPTION_ID
DATABASE_CONNECTION_STRING
JWT_SIGNING_KEY
PASSWORD_PEPPER
BOOTSTRAP_ADMIN_EMAIL
BOOTSTRAP_ADMIN_PASSWORD
GHCR_READ_TOKEN
```

El repositorio requiere además `GHCR_PUSH_TOKEN`, un PAT de automatización con `write:packages`. Se usa únicamente durante la publicación de imágenes; no se entrega a Azure ni a los contenedores.

`DATABASE_CONNECTION_STRING` debe usar sintaxis Npgsql de pares clave/valor, TLS obligatorio y una base diferente por ambiente. No use directamente una URI PostgreSQL con opciones no soportadas por Npgsql.

## Aprovisionamiento inicial

Con `az` y `gh` autenticados:

```bash
./scripts/provision-azure-oidc.sh
```

El script crea los dos resource groups, reutiliza `env-mplink`, crea o reconcilia la app registration `scrumboard-github-actions`, consulta el prefijo OIDC inmutable del repositorio, registra subjects por GitHub Environment y asigna `Contributor` únicamente en los tres resource groups necesarios. No crea client secrets de Azure ni concede `Owner` o `User Access Administrator`.

Los secretos funcionales se cargan por stdin desde variables del shell:

```bash
export DATABASE_CONNECTION_STRING='...'
export JWT_SIGNING_KEY='...'
export PASSWORD_PEPPER='...'
export BOOTSTRAP_ADMIN_EMAIL='...'
export BOOTSTRAP_ADMIN_PASSWORD='...'
export GHCR_READ_TOKEN='...'
./scripts/configure-github-secrets.sh staging
```

Repita con valores independientes para `production`. `GHCR_READ_TOKEN` debe ser un PAT reemplazable con permiso mínimo `read:packages`; `GHCR_PUSH_TOKEN` se configura una sola vez como repository secret y debe rotarse independientemente.

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
- El rollout de mantenimiento precede al job de migración y el rollout funcional solo ocurre tras `Succeeded`.
- Las revisiones se validan por nombre y como única revisión activa, no mediante una respuesta de una revisión anterior.
- Branch protection exige PR y checks antes de integrar en `develop` o `main`.
- Production queda adicionalmente restringido por el GitHub Environment `production`.
