# ScrumBoard

ScrumBoard es una aplicación colaborativa para administrar proyectos y tableros Scrum. Combina una API ASP.NET Core 8, una SPA Angular 17, PostgreSQL y actualizaciones en tiempo real con SignalR. Incluye control de concurrencia optimista, operaciones POST idempotentes, exportación de reportes y una topología reproducible con Docker Compose.

## Requisitos

Para la ejecución recomendada:

- Docker Engine 24 o posterior con Docker Compose v2.
- Al menos 2 GB de memoria disponibles para los contenedores.
- Puertos `8080` (web) y `5000` (API) libres, o valores alternativos en `.env`.

Para desarrollo sin contenedores:

- .NET SDK indicado en `global.json` (canal .NET 8).
- Node.js 20 y npm.
- PostgreSQL 16.
- Google Chrome o Chromium para las pruebas Angular.

## Inicio rápido

1. Cree la configuración local a partir del ejemplo:

   ```bash
   cp .env.example .env
   ```

2. Cambie al menos `POSTGRES_PASSWORD` y `JWT_SIGNING_KEY`. Para conservar el acceso de demostración, no cambie `PASSWORD_PEPPER`: los hashes sembrados se generaron con ese valor de desarrollo.

3. Construya e inicie el entorno:

   ```bash
   docker compose up --build
   ```

4. Abra `http://localhost:8080`. La API queda en `http://localhost:5000`, Scalar en `http://localhost:5000/scalar/v1` y OpenAPI en `http://localhost:5000/swagger/v1/swagger.json`.

El servicio `migrator` es intencionalmente de una sola ejecución: espera a PostgreSQL, aplica migraciones y termina con código `0`. La API solo inicia si esa ejecución finaliza correctamente. Compruebe el estado con:

```bash
docker compose ps
docker compose logs migrator
curl --fail http://localhost:5000/health/ready
```

Para detener el entorno use `docker compose down`. Para eliminar también los datos locales use `docker compose down --volumes`; esta última operación es destructiva.

Si cambia `API_PORT` o `FRONTEND_PORT`, ajuste también `API_PUBLIC_URL`, `HUB_PUBLIC_URL` y `FRONTEND_ORIGIN`, porque las URL del frontend son consumidas por el navegador y no por la red interna de Docker.

## Credenciales de demostración

| Rol | Correo | Contraseña |
| --- | --- | --- |
| Propietario | `owner@scrumboard.local` | `ScrumBoard123!` |
| Miembro | `member@scrumboard.local` | `ScrumBoard123!` |

Estas cuentas y la contraseña son exclusivamente para desarrollo. El propietario puede administrar columnas; ambos miembros pueden trabajar con tareas. El job cloud reemplaza las credenciales del propietario con secretos del ambiente, desactiva el miembro demo y elimina el workspace de ejemplo en production. Nunca use los secretos de `.env.example` fuera del entorno local.

## Arquitectura

La solución sigue una separación por capas:

- `ScrumBoard.Domain`: entidades, invariantes y ordenamiento.
- `ScrumBoard.Application`: modelos neutrales, puertos `Inbound`/`Outbound` y casos de uso, sin dependencias de frameworks o DI.
- `ScrumBoard.Infrastructure`: adaptadores de salida y configuración de EF Core/Npgsql, JWT, PBKDF2, tiempo y reportes PDF/XLSX.
- `ScrumBoard.Api`: adaptador de entrada HTTP, adaptador bidireccional SignalR, idempotencia técnica, composition root, Problem Details, autenticación, rate limiting y salud.
- `ScrumBoard.Migrator`: ejecutable independiente que aplica migraciones antes del arranque.
- `frontend`: SPA Angular servida por nginx no privilegiado, con configuración de endpoints en tiempo de ejecución.

La API y el migrador se publican en imágenes multi-stage y se ejecutan como el usuario no privilegiado de .NET. El frontend usa `nginx-unprivileged` como UID 101. La base de datos es el único componente con volumen persistente.

Consulte [el diagrama de arquitectura](docs/architecture.md) y [el modelo de datos](docs/data-model.md).

## Azure Container Apps

El ciclo cloud usa `develop` para staging y `main` para production. GitHub Actions publica imágenes GHCR inmutables, ejecuta el job de migración antes del rollout y valida la revisión activa mediante healthchecks y login. Consulte [la guía completa de Azure y DevOps](docs/azure-deployment.md).

## API y endpoints

Todas las rutas funcionales usan el prefijo `/api/v1`. Salvo la creación de sesión y los checks de salud, requieren `Authorization: Bearer <token>`.

