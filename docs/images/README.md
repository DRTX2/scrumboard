# Evidencia visual

Este directorio contiene evidencia visual revisada. No agregue imágenes vacías, datos inventados ni capturas con secretos.

## Captura del producto

`scrumboard-board.png` fue generada mediante Playwright contra el stack Docker completo y muestra el proyecto semilla autenticado.

1. Inicie el stack con `docker compose up --build`.
2. Entre con una cuenta demo y abra el proyecto `ScrumBoard Launch`.
3. Capture el tablero completo a un ancho aproximado de 1440 px, mostrando columnas, tareas, filtros y presencia si hay dos sesiones.
4. Recorte navegador y escritorio cuando no aporten contexto; elimine tokens, consola, correo personal u otros datos sensibles.
5. Añada texto alternativo descriptivo al referenciar la imagen en `README.md`.

## Imagen de base de datos

`database-model.svg` resume las relaciones principales y los campos de concurrencia. El Mermaid de [`../data-model.md`](../data-model.md) permanece como fuente canónica detallada. La imagen no contiene credenciales, cadenas de conexión ni datos personales.

Al actualizar entidades o migraciones, modifique primero el diagrama fuente y regenere la imagen para evitar divergencias.
