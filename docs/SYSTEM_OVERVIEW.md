# Estado técnico actual

## Arquitectura

- API: ASP.NET Core con arquitectura por capas (API, Application, Domain, Infrastructure).
- Persistencia: Entity Framework Core sobre MySQL.
- Autenticación: JWT y roles.
- Entidades operativas: vehículos, conductores, rutas y posiciones de rutas.
- Frontend relacionado: repositorio hermano `Front_gpsR`.

## Contrato actual de rutas

La API expone `POST /api/Routes` y `PUT /api/Routes/{id}`. El contrato recibe vehículo, conductor, nombre, origen, destino, puntos, carga y fechas planificadas.

La entidad de dominio mantiene origen/destino y también una lista ordenada de puntos. Por definición de negocio, los puntos intermedios son múltiples operaciones de una misma ruta —recogidas, entregas o ambas en la misma parada— y no son rutas independientes. La siguiente revisión debe garantizar que esa representación no duplique semánticamente origen y destino y que cada parada tenga un tipo operativo combinable, estado y evidencia sin romper las rutas existentes.

## Próxima línea de trabajo

1. Recuperar un entorno .NET reproducible.
2. Probar autenticación y permisos por rol.
3. Probar CRUD de vehículos, conductores y rutas.
4. Definir reglas de disponibilidad y conflictos.
5. Alinear DTOs frontend/backend.
6. Modelar el estado y la evidencia por parada sin romper las rutas existentes.
7. Añadir pruebas automatizadas para los flujos operativos principales.
