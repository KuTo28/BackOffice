# Altairis Backoffice

## La aplicación

Altairis Backoffice es una aplicación web para la gestión de operaciones hoteleras. Está compuesta por:

- **Frontend:** Next.js y React.
- **Backend:** ASP.NET Core Web API con Entity Framework Core.
- **Base de datos:** PostgreSQL.
- **Ejecución local:** Docker Compose.

El panel permite consultar el resumen de actividad, buscar hoteles con paginación, administrar tipos de habitación, configurar inventario por fechas y consultar reservas y sus trazas de operaciones.

## Puesta en marcha

Se necesita Docker Desktop con Docker Compose. Desde la carpeta raíz del proyecto:

```powershell
docker compose up --build
```

Cuando los servicios estén listos:

- Aplicación: <http://localhost:3000>
- API y documentación interactiva (Scalar): <http://localhost:8080/scalar/v1>

La API espera a que PostgreSQL esté disponible y aplica las migraciones pendientes al arrancar. PostgreSQL guarda sus datos en el volumen `postgres_data`, por lo que reiniciar los contenedores no elimina los datos.

Los puertos y la configuración de PostgreSQL se pueden personalizar mediante variables de entorno, como `WEB_PORT`, `API_PORT`, `POSTGRES_PORT`, `POSTGRES_USER`, `POSTGRES_PASSWORD` y `POSTGRES_DB`. No se deben utilizar credenciales de demostración en un entorno expuesto o de producción.

### Ejecutar las pruebas

El proyecto incluye pruebas unitarias del servicio de disponibilidad. Se pueden ejecutar localmente con:

```powershell
dotnet test .\AltairisBackoffice.Api.Tests\AltairisBackoffice.Api.Tests.csproj
```

También están disponibles como un servicio opcional de Compose:

```powershell
docker compose --profile test run --build --rm api-tests
```

Las pruebas actuales cubren validación de fechas, hoteles inexistentes e inactivos, inventario limitado y free sale, noches sin configurar y capacidad agotada. No requieren una conexión a PostgreSQL.

## Uso del backoffice

Se recomienda configurar los datos en este orden:

### 1. Crear un hotel

1. Abre **Hotels** y selecciona **Add hotel**.
2. Introduce el nombre y el correo electrónico; completa también teléfono, dirección, ciudad, país y descripción según corresponda.
3. Deja activo el hotel si debe estar disponible para la operativa y guarda los cambios.
4. Usa la búsqueda y la paginación para localizar hoteles cuando haya muchos registros.

Un hotel inactivo no admite nuevas operaciones de venta del flujo de reservas.

### 2. Añadir tipos de habitación

1. Abre **Room types** y selecciona el hotel correspondiente.
2. Pulsa **Add room type**.
3. Indica el nombre, la descripción y el precio base por noche.
4. Guarda el tipo de habitación.

Los tipos de habitación pertenecen a un hotel y se utilizan al consultar disponibilidad, configurar inventario y crear reservas.

### 3. Configurar disponibilidad e inventario

1. Abre **Inventory** y elige un hotel.
2. En el calendario, selecciona una fecha para ver el estado y el desglose de inventario por tipo de habitación.
3. Para configurar una fecha concreta, edita la capacidad del tipo de habitación correspondiente. La capacidad no puede reducirse por debajo de las reservas que ya la ocupan.
4. Para aplicar una configuración a varias fechas y tipos de habitación, selecciona **Create allotment**, indica el intervalo inclusivo, la capacidad y los tipos de habitación y guarda.
5. Activa **Free sale** cuando quieras que esas fechas no tengan un límite de capacidad de venta.

Las fechas sin configurar se muestran como no disponibles. En inventario limitado, las reservas activas consumen capacidad; las reservas canceladas dejan de hacerlo. El calendario distingue fechas disponibles, con disponibilidad limitada y no disponibles.

### 4. Consultar y gestionar reservas

En **Bookings** se pueden filtrar reservas por hotel, fecha de check-in, fecha de creación y estado; también se pueden ordenar y paginar los resultados. Al abrir una reserva se muestran sus datos y las trazas disponibles de las operaciones.

El backend implementa la creación de reservas mediante estos pasos:

1. **Availability:** `GET /api/Booking/availability` consulta el inventario en caché para el hotel y las fechas solicitadas.
2. **Quote:** `POST /api/Booking/quote` valida en la base de datos la selección de tipos de habitación y crea una cotización temporal válida durante 30 segundos.
3. **Confirm:** `POST /api/Booking/quote/{quoteId}/confirm` vuelve a validar el inventario y, si sigue disponible, crea la reserva y asocia los datos del huésped.

La cotización se conserva en memoria y no se persiste en la base de datos. La confirmación vuelve a validar el inventario y realiza las escrituras dentro de una transacción para protegerse frente a cambios concurrentes. Las reservas existentes no se pueden editar: solo se pueden cancelar. La cancelación libera el inventario y se registra junto con las operaciones de cotización y confirmación en las trazas de la reserva.

> **Nota:** el panel de reservas ofrece un formulario de creación. El flujo documentado de la API es el proceso de referencia para disponibilidad, cotización y confirmación; la creación desde el panel debe validarse contra ese flujo en la versión desplegada.

## Mejoras futuras

- Incorporar tarifas asignables a habitaciones, con restricciones, ofertas, servicios complementarios, suplementos y precios según el régimen.
- Añadir **stop sales** (cierres de venta) para fechas y tipos de habitación.
- Incorporar comisiones de venta.
- Añadir el estado **RQ** a las reservas.
- Crear un registro de cambios que permita consultar y revertir modificaciones a estados anteriores.
- Añadir inicio de sesión y gestión de usuarios para identificar quién realiza cada cambio.
- Notificar al hotel cuando se confirme una reserva.
- Mejorar la experiencia móvil para que todas las funciones puedan utilizarse correctamente desde un teléfono.
