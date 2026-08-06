# Roadmap de ScrumBoard

Este roadmap separa endurecimiento necesario de evolución funcional. Las fechas se decidirán con métricas de uso y prioridad de producto; el orden indica dependencia técnica, no un compromiso de entrega.

## Estado actual

- API REST versionada con autenticación JWT, autorización por membresía y Problem Details.
- Proyectos, columnas y tareas con concurrencia optimista mediante ETags.
- Shell Angular 17/PrimeNG Sakai responsive en español `es-EC`, con filtros, paginación por cursor y colaboración SignalR.
- Idempotencia para POST autenticados y almacenamiento de respuestas exitosas.
- Reportes PDF/XLSX síncronos con paridad semántica y límite de 10000 tareas; persistencia PostgreSQL mediante EF Core.
- Playwright sobre el stack sembrado para móvil, autorización, descarga y colaboración SignalR owner/member en contextos aislados.
- Migraciones desacopladas, imágenes multi-stage, Compose con healthchecks y CI con Trivy.

## Próximo: operación segura

- Implementar purga programada en lote de registros idempotentes vencidos.
- Separar credenciales PostgreSQL de migración y runtime con privilegio mínimo.
- Exportar trazas y métricas OpenTelemetry a un collector, con dashboards y alertas.
- Añadir logs estructurados con redacción de tokens, claves y datos personales.
- Publicar imágenes por digest, generar SBOM y firmar artefactos de release.
- Fijar GitHub Actions por SHA y habilitar actualización automatizada de dependencias.
- Migrar Angular y PrimeNG a una línea corregida cuando el requisito de Angular 17 deje de aplicar.

## Siguiente: calidad y resiliencia

- Incorporar pruebas de contrato OpenAPI.
- Ampliar Playwright con reconexión SignalR, conflictos ETag y reintentos idempotentes; la colaboración básica de dos contextos y la descarga filtrada ya están cubiertas.
- Mantener concurrencia, migraciones y límites de reportes en las suites backend existentes, sin fijar en documentación un conteo de casos.
- Definir objetivos de cobertura útiles por capa, sin convertir el porcentaje en el único criterio.
- Añadir backups, restauraciones verificadas y pruebas de actualización/rollback de esquema.
- Medir límites de tamaño, tiempo y memoria para reportes y cuerpos idempotentes.

## Después: escalado

- Elegir Azure SignalR Service o Redis para fan-out y mover presencia/versionado a almacenamiento distribuido; un backplane sin presencia distribuida no habilita réplicas seguras.
- Desacoplar eventos posteriores al commit mediante outbox para evitar pérdida de notificaciones.
- Evaluar caché de snapshots solo si las métricas justifican su invalidación por `board_version`; actualmente no existe caché dinámica del tablero.
- Ejecutar reportes pesados en jobs asíncronos con estado, cuotas, almacenamiento temporal privado y descarga expirable autorizada.
- Evaluar réplicas de lectura y partición solo después de medir cuellos de botella reales.

## Producto

- Administración de miembros e invitaciones con auditoría de cambios de rol.
- Historial de actividad y recuperación de acciones del tablero.
- Comentarios, etiquetas, adjuntos y notificaciones configurables.
- Sprints, estimaciones, burndown y límites WIP.
- Auditoría completa WCAG 2.2 AA e internacionalización adicional; la interfaz actual ya tiene layout móvil y localización `es-EC`.
- Búsqueda de texto completo y filtros guardados por usuario.

## Fuera de alcance inmediato

- Microservicios: el monolito modular actual reduce complejidad y conserva límites claros.
- Kubernetes: primero deben existir requisitos de disponibilidad, escalado y operación que lo justifiquen.
- Edición offline: requiere una estrategia de resolución de conflictos distinta al ETag actual.
