# LabTrack

Sistema de gestión de laboratorios para la Universidad Privada del Norte. Permite consultar equipos, solicitar horarios de uso, aprobar o rechazar solicitudes y registrar devoluciones. La interfaz usa amarillo lúcuma, negro y tonos neutros, con navegación diferenciada para estudiantes y encargados.

## Funcionalidades

- Acceso con ASP.NET Core Identity, contraseñas con hash, cookies de sesión y roles `Estudiante` y `Encargado`. Tres intentos fallidos consecutivos bloquean la cuenta durante 15 minutos, configurables en `Security:LockoutMinutes`.
- Catálogo paginado por Cómputo, Electrónica y Redes, con estados Disponible y Prestado.
- Solicitudes con fecha y horas de inicio y fin; historial personal con estado y observaciones.
- Aprobación con validación de conflictos y rechazo con motivo obligatorio.
- Búsqueda de préstamos activos por estudiante o equipo. Devolución con fecha efectiva y penalización automática por retraso.

## Tecnologías y estructura

ASP.NET Core MVC en .NET 10, C#, Razor, Entity Framework Core 10.0.0, SQL Server, Bootstrap 5 y JavaScript nativo. Los cuatro paquetes de EF Core y la integración de Identity están fijados en 10.0.0. La referencia a `System.Security.Cryptography.Pkcs` 9.0.21 actualiza una dependencia transitiva vulnerable sin cambiar EF Core.

```text
LabTrack.sln
LabTrack/
  Controllers/       Acceso, paneles, catálogo, reservas y administración
  Models/            Usuario, categoría, equipo y reserva
  ViewModels/        Formularios, filtros y resultados paginados
  Data/              DbContext, inicialización y migraciones
  Services/          Reglas transaccionales y reloj de Lima
  Views/             Vistas Razor y componentes compartidos
  wwwroot/           CSS, JavaScript, iconos y Bootstrap
  Properties/        Perfil de ejecución local
  appsettings.json   Configuración de conexión y seguridad
.github/workflows/sonarqube.yml
```

## Requisitos locales

Windows, SDK .NET 10 y una instancia de Microsoft SQL Server en ejecución. Visual Studio con soporte para .NET 10 es opcional. SQL Server Management Studio (SSMS) sirve para administrar el motor; su instalación por sí sola no proporciona SQL Server.

Abre `LabTrack.sln` en Visual Studio y establece `LabTrack` como proyecto de inicio, o utiliza PowerShell desde la raíz del repositorio.

## SQL Server y migraciones

La instancia predeterminada configurada es `APHN16S`, mediante autenticación integrada de Windows:

```text
Server=APHN16S;Database=LabTrackDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

La cuenta de Windows que ejecuta la inicialización debe poder crear la base de datos y modificar su esquema. Si utilizas una instancia con nombre, cambia únicamente el servidor, por ejemplo `APHN16S\SQLEXPRESS`. También puedes sobrescribir la conexión para tu sesión:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=APHN16S;Database=LabTrackDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true'
dotnet restore LabTrack.sln
dotnet tool install dotnet-ef --version 10.0.0 --tool-path "$env:LOCALAPPDATA\LabTrack\tools"
& "$env:LOCALAPPDATA\LabTrack\tools\dotnet-ef.exe" database update --project LabTrack/LabTrack.csproj
```

Si la herramienta ya está instalada, omite su instalación. `dotnet-ef --version` debe mostrar `10.0.0`. Las migraciones reales se conservan en `LabTrack/Data/Migrations`. Para cambios posteriores del modelo, crea una migración con esa misma herramienta y aplica `database update`.

En SSMS, conecta a `APHN16S` con autenticación de Windows y abre `Databases > LabTrackDb > Tables`. Las tablas incluyen Identity, `Categorias`, `Equipos`, `Reservas` y `__EFMigrationsHistory`. Existen claves foráneas restrictivas, índices de búsqueda, índices únicos para códigos y restricciones de estados y horarios.

