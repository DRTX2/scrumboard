# Arquitectura

## Vista de contenedores

```mermaid
flowchart LR
    U[Usuario / navegador]

    subgraph Host[Host Docker]
        FE[Angular 17\nnginx no privilegiado :8080]
        API[ASP.NET Core 8 API :8080\nREST + SignalR + health]
        MIG[Migrador EF Core\none-shot]
        DB[(PostgreSQL 16\nvolumen persistente)]
    end

    U -->|HTTP :8080| FE
    U -->|REST /api + JWT\nHTTP :5000| API
    U <-->|SignalR /hubs/boards| API
    API -->|Npgsql| DB
    MIG -->|migraciones EF Core| DB
    DB -. healthcheck .-> MIG
    MIG -. finaliza con éxito .-> API
    API -. ready .-> FE
```

El frontend recibe `API_BASE_URL` y `HUB_URL` al iniciar el contenedor. Esas URL deben ser accesibles desde el navegador, no solo desde la red interna de Compose.

## Capas del backend

```mermaid
flowchart TB
    HTTP[Controllers, middleware y SignalR\nScrumBoard.Api]
    APP[Casos de uso y puertos\nScrumBoard.Application]
    DOMAIN[Entidades e invariantes\nScrumBoard.Domain]
    INFRA[EF Core, JWT, PBKDF2 y reportes\nScrumBoard.Infrastructure]
    PG[(PostgreSQL)]
    FILES[PDF / XLSX]

    HTTP --> APP
    APP --> DOMAIN
    INFRA --> APP
    INFRA --> DOMAIN
    HTTP --> INFRA
    INFRA --> PG
    INFRA --> FILES
```

Las dependencias de código apuntan hacia dominio y aplicación. Infraestructura implementa los puertos y la API compone el proceso. El migrador reutiliza el contexto de infraestructura sin acoplar la migración al arranque web.

## Flujo de una mutación

```mermaid
sequenceDiagram
    actor C as Cliente
    participant A as API
    participant S as Servicio de aplicación
    participant D as PostgreSQL
    participant H as Hub SignalR

    C->>A: PUT/PATCH + Bearer + If-Match
    A->>S: Comando y versión esperada
    S->>D: Leer entidad y membresía
    S->>S: Validar versión e invariantes
    S->>D: Guardar nueva versión
    D-->>S: Commit
    S->>H: Publicar evento del tablero
    H-->>C: TaskUpdated / TaskMoved / ColumnChanged
    A-->>C: 200 + ETag + X-Board-ETag
```

Para `POST` autenticados, el middleware de idempotencia envuelve este flujo, identifica usuario/ruta/clave, compara el hash del cuerpo y conserva únicamente respuestas exitosas.

## Decisiones operativas

- PostgreSQL determina disponibilidad mediante `pg_isready`.
- El migrador espera una base sana y debe finalizar antes de la API.
- La API expone checks HTTP y el frontend espera a que esté listo.
- API y migrador usan filesystem de solo lectura con `/tmp` efímero.
- La presencia SignalR es local al proceso; una segunda réplica requiere estado distribuido.
- `/health/live` comprueba el proceso sin depender de PostgreSQL; `/health/ready` incluye la conectividad con la base de datos.
