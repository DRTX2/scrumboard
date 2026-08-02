# Roadmap de ScrumBoard

Este roadmap separa endurecimiento necesario de evolución funcional. Las fechas se decidirán con métricas de uso y prioridad de producto; el orden indica dependencia técnica, no un compromiso de entrega.

## Estado actual

- API REST versionada con autenticación JWT, autorización por membresía y Problem Details.
- Proyectos, columnas y tareas con concurrencia optimista mediante ETags.
- Tablero Angular con filtros y colaboración SignalR.
- Idempotencia para POST autenticados y almacenamiento de respuestas exitosas.
- Reportes PDF/XLSX y persistencia PostgreSQL mediante EF Core.
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

- Incorporar pruebas de contrato OpenAPI y escenarios E2E contra el stack completo.
- Probar concurrencia, reintentos idempotentes, reconexión SignalR y generación de reportes grandes.
- Definir objetivos de cobertura útiles por capa, sin convertir el porcentaje en el único criterio.
- Añadir backups, restauraciones verificadas y pruebas de actualización/rollback de esquema.
- Medir límites de tamaño, tiempo y memoria para reportes y cuerpos idempotentes.

## Después: escalado

- Mover presencia a almacenamiento distribuido y añadir backplane administrado para SignalR.
- Desacoplar eventos posteriores al commit mediante outbox para evitar pérdida de notificaciones.
- Incorporar caché de lecturas con invalidación basada en `board_version`.
- Ejecutar reportes pesados en trabajos asíncronos con descarga temporal autorizada.
- Evaluar réplicas de lectura y partición solo después de medir cuellos de botella reales.

## Producto

- Administración de miembros e invitaciones con auditoría de cambios de rol.
- Historial de actividad y recuperación de acciones del tablero.
- Comentarios, etiquetas, adjuntos y notificaciones configurables.
- Sprints, estimaciones, burndown y límites WIP.
- Accesibilidad WCAG 2.2 AA, internacionalización y experiencia móvil validada.
- Búsqueda de texto completo y filtros guardados por usuario.

## Fuera de alcance inmediato

- Microservicios: el monolito modular actual reduce complejidad y conserva límites claros.
- Kubernetes: primero deben existir requisitos de disponibilidad, escalado y operación que lo justifiquen.
- Edición offline: requiere una estrategia de resolución de conflictos distinta al ETag actual.
