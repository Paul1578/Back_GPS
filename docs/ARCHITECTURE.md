# Arquitectura backend

La API mantiene una arquitectura por capas dentro de un proyecto .NET:

`Controller → Command/Query → Domain → Repository/UnitOfWork → EF Core/MySQL`

JWT autentica la solicitud. La claim firmada `owner_id` define el tenant; filtros EF globales aíslan vehículos, conductores, rutas y posiciones, y las altas sellan el propietario desde la solicitud autenticada.

Las rutas almacenan puntos ordenados como JSON en `Routes.PointsJson`. Cada punto mantiene operación (`None`, `Pickup`, `Delivery`, `PickupAndDelivery`), estado y evidencias. Las mutaciones por parada validan el estado de la ruta y se vuelven a persistir en el JSON. `RoutePositions` guarda posiciones GPS históricas enlazadas con la ruta.

`POST /api/Routes` y `PUT /api/Routes/{id}` reciben `points` con origen, paradas intermedias y destino en ese orden; también reciben `origin` y `destination` explícitos para compatibilidad. El mapper de frontend conserva los extremos y excluye sólo esos dos al mostrar la lista intermedia. El estado de una parada enviado por el cliente al crear/editar se ignora y se inicia como `Pending`.

El conductor cambia estado con `PUT /api/Routes/{id}/points/{index}/status` y adjunta notas/fotos con `POST /api/Routes/{id}/points/{index}/evidence`. Completar una operación requiere evidencia y todos los puntos operativos deben quedar completados u omitidos para cerrar la ruta. La evidencia de imagen se conserva como data URL en el JSON (JPEG/PNG/WebP, máximo 4 MB); para cargas de producción a escala se recomienda mover binarios a object storage.
