# Back_GPS

Backend en .NET 8 con base de datos MySQL usando Docker.

Este repositorio contiene **solo un entorno de desarrollo local**. No existe producción ni despliegue automático.
El objetivo es que cualquier persona del equipo pueda clonar el proyecto y ejecutarlo sin configuraciones complejas.

---

## 1. ¿Qué necesito para usar este proyecto?

Antes de empezar, asegúrate de tener instalado:

* Docker
* Docker Compose (incluido en Docker Desktop)
* .NET SDK 8.0 o superior

Puedes verificarlo con estos comandos:

```bash
docker --version
docker compose version
dotnet --version
```

Si alguno falla, primero instala ese componente.

---

## 2. Configuración inicial (paso más importante)

### 2.1 Crear el archivo `.env`

El proyecto necesita un archivo llamado `.env` con la configuración básica de la base de datos.

En el repositorio existe un archivo de ejemplo llamado `.env.example`.

Debes copiarlo y renombrarlo a `.env`.

Linux / macOS:

```bash
cp .env.example .env
```

Windows (PowerShell):

```powershell
copy .env.example .env
```

No es necesario modificar nada dentro del archivo para empezar.

**Importante:** el archivo `.env` no debe subirse a Git.

---

## 3. Levantar la base de datos (Docker)

El proyecto usa Docker para ejecutar MySQL y phpMyAdmin.

### Forma recomendada

Linux / macOS:

```bash
./scripts/dev-up.sh
```

Windows:

```powershell
.\scripts\dev-up.ps1
```

Estos comandos levantan automáticamente los servicios necesarios.

### Forma manual (opcional)

Si prefieres hacerlo directamente:

```bash
docker compose up -d
```

---

## 4. Verificar que todo funciona

Cuando Docker termine de levantar los servicios:

* Abre un navegador
* Entra a: [http://localhost:8080](http://localhost:8080)

Eso abrirá phpMyAdmin (herramienta visual para la base de datos).

Datos de acceso:

* Servidor: `mysql`
* Usuario: `root`
* Contraseña: `admin`

Si puedes entrar, la base de datos está funcionando correctamente.

---

## 5. Ejecutar el backend (.NET)

Con la base de datos levantada, ejecuta el backend:

```bash
dotnet watch run
```

El backend se conectará automáticamente a MySQL usando la configuración del archivo `.env`.

---

## 6. Flujo normal de trabajo

Cada vez que empieces a trabajar:

1. Asegúrate de que Docker esté abierto
2. Levanta los servicios (`dev-up` o `docker compose up -d`)
3. Ejecuta el backend con `dotnet watch run`
4. Desarrolla normalmente

Al terminar:

```bash
docker compose down
```

---

## 7. Problemas comunes

**La base de datos no inicia**

* Verifica que no tengas MySQL instalado y corriendo en tu sistema

**phpMyAdmin no conecta**

* Espera unos segundos y recarga la página

**Todo está roto y no sabes qué pasó**

```bash
docker compose down -v
docker compose up -d
```

---

## 8. Notas para usuarios Windows

* Usar Docker Desktop con WSL2 habilitado
* Ejecutar los scripts `.ps1` desde PowerShell
* No usar MySQL instalado localmente

---

## 9. Próximos pasos (planificados)

* Crear un usuario MySQL que no sea root
* Configurar migraciones con Entity Framework
* Dockerizar el backend
* Separar configuraciones de desarrollo y producción

---

Estado actual: entorno local simple, estable y fácil de levantar.
