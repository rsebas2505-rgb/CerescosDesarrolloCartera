/* ================================================================
   CERESCOS - SISTEMA DE SOLICITUDES DE CARTERA
   ESTRUCTURA DE BASE DE DATOS
   ================================================================

   Motor      : SQL Server (probado en LocalDB / MSSQLLocalDB)
   Base       : CerescosDB
   Generado   : 19/09/2026

   Este script recrea desde cero las tablas del proyecto con sus
   llaves primarias, llaves foraneas, valores por defecto e indices.

   ================================================================
   MODELO RELACIONAL
   ================================================================

                        +---------------------+
                        |      Usuarios       |
                        +---------------------+
                        | PK  Id              |
                        |     Nombre          |
                        |     Apellido        |
                        |     Correo      (U) |
                        |     UsuarioNombre   |
                        |     PasswordHash    |
                        |     Rol             |
                        |     Activo          |
                        +---------------------+
                           ^       ^       ^
                           |       |       |
              creador (1:N)|       |       |cartera (1:N)
                           |       |jefe (1:N)
                           |       |       |
                        +---------------------+
                        |     Solicitudes     |
                        +---------------------+
                        | PK  Id              |
                        | FK  UsuarioCreadorId|  --> quien la crea
                        | FK  JefeUsuarioId   |  --> quien la aprueba (1er nivel)
                        | FK  CarteraUsuarioId|  --> quien la aprueba (2do nivel)
                        |     Canal           |
                        |     NombreCliente   |
                        |     EstadoJefe      |
                        |     EstadoCartera   |
                        |     EstadoGeneral   |
                        +---------------------+

                        +---------------------+
                        |      Registros      |   (sin relaciones)
                        +---------------------+
                        | PK  Id              |
                        |     Nombre / Cedula |
                        |     Correo / Estado |
                        +---------------------+

   Un usuario participa en una solicitud con tres papeles distintos:
   como creador, como jefe que aprueba y como persona de cartera.
   Por eso Solicitudes apunta tres veces a Usuarios.

   ================================================================
   FLUJO DE ESTADOS
   ================================================================

   EstadoJefe      : Pendiente -> Aprobado | Rechazado
   EstadoCartera   : Pendiente -> Aprobado | Rechazado
   EstadoGeneral   : Pendiente
                     -> En revision de Cartera  (el jefe aprobo)
                     -> Aprobado                (cartera aprobo)
                     -> Rechazado               (rechazo el jefe o cartera)

   ================================================================ */


/* ----------------------------------------------------------------
   CREAR LA BASE DE DATOS
   ----------------------------------------------------------------
   Descomentar solo si la base todavia no existe.
---------------------------------------------------------------- */

-- IF DB_ID('CerescosDB') IS NULL
--     CREATE DATABASE CerescosDB;
-- GO

USE CerescosDB;
GO


/* ----------------------------------------------------------------
   LIMPIEZA
   ----------------------------------------------------------------
   Se eliminan en orden inverso a las dependencias:
   primero la tabla hija (Solicitudes) y despues la padre (Usuarios).

   CUIDADO: esto borra los datos existentes.
---------------------------------------------------------------- */

-- DROP TABLE IF EXISTS dbo.Solicitudes;
-- DROP TABLE IF EXISTS dbo.Registros;
-- DROP TABLE IF EXISTS dbo.Usuarios;
-- GO


/* ================================================================
   TABLA: Usuarios
   ================================================================

   Personas que ingresan al sistema.

   El rol define que puede hacer cada una:

     Creador             -> registra solicitudes
     Jefe de Depositos   -> aprueba el canal Depositos
     Jefe de Cadenas     -> aprueba el canal Cadenas
     Cartera             -> aprobacion final
     Persona de cartera  -> equivalente a Cartera

   La contrasena NUNCA se guarda en texto plano: en PasswordHash
   se almacena el hash generado por ASP.NET Identity.

   ================================================================ */