## Cuentas y catálogo inicial

El comando `--initialize` aplica las migraciones y crea los registros que faltan. Es idempotente y no cambia las contraseñas de cuentas existentes. Ejecútalo una vez antes de iniciar la aplicación; el arranque normal no modifica el esquema ni crea cuentas.

| Identificador | Rol | Nombre | Variable de entorno para la contraseña |
|---|---|---|---|
| `encargado` | Encargado | Encargado de laboratorio | `Seed__EncargadoPassword` |
| `C00123456` | Estudiante | Ana Torres | `Seed__Estudiante1Password` |
| `C00654321` | Estudiante | Diego Ramos | `Seed__Estudiante2Password` |

Los códigos de estudiante tienen nueve caracteres y cumplen `^C00[0-9]{6}$`. El encargado utiliza un identificador independiente. No hay registro público de cuentas. La inicialización también crea las tres categorías y seis equipos, sin solicitudes ficticias.

Define contraseñas distintas de al menos ocho caracteres para las cuentas de demostración. El siguiente comando las solicita de forma oculta y las transmite al proceso mediante variables de entorno, sin incluirlas en el código ni en el historial de comandos:

```powershell
$env:Seed__EncargadoPassword = [Net.NetworkCredential]::new('', (Read-Host 'Contraseña del encargado' -AsSecureString)).Password
$env:Seed__Estudiante1Password = [Net.NetworkCredential]::new('', (Read-Host 'Contraseña de Ana' -AsSecureString)).Password
$env:Seed__Estudiante2Password = [Net.NetworkCredential]::new('', (Read-Host 'Contraseña de Diego' -AsSecureString)).Password
try {
    dotnet run --project LabTrack -- --initialize
} finally {
    Remove-Item Env:\Seed__EncargadoPassword, Env:\Seed__Estudiante1Password, Env:\Seed__Estudiante2Password -ErrorAction SilentlyContinue
}
```

Para ingresar, escribe el identificador y la contraseña elegida en el formulario de LabTrack. Identity guarda el hash en SQL Server. Las variables de entorno solo se necesitan durante la creación inicial de las cuentas.

## Ejecución con Docker Desktop

Abre Docker Desktop con contenedores Linux. Desde la carpeta que contiene `compose.yaml`, ejecuta:

```powershell
docker compose up -d --build
```

Abre http://localhost:5133. En la instalación actual ya se copiaron los usuarios y los préstamos de SQL Server de Windows. Desde entonces, los cambios que realices en Docker se guardan en su propia base. Para detener el sistema usa `docker compose stop` y para volver a levantarlo usa `docker compose up -d`. Los volúmenes conservan los datos entre ejecuciones. El parámetro `down -v` elimina esos volúmenes y los datos.

**Primera instalación en otra máquina.** Crea un archivo local `.env` junto a `compose.yaml`. Define `SQL_ADMIN_PASSWORD` y `SQL_APP_PASSWORD` con dos claves SQL distintas, de al menos 16 caracteres, que combinen mayúsculas, minúsculas y números. Usa caracteres alfanuméricos para evitar separadores de cadenas de conexión. Para una base vacía, define también `SEED_ENCARGADO_PASSWORD`, `SEED_ESTUDIANTE1_PASSWORD` y `SEED_ESTUDIANTE2_PASSWORD` con las claves iniciales elegidas para las tres cuentas. Git y el contexto de compilación excluyen este archivo.

SQL Server Developer se ejecuta en una red interna. El servicio de inicialización aplica las migraciones y configura el usuario SQL de lectura y escritura de la aplicación. El servicio web solo publica su puerto en localhost. Puedes cambiarlo con `LABTRACK_PORT` en `.env`. Para revisar el estado usa `docker compose ps -a`; el contenedor de inicialización termina con código 0 al completar su trabajo.

## Ejecución local

