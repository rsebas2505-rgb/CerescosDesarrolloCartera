# CerescosDesarrolloCartera

Sistema de solicitudes de cartera de Cerescos.

## Tecnologia

- ASP.NET Core 8 (Web API + paginas estaticas en `wwwroot`)
- SQL Server (LocalDB `MSSQLLocalDB`), base `CerescosDB`
- Autenticacion por cookie

## Estructura

| Carpeta | Contenido |
|---|---|
| `CerescosDesarrolloCartera/Controllers` | AuthController, RegistrosController, SolicitudesController, ControladorBase |
| `CerescosDesarrolloCartera/Models` | Registro, Solicitud, Usuario |
| `CerescosDesarrolloCartera/wwwroot` | login, index, registro, solicitud, jefe, cartera, trazabilidad (HTML/CSS/JS) |
| `CerescosDesarrolloCartera/Adjuntos` | Archivos subidos en ejecucion (no se versionan) |
| `Database` | `CerescosDB.sql` (script de creacion), modelo MySQL Workbench y diagrama PDF |

## Como ejecutarlo

1. Crear la base `CerescosDB` en LocalDB ejecutando `Database/CerescosDB.sql`.
2. Revisar la cadena `ConnectionStrings:CerescosDB` en `appsettings.json`.
3. Abrir `CerescosDesarrolloCartera.sln` en Visual Studio 2022 y ejecutar (perfil `https`). Abre `login.html`.