| Método | Ruta | Propósito |
| --- | --- | --- |
| `POST` | `/api/v1/sessions` | Autenticar y obtener un JWT. |
| `GET`, `POST` | `/api/v1/projects` | Listar con paginación/filtros o crear proyectos. |
| `GET`, `PUT`, `DELETE` | `/api/v1/projects/{projectId}` | Consultar, actualizar o eliminar un proyecto. |
| `GET` | `/api/v1/projects/{projectId}/board` | Obtener el tablero; acepta `assigneeId`, `priority` y `search`. |
| `GET` | `/api/v1/projects/{projectId}/members` | Listar miembros y roles. |
| `POST` | `/api/v1/projects/{projectId}/columns` | Crear una columna. |
| `PUT`, `PATCH`, `DELETE` | `/api/v1/projects/{projectId}/columns/{columnId}` | Editar, mover o eliminar una columna. |
| `POST` | `/api/v1/projects/{projectId}/tasks` | Crear una tarea. |
| `PUT`, `PATCH`, `DELETE` | `/api/v1/projects/{projectId}/tasks/{taskId}` | Editar, mover o eliminar una tarea. |
| `GET` | `/api/v1/projects/{projectId}/reports?format=pdf` | Descargar un reporte `pdf` o `xlsx`, con los filtros del tablero. |
| `GET` | `/health/live`, `/health/ready` | Comprobar proceso y disponibilidad de PostgreSQL, respectivamente. |
| WebSocket/HTTP | `/hubs/boards` | Hub autenticado de SignalR. |

La lista de proyectos acepta `page`, `pageSize`, `search`, `sort` y `direction`; también devuelve `X-Total-Count`. Los errores se presentan como RFC 9457 Problem Details e incluyen código de dominio y `traceId` cuando corresponde. La especificación OpenAPI es la fuente exacta para cuerpos y respuestas.

## Concurrencia

Los proyectos, columnas y tareas tienen una versión persistida. Las lecturas y mutaciones devuelven un `ETag`; las mutaciones `PUT`, `PATCH` y `DELETE` exigen enviarlo en `If-Match`.

```http
If-Match: "3"
```

- Sin `If-Match`, la API responde `428 Precondition Required`.
- Con una versión obsoleta, responde `412 Precondition Failed`.
- Los movimientos usan el ETag global del tablero y las demás mutaciones usan el ETag de la entidad; las respuestas del tablero también devuelven `X-Board-ETag`.
- El orden de columnas y tareas usa posiciones espaciadas y rebalanceo cuando ya no existe espacio entre vecinos.

El cliente debe volver a leer el recurso después de un `412`, reconciliar los cambios y reintentar de forma explícita; no debe sobrescribir silenciosamente el trabajo de otra persona.

## Idempotencia

Los `POST` autenticados marcados como idempotentes aceptan `Idempotency-Key` de 1 a 100 caracteres. La clave es única por usuario durante su vigencia. El fingerprint SHA-256 incorpora método, plantilla y valores de ruta, query ordenada, tipo de contenido y bytes del cuerpo.

- Repetir la misma clave y el mismo cuerpo reproduce status, cuerpo, tipo de contenido, `Location`, `ETag` y `X-Board-ETag`, y añade `Idempotency-Replayed: true`.
- Reutilizar la clave para cualquier solicitud distinta, incluso en otra ruta, responde `409 Conflict`.
- Una solicitud concurrente aún en proceso también responde `409 Conflict`.
- Las respuestas no exitosas no se conservan.
- Una reserva en proceso tiene un lease de cinco minutos; al completarse, el replay permanece vigente durante 24 horas. Los registros vencidos dejan de bloquear la clave; una limpieza periódica en lote figura en el roadmap.
- La mutación y la respuesta replay se confirman en una transacción; SignalR se publica después del commit y sus fallos no revierten una operación durable.

En integraciones reales, genere una clave aleatoria por intención de negocio y conserve la misma clave únicamente durante reintentos de esa intención.
La SPA sigue ese ciclo en los diálogos de creación: genera la clave al iniciar una intención y la conserva mientras el usuario reintenta esa misma operación.

## Tiempo real

El frontend se conecta a `/hubs/boards` con el JWT y llama `SubscribeToBoard(projectId)`. El servidor valida la membresía antes de agregar la conexión al grupo. Application produce notificaciones tipadas y el adaptador SignalR las traduce a `ColumnChanged`, `TaskCreated`, `TaskUpdated`, `TaskMoved`, `TaskDeleted` y `PresenceChanged`.

