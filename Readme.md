# Back_GPS

Backend .NET + MySQL (Docker) + phpMyAdmin

Este proyecto utiliza **Docker Compose** para levantar la base de datos y **.NET 8** para el backend.

---

## 1. Requisitos

* Docker
* Docker Compose (plugin `docker compose`)
* .NET SDK 8.0+

Verificar:

```bash
docker --version
docker compose version
dotnet --version
```

---

## 2. Variables de entorno (.env)

Crear un archivo `.env` en la raíz del proyecto (`Back_GPS/`).

```env
# =====================
# DATABASE CONFIG
# =====================
DB_HOST=127.0.0.1
DB_PORT=3306
DB_NAME=fletflow
DB_USER=root
DB_PASSWORD=admin

# =====================
# MYSQL DOCKER
# =====================
MYSQL_ROOT_PASSWORD=admin
MYSQL_DATABASE=fletflow
```

⚠️ **Nunca subir `.env` al repositorio**. Agregarlo a `.gitignore`.

---

## 3. Docker Compose

Archivo: `docker-compose.yml`

```yaml
version: "3.9"

services:
  mysql:
    image: mysql:8.0
    container_name: mysql-gps
    restart: unless-stopped
    environment:
      MYSQL_ROOT_PASSWORD: ${MYSQL_ROOT_PASSWORD}
      MYSQL_DATABASE: ${MYSQL_DATABASE}
    ports:
      - "3306:3306"
    volumes:
      - mysql_data:/var/lib/mysql
    command: --default-authentication-plugin=mysql_native_password

  phpmyadmin:
    image: phpmyadmin/phpmyadmin:latest
    container_name: phpmyadmin-gps
    restart: unless-stopped
    ports:
      - "8080:80"
    environment:
      PMA_HOST: mysql
      PMA_PORT: 3306
    depends_on:
      - mysql

volumes:
  mysql_data:
```

---

## 4. Comandos Docker

### Levantar servicios

```bash
docker compose up -d
```

### Ver estado

```bash
docker compose ps
```

### Ver logs

```bash
docker compose logs mysql
docker compose logs phpmyadmin
```

### Detener servicios

```bash
docker compose down
```

### Limpieza (opcional)

```bash
docker system prune -f
docker volume prune -f
```

---

## 5. Acceso a phpMyAdmin

URL:

```
http://localhost:8080
```

Credenciales:

* Servidor: `mysql`
* Usuario: `root`
* Password: `admin`

---

## 6. Configuración en .NET

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};User=${DB_USER};Password=${DB_PASSWORD};TreatTinyAsBoolean=false;"
  }
}
```

⚠️ En producción se recomienda **NO** dejar credenciales en `appsettings.json` y usar solo variables de entorno.

---

## 7. Dependency Injection (EF Core + MySQL)

Ejemplo recomendado:

```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.Parse("8.0.36-mysql")
    )
);
```

❌ Evitar:

```csharp
ServerVersion.AutoDetect(connectionString)
```

---

## 8. Comandos .NET

### Restaurar dependencias

```bash
dotnet restore
```

### Ejecutar backend (watch mode)

```bash
dotnet watch run
```

### Build

```bash
dotnet build
```

---

## 9. Flujo recomendado de desarrollo

1. Crear `.env`
2. `docker compose up -d`
3. Verificar phpMyAdmin
4. `dotnet watch run`
5. Desarrollar / migraciones

---

## 10. Próximos pasos (roadmap)

* [ ] Crear usuario MySQL no-root
* [ ] Migraciones EF Core
* [ ] Dockerizar backend .NET
* [ ] Separar perfiles dev / prod
* [ ] Seguridad básica MySQL

---

## 11. Notas

* MySQL corre **solo en Docker**
* El backend se conecta vía `127.0.0.1:3306`
* Los datos persisten en volumen Docker

---

**Estado actual:** entorno limpio, reproducible y listo para escalar.