IF OBJECT_ID('dbo.Usuarios', 'U') IS NULL
BEGIN

    CREATE TABLE dbo.Usuarios
    (
        Id              INT             IDENTITY(1,1)   NOT NULL,

        Nombre          NVARCHAR(100)                   NOT NULL,
        Apellido        NVARCHAR(100)                       NULL,
        Cedula          NVARCHAR(100)                       NULL,

        Correo          NVARCHAR(150)                   NOT NULL,
        UsuarioNombre   NVARCHAR(100)                       NULL,
        PasswordHash    NVARCHAR(500)                   NOT NULL,

        Rol             NVARCHAR(50)                    NOT NULL
            CONSTRAINT DF_Usuarios_Rol DEFAULT ('Creador'),

        Activo          BIT                             NOT NULL
            CONSTRAINT DF_Usuarios_Activo DEFAULT (1),

        FechaCreacion   DATETIME2                       NOT NULL
            CONSTRAINT DF_Usuarios_FechaCreacion DEFAULT (SYSDATETIME()),

        FechaRegistro   DATETIME2                           NULL,

        CONSTRAINT PK_Usuarios
            PRIMARY KEY CLUSTERED (Id),

        -- No puede haber dos cuentas con el mismo correo.
        CONSTRAINT UQ_Usuarios_Correo
            UNIQUE (Correo)
    );

END
GO


/* ================================================================
   TABLA: Solicitudes
   ================================================================

   Solicitud de cartera y su recorrido de aprobacion.

   Guarda tres referencias a Usuarios:

     UsuarioCreadorId  quien la registra          (obligatorio)
     JefeUsuarioId     jefe asignado segun canal  (opcional)
     CarteraUsuarioId  quien aprueba al final     (opcional)

   Los dos ultimos son opcionales porque se llenan a medida que
   la solicitud avanza en el flujo.

   ================================================================ */

IF OBJECT_ID('dbo.Solicitudes', 'U') IS NULL
BEGIN

    CREATE TABLE dbo.Solicitudes
    (
        Id                      INT             IDENTITY(1,1)   NOT NULL,

        -- ----------------------------------------------------
        -- DATOS DE LA SOLICITUD
        -- ----------------------------------------------------

        UsuarioCreadorId        INT                             NOT NULL,

        Canal                   NVARCHAR(50)                    NOT NULL,
        NombreCliente           NVARCHAR(150)                   NOT NULL,
        FechaSolicitud          DATE                            NOT NULL,
        TipoSolicitud           NVARCHAR(500)                   NOT NULL,
        DetalleSolicitud        NVARCHAR(MAX)                   NOT NULL,

        -- ----------------------------------------------------
        -- PRIMER NIVEL: APROBACION DEL JEFE
        -- ----------------------------------------------------

        EstadoJefe              NVARCHAR(30)                    NOT NULL
            CONSTRAINT DF_Solicitudes_EstadoJefe DEFAULT ('Pendiente'),

        JefeUsuarioId           INT                                 NULL,
        FechaAprobacionJefe     DATETIME2                           NULL,
        ObservacionJefe         NVARCHAR(MAX)                       NULL,

        -- ----------------------------------------------------
        -- SEGUNDO NIVEL: APROBACION DE CARTERA
        -- ----------------------------------------------------

        EstadoCartera           NVARCHAR(30)                    NOT NULL
            CONSTRAINT DF_Solicitudes_EstadoCartera DEFAULT ('Pendiente'),

        CarteraUsuarioId        INT                                 NULL,
        FechaAprobacionCartera  DATETIME2                           NULL,
        ObservacionCartera      NVARCHAR(MAX)                       NULL,

        -- ----------------------------------------------------
        -- RESULTADO
        -- ----------------------------------------------------

        EstadoGeneral           NVARCHAR(50)                    NOT NULL
            CONSTRAINT DF_Solicitudes_EstadoGeneral DEFAULT ('Pendiente'),

        FechaCreacion           DATETIME2                       NOT NULL
            CONSTRAINT DF_Solicitudes_FechaCreacion DEFAULT (SYSDATETIME()),

        -- ----------------------------------------------------
        -- DOCUMENTO ADJUNTO (OPCIONAL)
        -- ----------------------------------------------------
        --
        -- El archivo NO se guarda en la base: se guarda en la
        -- carpeta "Adjuntos" del servidor, fuera de wwwroot.
        -- Aqui solo queda la referencia.
        --
        -- ArchivoNombre     nombre original que subio el usuario
        -- ArchivoAlmacenado nombre con el que quedo en disco
        --
        -- ----------------------------------------------------

        ArchivoNombre           NVARCHAR(255)                       NULL,
        ArchivoAlmacenado       NVARCHAR(100)                       NULL,
        ArchivoTipo             NVARCHAR(150)                       NULL,
        ArchivoTamano           BIGINT                              NULL,
        ArchivoFecha            DATETIME2                           NULL,

        CONSTRAINT PK_Solicitudes
            PRIMARY KEY CLUSTERED (Id),

        -- ----------------------------------------------------
        -- RELACIONES CON Usuarios
        -- ----------------------------------------------------
        --
        -- Sin ON DELETE CASCADE a proposito: una solicitud es un
        -- documento historico y no debe desaparecer porque se
        -- elimine un usuario. Para dar de baja a alguien se usa
        -- Usuarios.Activo = 0.
        --
        -- ----------------------------------------------------

        CONSTRAINT FK_Solicitudes_Creador
            FOREIGN KEY (UsuarioCreadorId)
            REFERENCES dbo.Usuarios (Id),

        CONSTRAINT FK_Solicitudes_Jefe
            FOREIGN KEY (JefeUsuarioId)
            REFERENCES dbo.Usuarios (Id),

        CONSTRAINT FK_Solicitudes_Cartera
            FOREIGN KEY (CarteraUsuarioId)
            REFERENCES dbo.Usuarios (Id)
    );

