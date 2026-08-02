# Modelo de datos

## Diagrama entidad-relación

```mermaid
erDiagram
    USERS {
        uuid id PK
        varchar name
        varchar email UK
        varchar password_hash
        boolean is_active
        timestamptz created_at
    }

    PROJECTS {
        uuid id PK
        varchar name
        varchar description
        date start_date
        date expected_end_date
        varchar status
        bigint version
        bigint board_version
        timestamptz created_at
        timestamptz updated_at
    }

    PROJECT_MEMBERS {
        uuid project_id PK,FK
        uuid user_id PK,FK
        varchar role
    }

    BOARD_COLUMNS {
        uuid id PK
        uuid project_id FK
        varchar name
        bigint position
        bigint version
        timestamptz created_at
        timestamptz updated_at
    }

    TASKS {
        uuid id PK
        uuid project_id FK
        uuid column_id FK
        varchar title
        varchar description
        varchar priority
        uuid assignee_id FK
        date due_date
        bigint position
        bigint version
        timestamptz created_at
        timestamptz updated_at
    }

    IDEMPOTENCY_RECORDS {
        uuid id PK
        uuid user_id
        varchar operation
        varchar key
        char request_hash
        integer status_code
        varchar content_type
        jsonb response_body
        varchar location
        timestamptz created_at
        timestamptz expires_at
        timestamptz completed_at
    }

    USERS ||--o{ PROJECT_MEMBERS : participa
    PROJECTS ||--o{ PROJECT_MEMBERS : contiene
    PROJECTS ||--o{ BOARD_COLUMNS : organiza
    PROJECTS ||--o{ TASKS : contiene
    BOARD_COLUMNS ||--o{ TASKS : agrupa
    USERS o|--o{ TASKS : asignada_a
    USERS ||--o{ IDEMPOTENCY_RECORDS : origina_logicamente
```

La última relación es lógica: `idempotency_records.user_id` forma parte del índice único, pero la configuración actual no declara una clave foránea hacia `users`.

## Integridad y concurrencia

- `project_members` usa clave primaria compuesta `(project_id, user_id)`.
- `board_columns` y `tasks` indexan sus posiciones para ordenar eficientemente; la serialización por `board_version` evita carreras entre movimientos.
- La fecha esperada de fin de un proyecto no puede ser anterior a su inicio.
- `version` es token de concurrencia en proyectos, columnas y tareas; `board_version` representa cambios agregados del tablero.
- El borrado de un proyecto elimina membresías, columnas y tareas; una columna con tareas no puede eliminarse y la FK de tareas es restrictiva.
- Al eliminar un usuario asignado, `tasks.assignee_id` pasa a `NULL`; las membresías restringen el borrado.
- La idempotencia tiene unicidad por `(user_id, operation, key)` e índice por `expires_at`.

## Índices de consulta

El modelo añade índices para correo, nombre/actualización de proyecto, membresía por usuario, tarea por asignado, tarea por prioridad y expiración idempotente. Estos índices corresponden a autenticación, listados, filtros del tablero y mantenimiento futuro.
