# SuperStockAPI — Cassandra Edition con Backup & Restore

API REST de gestión de inventario sobre un clúster **Apache Cassandra de 2 nodos**, con soporte para **backup y restauración** mediante snapshots. Toda la infraestructura se orquesta con Docker Compose.

## Tabla de contenidos

- [Stack tecnológico](#-stack-tecnológico)
- [Arquitectura](#-arquitectura)
- [Requisitos previos](#-requisitos-previos)
- [Inicio rápido](#-inicio-rápido)
- [Carga inicial de datos (seed)](#-carga-inicial-de-datos-seed)
- [Endpoints principales](#-endpoints-principales)
- [Sistema de backup y restauración](#-sistema-de-backup-y-restauración)
- [Verificación del clúster](#-verificación-del-clúster)
- [Estructura del proyecto](#-estructura-del-proyecto)
- [Troubleshooting](#-troubleshooting)

---

## Stack tecnológico

| Componente | Tecnología |
|---|---|
| Backend | .NET 10 / ASP.NET Core |
| Base de datos | Apache Cassandra (clúster de 2 nodos) |
| Driver | CassandraCSharpDriver 3.22.0 |
| Autenticación | JWT + BCrypt |
| Orquestación | Docker Compose |
| Arquitectura | Clean Architecture (Domain, Application, Infrastructure, API) |

---

##  Arquitectura

```
┌──────────────────────────────────────────────────┐
│              docker-compose stack                 │
│                                                   │
│  ┌──────────────┐    ┌──────────────┐            │
│  │ cassandra-   │◄──►│ cassandra-   │  RF=2      │
│  │   seed       │    │   node2      │  dc1       │
│  │ (172.x.0.2)  │    │ (172.x.0.3)  │  rack1     │
│  └──────┬───────┘    └──────┬───────┘            │
│         │                   │                     │
│         └────────┬──────────┘                     │
│                  │                                │
│         ┌────────▼─────────┐                      │
│         │ superstock-api   │  .NET 10             │
│         │ (puerto 8080)    │                      │
│         └──────────────────┘                      │
└──────────────────────────────────────────────────┘
```

**Alta disponibilidad** garantizada por:
- `ReplicationFactor = 2` (cada dato existe en ambos nodos)
- `ConsistencyLevel = LocalOne` (basta con un nodo vivo para responder)
- `DCAwareRoundRobinPolicy` (balanceo entre nodos del DC local)

---

## Requisitos previos

- **Docker Desktop** con al menos **3 GB de RAM** asignados
- **PowerShell 5+** (Windows) para scripts de backup
- Puertos libres: `8080` (API) y `9042` (Cassandra)

---

## Inicio rápido

### 1. Limpiar contenedores previos (opcional)

Si tienes contenedores Cassandra anteriores corriendo:

```powershell
docker stop cassandra-seed cassandra-node2 cassandra-node3 2>$null
docker rm cassandra-seed cassandra-node2 cassandra-node3 2>$null
```

### 2. Levantar el stack completo

Desde la raíz del proyecto:

```powershell
docker-compose up -d --build
```

> La primera vez tarda **3-5 minutos** (compila la imagen de la API y descarga Cassandra).

### 3. Verificar que los nodos estén operativos

Espera ~90 segundos y verifica:

```powershell
docker exec -it cassandra-seed nodetool status
```

Ambos nodos deben aparecer con estado **`UN`** (Up/Normal):

```
Datacenter: dc1
=======================
Status=Up/Down
|/ State=Normal/Leaving/Joining/Moving
--  Address     Load      Tokens  Owns  Host ID   Rack
UN  172.x.x.x   ...       16      ?     ...       rack1
UN  172.x.x.x   ...       16      ?     ...       rack1
```

### 4. Acceder a la API

- **Swagger UI:** http://localhost:8080/swagger
- **Credenciales por defecto:** `admin@superstock.sv` / `Admin123!`

---

##  Carga inicial de datos (seed)

El archivo `seed.db` contiene un script CQL con datos de prueba (usuarios, proveedores, productos y ventas).

### Ejecutar el seed

```powershell
# Copiar el script al contenedor
docker cp seed.db cassandra-seed:/tmp/seed.db

# Ejecutarlo con cqlsh
docker exec -it cassandra-seed cqlsh -f /tmp/seed.db
```

### Verificar la carga

```powershell
docker exec -it cassandra-seed cqlsh -e "SELECT count(*) FROM superstock.productos;"
docker exec -it cassandra-seed cqlsh -e "SELECT count(*) FROM superstock.proveedores;"
docker exec -it cassandra-seed cqlsh -e "SELECT nombre, stock_actual FROM superstock.productos;"
```

> Si aparecen warnings sobre "ya existe", es seguro ignorarlos — significa que algunos registros ya estaban cargados.

---

##  Endpoints principales

| Método | Ruta | Descripción | Rol |
|---|---|---|---|
| `POST` | `/api/account/login` | Obtener JWT token | público |
| `POST` | `/api/account/register` | Registrar usuario | admin |
| `GET` | `/api/producto` | Buscar productos | autenticado |
| `GET` | `/api/producto/{id}` | Obtener producto por ID | autenticado |
| `GET` | `/api/producto/barcode/{codigo}` | Buscar por código de barras | autenticado |
| `POST` | `/api/producto` | Crear producto | admin, bodeguero |
| `PUT` | `/api/producto/{id}` | Actualizar producto | admin, bodeguero |
| `DELETE` | `/api/producto/{id}` | Soft delete | admin |
| `GET` | `/api/proveedor` | Buscar proveedores | autenticado |
| `POST` | `/api/proveedor` | Crear proveedor | admin, gerente |
| `GET` | `/api/venta` | Buscar ventas | autenticado |
| `POST` | `/api/venta` | Registrar venta | admin, cajero |
| `PATCH` | `/api/venta/{id}/anular` | Anular venta | admin, gerente |
| `GET` | `/api/reporte/ventas-por-dia` | Reporte de ventas diarias | admin, gerente |
| `GET` | `/api/reporte/productos-stock-bajo` | Productos con stock crítico | admin, gerente |
| `GET` | `/api/reporte/ventas-por-categoria` | Top productos vendidos | admin, gerente |

---

##  Sistema de backup y restauración

El proyecto incluye scripts para crear y restaurar snapshots del clúster Cassandra.

### Estructura

```
scripts/
├── backup-manual.ps1        ← Backup desde PowerShell (Windows)
├── restore-manual.ps1       ← Restore desde PowerShell (Windows)
├── restore-manual.sh        ← Restore desde Bash (Linux/Mac)
└── medusa-auto-backup.sh    ← Backup automatizado (estilo Medusa)

backups/
└── manual/
    └── backup_YYYYMMDD_HHMMSS/
        ├── cassandra-seed.tar.gz
        └── cassandra-node2.tar.gz
```

### 📦 Crear un backup manual

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\backup-manual.ps1
```

Esto ejecuta `nodetool snapshot` en ambos nodos, comprime los archivos y los descarga a `./backups/manual/`. El **tag** del backup se genera automáticamente con timestamp:

```
=== BACKUP ===
Tag: backup_20260524_143000

[cassandra-seed] snapshot...
[cassandra-seed] exportar a PC...
  OK: backups\manual\backup_20260524_143000\cassandra-seed.tar.gz
[cassandra-node2] snapshot...
[cassandra-node2] exportar a PC...
  OK: backups\manual\backup_20260524_143000\cassandra-node2.tar.gz
```

**Apunta el tag** — lo necesitas para restaurar.

###  Restaurar un backup

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\restore-manual.ps1 -Tag backup_20260524_143000
```

> **Advertencia:** El proceso de restore detiene la API, trunca las tablas del keyspace y restaura los datos del snapshot.

###  Backup automatizado (Medusa)

Si prefieres ejecutar el backup desde dentro del contenedor:

```powershell
docker exec cassandra-seed bash /scripts/medusa-auto-backup.sh
```

Esto crea un snapshot interno usando `nodetool snapshot` con timestamp automático.

### Listar snapshots existentes

```powershell
docker exec -it cassandra-seed nodetool listsnapshots
```

### Eliminar snapshots viejos del contenedor

```powershell
docker exec -it cassandra-seed nodetool clearsnapshot superstock
```

---

## Verificación del clúster

### Estado de los nodos

```powershell
docker exec -it cassandra-seed nodetool status
```

### Información detallada del clúster

```powershell
docker exec -it cassandra-seed nodetool describecluster
```

### Información del nodo seed

```powershell
docker exec -it cassandra-seed nodetool info
```

### Explorar el keyspace

```powershell
docker exec -it cassandra-seed cqlsh -e "DESCRIBE KEYSPACE superstock"
```

### Logs de la API

```powershell
docker-compose logs -f superstock-api
```

---

##  Estructura del proyecto

```
SuperStockAPI_V2/
├── docker-compose.yml              ← Orquesta el stack (cluster + API)
├── seed.db                         ← Script CQL con datos de prueba
├── README.md
│
├── SuperStock.API/                 ← Capa de presentación
│   ├── Controllers/
│   ├── DTOs/
│   ├── Helpers/
│   ├── Dockerfile
│   ├── Program.cs
│   └── appsettings.json
│
├── SuperStock.Application/         ← Casos de uso
│   └── Services/
│
├── SuperStock.Domain/              ← Entidades e interfaces
│   ├── Entities/
│   └── Interfaces/
│
├── SuperStock.Infrastructure/      ← Persistencia y seguridad
│   ├── Persistence/
│   │   ├── CassandraDbContext.cs
│   │   └── Repositories/
│   ├── Security/
│   └── Settings/
│
├── scripts/                        ← Scripts de backup/restore
│   ├── backup-manual.ps1
│   ├── restore-manual.ps1
│   ├── restore-manual.sh
│   └── medusa-auto-backup.sh
│
└── backups/                        ← Almacén local de snapshots
    └── manual/
```

---

##  Troubleshooting

### El contenedor de la API se cae al arrancar

**Causa común:** Cassandra aún no está lista cuando la API intenta conectarse. La API implementa reintentos automáticos (hasta 12 intentos cada 10s), así que normalmente se resuelve solo.

**Solución manual:**
```powershell
docker-compose restart superstock-api
docker-compose logs --tail=30 superstock-api
```

### Error "Not enough replicas available for query at consistency QUORUM"

Pasa cuando el clúster acaba de crear el keyspace y aún no propagó datos al segundo nodo. La API ya usa `LocalOne` para evitar esto, pero si lo cambias a `QUORUM` y aparece, simplemente reinicia la API después de unos segundos.

### Conflict: container name already in use

Hay contenedores residuales de un stack anterior:
```powershell
docker stop cassandra-seed cassandra-node2 cassandra-node3 2>$null
docker rm cassandra-seed cassandra-node2 cassandra-node3 2>$null
docker-compose up -d
```

### El build de Docker falla con error de NuGet (rutas de Windows)

Indica que el proyecto contiene un `NuGet.Config` con rutas locales de Visual Studio. El Dockerfile ya genera uno limpio que apunta solo a `nuget.org`, así que reconstruye sin cache:
```powershell
docker-compose build --no-cache superstock-api
```

### Detener y limpiar todo

```powershell
docker-compose down       # detiene contenedores (conserva datos)
docker-compose down -v    # detiene y borra TODOS los datos
```

---

## 📊 Consultas útiles en cqlsh

```sql
-- Entrar a cqlsh
docker exec -it cassandra-seed cqlsh

-- Una vez dentro:
USE superstock;

-- Ver todas las tablas
DESCRIBE TABLES;

-- Contar registros por tabla
SELECT count(*) FROM productos;
SELECT count(*) FROM ventas;
SELECT count(*) FROM proveedores;
SELECT count(*) FROM usuarios;

-- Ver productos activos con stock bajo
SELECT nombre, stock_actual, stock_minimo FROM productos WHERE activo = true ALLOW FILTERING;

-- Ver replicación del keyspace
DESCRIBE KEYSPACE superstock;

-- Salir
EXIT;
```

---

## 📝 Notas técnicas

- **IDs:** se usa `Guid` (UUID en CQL) en todas las entidades
- **Detalles del producto:** `map<text,text>` para flexibilidad
- **Items de venta:** serializados como JSON en columna `text`
- **Soft delete:** las eliminaciones marcan `is_deleted = true`
- **Reportes:** agregaciones en memoria con LINQ (Cassandra no es DB analítica)
- **Reintentos:** la API espera hasta 2 minutos a que Cassandra esté lista al arrancar

---

## 👥 Autores

Proyecto académico de **Base de Datos II** — Universidad Evangélica de El Salvador, Ciclo 01-2026.
