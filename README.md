# SuperStock SV - API REST con MongoDB

Sistema Inteligente de Gestión de Inventario para Supermercados.
Proyecto de Cátedra - Base de Datos II (No Relacionales) - Entrega 2.

## Requisitos Previos

- .NET 10 SDK
- Docker Engine + Docker Compose v2 (`docker compose`)
- Un editor como Visual Studio, Rider o VS Code

## MongoDB Replica Set local (3 nodos)

La API usa `WriteConcern.WMajority` y `ReadConcern.Majority`, por lo que requiere un **replica set** (no un `mongod` standalone).

### 1. Levantar el clúster completo

Desde la raíz del repositorio:

```bash
docker compose up -d
```

El `docker-compose.yml` levanta en una sola red interna (`mongo-cluster`):

- `mongo1`, `mongo2`, `mongo3` — nodos del replica set `rs0`
- `mongo-init` — inicializa el replica set automáticamente con `rs.initiate` (se ejecuta una sola vez y termina)
- `superstock-api` — la API, que espera a que `mongo-init` complete antes de arrancar

Puertos expuestos en el host (para herramientas externas como Compass):

| Servicio | Puerto host |
|----------|-------------|
| mongo1   | 27018       |
| mongo2   | 27019       |
| mongo3   | 27020       |

Comandos útiles:

```bash
docker compose down          # detener y eliminar contenedores
docker compose logs -f       # seguir logs de todos los servicios
docker compose ps            # estado de los contenedores
```

### 2. Connection string

**Dentro de Docker** (la API en contenedor): la variable de entorno `MongoDb__ConnectionString` del compose usa los nombres de servicio internos:

```text
mongodb://mongo1:27017,mongo2:27017,mongo3:27017/superstock_db?replicaSet=rs0
```

**Desarrollo local** (`dotnet run`): `SuperStock.API/appsettings.Development.json` apunta a los puertos del host:

```text
mongodb://localhost:27018,localhost:27019,localhost:27020/superstock_db?replicaSet=rs0
```

`appsettings.json` puede seguir apuntando a Atlas u otro entorno; al ejecutar con `ASPNETCORE_ENVIRONMENT=Development` (por defecto en `dotnet run`) se usa el replica set local.

### 3. Probar failover (opcional)

```bash
docker stop mongo1
docker compose logs superstock-api   # la API continúa operando
docker start mongo1
```

Tras unos segundos, `mongo2` o `mongo3` habrá sido elegido `PRIMARY`; la API no necesita cambiar la URI.

## Configuración

`SuperStock.API/appsettings.json` — configuración base (p. ej. Atlas en producción).

`SuperStock.API/appsettings.Development.json` — replica set local Docker (ver arriba).

JWT y demás claves siguen en `appsettings.json`:

```json
{
  "JWTKey": "clave-super-secreta-superstock-sv-2026-muy-larga-123456",
  "JWTIssuer": "superstock-api",
  "JWTLifeTime": 10
}
```

## Ejecución

```bash
cd SuperStock.API
dotnet restore
dotnet run
```

La API se ejecuta en `https://localhost:5001` (o el puerto asignado). 
Swagger UI disponible en: `https://localhost:5001/swagger`

Al iniciar, el sistema crea automáticamente:
- Índices secundarios en MongoDB para optimizar consultas.
- Schema Validation ($jsonSchema) en las colecciones productos y ventas.
- TTL Index para limpiar ventas anuladas después de 90 días.
- Usuario admin por defecto: `admin@superstock.sv` / `Admin123!`

## Arquitectura

Proyecto en Clean Architecture con 4 capas:

```
SuperStock.slnx
├── SuperStock.Domain          → Entidades, Interfaces (sin dependencias externas)
├── SuperStock.Application     → Servicios de negocio (BCrypt para auth)
├── SuperStock.Infrastructure  → MongoDB.Driver, JWT, repositorios
└── SuperStock.API             → Controllers, DTOs, Program.cs
```

Base de datos: **MongoDB** (reemplaza SQL Server + EF Core del proyecto base Tickets).
Autenticación: **JWT + BCrypt** (reemplaza ASP.NET Identity).

## Endpoints de la API

### Autenticación

| Método | Ruta | Descripción | Acceso |
|--------|------|-------------|--------|
| POST | `/api/account/login` | Login, retorna JWT | Público |
| POST | `/api/account/register` | Registrar usuario | Admin |

### Productos (CRUD + Filtros + Paginación)

| Método | Ruta | Descripción | Acceso |
|--------|------|-------------|--------|
| GET | `/api/producto` | Buscar con filtros y paginación | Autenticado |
| GET | `/api/producto/{id}` | Obtener por ID | Autenticado |
| GET | `/api/producto/barcode/{codigo}` | Obtener por código de barras | Autenticado |
| POST | `/api/producto` | Crear producto | Admin, Bodeguero |
| PUT | `/api/producto/{id}` | Actualizar producto | Admin, Bodeguero |
| DELETE | `/api/producto/{id}` | Eliminar producto (soft delete) | Admin |