```powershell
dotnet build LabTrack.sln --configuration Release
dotnet run --project LabTrack
```

Abre `http://localhost:5132`. El perfil de desarrollo se limita a localhost. Para detener el servidor, usa `Ctrl+C`. Si el puerto está ocupado, puedes ejecutar `dotnet run --project LabTrack --no-launch-profile --urls http://localhost:5133` y abrir ese puerto.

El estudiante accede a su panel, catálogo y reservas. El encargado dispone de solicitudes y devoluciones. Las operaciones que modifican datos usan POST con protección antifalsificación. Los errores inesperados muestran un mensaje genérico y una referencia, sin detalles de SQL ni credenciales.

## Reglas de horarios y concurrencia

Todos los horarios se interpretan y guardan en hora de Lima (UTC−05:00), independientemente de la zona horaria del equipo. Una reserva tiene inicio futuro y fin posterior, en el mismo día.

La condición de cruce es `inicioNuevo < finExistente && finNuevo > inicioExistente`. Los horarios contiguos son válidos. Un equipo no puede tener aprobaciones superpuestas; cada estudiante solo puede mantener una solicitud pendiente o aprobada por intervalo. Las solicitudes pendientes de distintos estudiantes para un mismo equipo pueden coexistir hasta la decisión del encargado.

La solicitud y la aprobación comprueban los conflictos dentro de una transacción. Todas las escrituras de reservas adquieren el mismo bloqueo exclusivo de aplicación en SQL Server mediante `sp_getapplock`, liberado al finalizar la transacción. Así, dos procesos del servidor tampoco pueden aprobar simultáneamente horarios incompatibles. La columna `rowversion` añade detección de actualizaciones concurrentes. Este diseño serializa escrituras breves, apropiado para un laboratorio; el catálogo sigue realizando consultas sin bloqueo de aplicación.

El catálogo muestra Prestado desde el inicio de una reserva aprobada hasta registrar su devolución, incluso si está vencida. Las reservas futuras ocupan su intervalo, aunque el equipo figure disponible ahora. El formulario muestra hasta diez próximos horarios aprobados y verifica el intervalo completo al enviar. El encargado no puede aprobar un horario cuyo inicio ya pasó.

Solo se devuelven reservas aprobadas cuyo horario ya comenzó. Una devolución solo puede registrarse una vez. Las rechazadas y devueltas dejan de bloquear intervalos. Si la devolución ocurre después del fin, el estudiante queda Penalizado; esta condición no impone multas, bloqueos adicionales ni una duración de sanción. Una devolución puntual posterior no borra una penalización previa.

## SonarQube Cloud y GitHub Actions

GitHub compila la solución y analiza el proyecto Edwardscg_LabTrack de la organización edwardscg al actualizar main, abrir o actualizar una solicitud de integración, o ejecutar el workflow manualmente. El runner Windows de GitHub instala .NET 10 y Java 21. Configura el secreto SONAR_TOKEN con un token de SonarQube Cloud con permiso de análisis en Settings > Secrets and variables > Actions.

## Mantenimiento del repositorio

`main` integra las ramas `feature/foundation-database`, `feature/authentication`, `feature/equipment-catalog`, `feature/reservations`, `feature/admin-management` y `feature/returns-penalties`. Los cambios se organizan con commits convencionales.

Antes de cada commit o push revisa `git status` y `git diff --cached --name-only`. Agrega archivos por ruta explícita. Si un archivo ajeno al producto ya tiene seguimiento, utiliza `git rm --cached -- ruta` para conservar la copia local. Las exclusiones generales están en `.gitignore`; las propias del entorno de trabajo permanecen en `.git/info/exclude`.

El repositorio contiene código, configuración, recursos indispensables, migraciones y este README. Los documentos académicos y el material auxiliar se mantienen fuera de Git. La validación de desarrollo consiste en compilación y arranque local; no implica una certificación de calidad ni sustituye la evaluación posterior.