END
GO


/* ================================================================
   TABLA: Registros
   ================================================================

   Tabla independiente: no tiene llaves foraneas y ninguna pantalla
   del sitio la consulta. La escribe unicamente el endpoint
   POST /api/Registros.

   La columna UsuarioId existe pero NO es llave foranea.

   ================================================================ */

IF OBJECT_ID('dbo.Registros', 'U') IS NULL
BEGIN

    CREATE TABLE dbo.Registros
    (
        Id                      INT             IDENTITY(1,1)   NOT NULL,

        Nombre                  NVARCHAR(100)                   NOT NULL,
        Cedula                  NVARCHAR(30)                    NOT NULL,
        Correo                  NVARCHAR(150)                   NOT NULL,

        Estado                  NVARCHAR(30)                    NOT NULL
            CONSTRAINT DF_Registros_Estado DEFAULT (N'Pendiente'),

        FechaSolicitud          DATETIME2                       NOT NULL
            CONSTRAINT DF_Registros_FechaSolicitud DEFAULT (SYSDATETIME()),

        FechaAprobacionFinal    DATETIME2                           NULL,
        ComentarioAprobacion    NVARCHAR(500)                       NULL,

        UsuarioId               INT                                 NULL,
        Canal                   NVARCHAR(30)                        NULL,
        NombreCliente           NVARCHAR(200)                       NULL,
        TipoSolicitud           NVARCHAR(500)                       NULL,
        DetalleSolicitud        NVARCHAR(2000)                      NULL,

        CONSTRAINT PK_Registros
            PRIMARY KEY CLUSTERED (Id)
    );

END
GO


/* ================================================================
   INDICES DE APOYO
   ================================================================

   Aceleran las consultas que hace la aplicacion. No cambian
   ningun resultado, solo el tiempo de respuesta.

   ================================================================ */

-- Login: WHERE UsuarioNombre = @UsuarioNombre
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Usuarios_UsuarioNombre')
    CREATE INDEX IX_Usuarios_UsuarioNombre
        ON dbo.Usuarios (UsuarioNombre);
GO

-- Asignacion de jefe: WHERE Rol = @Rol AND Activo = 1
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Usuarios_Rol_Activo')
    CREATE INDEX IX_Usuarios_Rol_Activo
        ON dbo.Usuarios (Rol, Activo);
GO

-- Bandeja del creador
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Solicitudes_Creador')
    CREATE INDEX IX_Solicitudes_Creador
        ON dbo.Solicitudes (UsuarioCreadorId);
GO