**Filtros disponibles en GET /api/producto:**
- `categoria` → Filtra por categoría exacta (perecederos, secos, cuidado_personal, limpieza)
- `nombre` → Búsqueda parcial por nombre (case-insensitive)
- `stockBajo` → true para mostrar solo productos con stock <= mínimo
- `activo` → true/false para filtrar por estado
- `page` → Número de página (default: 1)
- `pageSize` → Resultados por página (default: 10)

### Ventas

| Método | Ruta | Descripción | Acceso |
|--------|------|-------------|--------|
| GET | `/api/venta` | Buscar con filtros y paginación | Autenticado |
| GET | `/api/venta/{id}` | Obtener por ID | Autenticado |
| POST | `/api/venta` | Registrar venta (descuenta stock) | Admin, Cajero |
| PATCH | `/api/venta/{id}/anular` | Anular venta (restaura stock) | Admin, Gerente |

**Filtros disponibles en GET /api/venta:**
- `fechaDesde` / `fechaHasta` → Rango de fechas
- `cajeroId` → Filtrar por cajero
- `estado` → completada / anulada

### Proveedores (CRUD + Filtros + Paginación)

| Método | Ruta | Descripción | Acceso |
|--------|------|-------------|--------|
| GET | `/api/proveedor` | Buscar con filtros | Autenticado |
| GET | `/api/proveedor/{id}` | Obtener por ID | Autenticado |
| POST | `/api/proveedor` | Crear proveedor | Admin, Gerente |
| PUT | `/api/proveedor/{id}` | Actualizar proveedor | Admin, Gerente |
| DELETE | `/api/proveedor/{id}` | Eliminar (soft delete) | Admin |

### Reportes (Aggregation Pipeline)

| Método | Ruta | Descripción | Acceso |
|--------|------|-------------|--------|
| GET | `/api/reporte/ventas-por-dia` | Resumen diario de ventas | Admin, Gerente |
| GET | `/api/reporte/productos-stock-bajo` | Productos que necesitan restock | Admin, Gerente |
| GET | `/api/reporte/ventas-por-categoria` | Top productos vendidos | Admin, Gerente |

## Conceptos NoSQL Implementados

| Concepto | Dónde se implementa |
|----------|---------------------|
| **BSON** | MongoDB almacena todos los documentos en BSON; decimales como Decimal128 |
| **Colección** | productos, ventas, proveedores, usuarios (en MongoDbContext) |
| **Esquema dinámico** | Campo `Detalles` en Producto: Dictionary flexible por categoría |
| **Desnormalización** | ProveedorRef embebido en Producto; CajeroRef y VentaItems embebidos en Venta |
| **Embedding** | Items de venta y datos del cajero dentro del documento de Venta |
| **Schema Validation ($jsonSchema)** | Validación en colecciones productos y ventas (MongoDbContext) |
| **Campos requeridos (required)** | nombre, categoria, precioVenta, codigoBarras en productos |
| **Tipos de datos BSON (bsonType)** | string, int, decimal, array, object validados en $jsonSchema |
| **Patrón (pattern)** | Código de barras validado con regex `^[0-9]{8,14}$` |
| **Rango (minimum)** | precioVenta >= 0, stockActual >= 0 |
| **enum** | categoria limitada a valores válidos; estado y metodoPago en ventas |
| **additionalProperties** | true en productos (permite polimorfismo del campo detalles) |
| **validationLevel** | "moderate" en productos, "strict" en ventas |
| **validationAction** | "error" en ambas colecciones (rechaza documentos inválidos) |
| **collMod** | Usado para aplicar validación a colecciones existentes |
| **Índice secundario** | Índices en categoria+precio, codigoBarras (único), nombre (texto), fecha |
| **Cardinalidad** | codigoBarras tiene cardinalidad alta → índice único eficiente |
| **TTL (Time to Live)** | Ventas anuladas se borran automáticamente después de 90 días |
| **Aggregation Pipeline** | Reportes de ventas/día, stock bajo, ventas/categoría ($match, $group, $sort, $unwind, $project) |
| **Write Concern** | WMajority: escritura confirmada por mayoría del Replica Set |
| **Read Concern** | Majority: lecturas consistentes desde el Replica Set |
| **Consistencia eventual vs ACID** | Write/Read Concern Majority para transacciones críticas (ventas) |

## Estructura de Colecciones MongoDB

### productos (esquema polimórfico)
Cada producto tiene campos base comunes y un campo `detalles` flexible que varía según la categoría. Esto es el concepto de **esquema dinámico**: los documentos dentro de una misma colección no tienen exactamente la misma estructura.

### ventas (embedding / desnormalización)
Los items de cada venta se embeben directamente en el documento de venta (no se referencian por ID). Esto elimina la necesidad de JOINs en la operación más frecuente del sistema (cobro en caja).

### proveedores
Colección normalizada con referencia embebida (ProveedorRef) dentro de cada producto para lectura rápida.

### usuarios
Autenticación propia con BCrypt (sin ASP.NET Identity). Roles: admin, gerente, cajero, bodeguero.

## Estudiantes - Grupo D

- Sofía Cristina González González – 2025020179
- Rodrigo Eduardo Herrera Coto – 2025020105
- Jose Edgardo Jordan Guzman – 2025011211
- Josue Adony Morales Torres – 2025010815
- David Alexander Umaña Cortez – 2025011814