La presencia se mantiene en memoria y por conexión; al desconectarse se limpian todas sus suscripciones. Cada snapshot lleva una versión creciente y el cliente ignora actualizaciones antiguas. La topología actual admite una sola réplica de API. Para escalado horizontal se necesita un backplane de SignalR y presencia distribuida, tal como se indica en el roadmap.

## Reportes

`GET /api/v1/projects/{projectId}/reports` genera descargas PDF o XLSX. Admite `assigneeId`, `priority` y `search`; el caso de uso aplica la membresía antes de consultar datos y un filtro sin coincidencias genera un reporte vacío válido. Los exportadores viven detrás de puertos de aplicación para mantener la lógica independiente de las bibliotecas de formato.

## Seguridad y secretos

- JWT firmado simétricamente, con issuer/audience y expiración de 30 minutos por defecto.
- Contraseñas derivadas con PBKDF2, 210 000 iteraciones, sal individual y pepper externo.
- CORS restringido al origen configurado y autorización por membresía/rol.
- Rate limit global de 120 solicitudes por minuto por usuario o IP.
- IDs de correlación, Problem Details y trazabilidad OpenTelemetry instrumentada.
- Imágenes de aplicación no privilegiadas, filesystem de API/migrador de solo lectura y `no-new-privileges` en Compose.
- PostgreSQL no publica su puerto al host en la topología predeterminada.

En producción, inyecte secretos desde el gestor de secretos de la plataforma, use claves aleatorias largas, TLS extremo a extremo, rotación de credenciales y un usuario PostgreSQL con privilegios separados para migración y ejecución. El Compose local sirve HTTP; no representa por sí solo un despliegue público endurecido.

[//]: # (pending)
### Riesgo conocido de Angular 17

`npm audit --omit=dev` reporta 10 vulnerabilidades altas en la línea Angular/PrimeNG exigida por el reto. La corrección automática disponible migra a Angular 22 y rompe la restricción de Angular 17, por lo que no se aplicó de forma encubierta. Esta SPA no usa SSR, `HttpTransferCache`, plantillas dinámicas ni HTML proporcionado por usuarios, lo que reduce la exposición de varios avisos, pero no elimina la deuda. CI bloquea nuevas vulnerabilidades críticas y el salto de versión está registrado en el roadmap.

## Desarrollo y pruebas

Backend:

```bash
dotnet restore ScrumBoard.sln
dotnet build ScrumBoard.sln --configuration Release --no-restore
dotnet test ScrumBoard.sln --configuration Release --no-build --collect:"XPlat Code Coverage"
```

Las pruebas de integración usan Testcontainers, por lo que Docker debe estar disponible.

Las migraciones viven con el adaptador PostgreSQL en `src/ScrumBoard.Infrastructure/Adapters/Outbound/Persistence/Migrations` y se aplican mediante `ScrumBoard.Migrator`. La historia incremental actual reemplaza migraciones tempranas del entorno de desarrollo; si conserva un volumen creado por una versión anterior, recréelo antes de iniciar el stack:

```bash
docker compose down --volumes
docker compose up --build
```

La estrategia y el orden de migraciones están documentados en [arquitectura](docs/architecture.md#historia-de-migraciones).

Frontend:

```bash
cd frontend
npm ci
npm test -- --watch=false --browsers=ChromeHeadlessNoSandbox
npm run build -- --configuration production
```

End-to-end, con la API disponible y las variables adecuadas:

```bash
cd frontend
npx playwright install chromium
npm run e2e
```

La automatización en `.github/workflows/ci.yml` restaura, compila y prueba .NET; audita dependencias runtime, prueba y compila Angular; construye las tres imágenes; y bloquea vulnerabilidades corregibles de severidad alta o crítica encontradas por Trivy.

## Evidencia visual

Tablero autenticado ejecutándose sobre el stack completo:

![Tablero colaborativo](docs/images/scrumboard-board.png)

Modelo de datos PostgreSQL (la definición detallada está en [`docs/data-model.md`](docs/data-model.md)):

![Modelo de base de datos](docs/images/database-model.svg)

## Roadmap

El estado actual, deuda técnica y próximos incrementos están en [`docs/ROADMAP.md`](docs/ROADMAP.md).

## Declaración de uso de IA

Se utilizó un asistente de IA (OpenCode con un modelo de OpenAI) como apoyo en el análisis, implementación, pruebas, contenedores, CI y documentación. Las decisiones de arquitectura, la revisión del resultado y la responsabilidad sobre el código publicado permanecen a cargo del autor del proyecto.