-- Bandeja del jefe: WHERE JefeUsuarioId = @Id AND EstadoJefe = 'Pendiente'
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Solicitudes_Jefe_Estado')
    CREATE INDEX IX_Solicitudes_Jefe_Estado
        ON dbo.Solicitudes (JefeUsuarioId, EstadoJefe);
GO

-- Bandeja de cartera: WHERE EstadoJefe IN ('Aprobado','Rechazado')
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Solicitudes_EstadoJefe')
    CREATE INDEX IX_Solicitudes_EstadoJefe
        ON dbo.Solicitudes (EstadoJefe);
GO


/* ================================================================
   ACTUALIZACION PARA BASES QUE YA EXISTEN
   ================================================================

   Si la base se creo antes de la funcionalidad de documentos
   adjuntos, este bloque agrega las columnas que faltan.
   Es seguro ejecutarlo varias veces.

   ================================================================ */

IF COL_LENGTH('dbo.Solicitudes', 'ArchivoNombre') IS NULL
BEGIN

    ALTER TABLE dbo.Solicitudes ADD
        ArchivoNombre     NVARCHAR(255) NULL,
        ArchivoAlmacenado NVARCHAR(100) NULL,
        ArchivoTipo       NVARCHAR(150) NULL,
        ArchivoTamano     BIGINT        NULL,
        ArchivoFecha      DATETIME2     NULL;

END
GO


/* ================================================================
   MEJORAS SUGERIDAS (NO APLICADAS)
   ================================================================

   Quedan comentadas porque cambian el comportamiento actual y
   pueden fallar si los datos existentes no las cumplen.
   Revisar antes de ejecutar.

   ---------------------------------------------------------------- */

-- 1) El nombre de usuario deberia ser unico.
--    Hoy solo se valida desde C#, no desde la base de datos.
--    Verificar antes:
--       SELECT UsuarioNombre, COUNT(*) FROM dbo.Usuarios
--       GROUP BY UsuarioNombre HAVING COUNT(*) > 1;
--
-- ALTER TABLE dbo.Usuarios
--     ADD CONSTRAINT UQ_Usuarios_UsuarioNombre UNIQUE (UsuarioNombre);


-- 2) Restringir los estados a los valores validos.
--
-- ALTER TABLE dbo.Solicitudes
--     ADD CONSTRAINT CK_Solicitudes_EstadoJefe
--         CHECK (EstadoJefe IN ('Pendiente', 'Aprobado', 'Rechazado'));
--
-- ALTER TABLE dbo.Solicitudes
--     ADD CONSTRAINT CK_Solicitudes_EstadoCartera
--         CHECK (EstadoCartera IN ('Pendiente', 'Aprobado', 'Rechazado'));
--
-- ALTER TABLE dbo.Solicitudes
--     ADD CONSTRAINT CK_Solicitudes_Canal
--         CHECK (Canal IN ('Depositos', 'Cadenas'));


-- 3) Relacionar Registros.UsuarioId con Usuarios.
--
-- ALTER TABLE dbo.Registros
--     ADD CONSTRAINT FK_Registros_Usuario
--         FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuarios (Id);


/* ================================================================
   CONSULTAS DE VERIFICACION
   ================================================================ */

-- Tablas creadas
SELECT
    t.name                                  AS Tabla,
    (SELECT COUNT(*) FROM sys.columns c
      WHERE c.object_id = t.object_id)      AS Columnas
FROM sys.tables t
ORDER BY t.name;
GO

-- Relaciones existentes
SELECT
    fk.name                                 AS Relacion,
    OBJECT_NAME(fk.parent_object_id)        AS TablaHija,
    cp.name                                 AS ColumnaHija,
    OBJECT_NAME(fk.referenced_object_id)    AS TablaPadre,
    cr.name                                 AS ColumnaPadre
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc
    ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns cp
    ON cp.object_id = fk.parent_object_id
   AND cp.column_id = fkc.parent_column_id
JOIN sys.columns cr
    ON cr.object_id = fk.referenced_object_id
   AND cr.column_id = fkc.referenced_column_id
ORDER BY TablaHija, Relacion;
GO
