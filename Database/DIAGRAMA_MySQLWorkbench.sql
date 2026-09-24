/* ================================================================
   CERESCOS - ESTRUCTURA PARA DIBUJAR EL DIAGRAMA EER
   ================================================================

   >>> ATENCION: ESTE ARCHIVO NO ES LA BASE DE DATOS REAL <<<

   La base de datos del proyecto es SQL SERVER y su script
   oficial es:

       Database\CerescosDB.sql

   Este archivo es una TRADUCCION al dialecto MySQL, hecha con
   un unico proposito: que MySQL Workbench pueda leerlo y
   dibujar el diagrama de relaciones (EER).

   NO lo ejecutes contra la base de datos del proyecto.
   NO reemplaza a CerescosDB.sql.

   ================================================================
   COMO GENERAR EL DIAGRAMA
   ================================================================

   1. Abre MySQL Workbench.

   2. Menu:  File  ->  Import  ->  Reverse Engineer MySQL Create Script

   3. En "Input File" selecciona este archivo y marca la casilla
      "Place imported objects on a diagram".

   4. Execute  ->  Next  ->  Finish

   Se abre el diagrama con las tres tablas y las lineas de
   relacion. Puedes arrastrarlas para acomodarlas, y con
   Arrange -> Autolayout las ordena solas.

   Nota: este paso solo construye un modelo visual dentro de
   Workbench. No ejecuta nada ni se conecta a ningun servidor.

   ================================================================
   EQUIVALENCIA DE TIPOS
   ================================================================

   SQL Server (real)        MySQL (este archivo)
   ----------------------   ----------------------
   INT IDENTITY(1,1)        INT AUTO_INCREMENT
   NVARCHAR(n)              VARCHAR(n)
   NVARCHAR(MAX)            TEXT
   DATETIME2                DATETIME
   BIT                      TINYINT(1)
   SYSDATETIME()            CURRENT_TIMESTAMP

   ================================================================ */


CREATE SCHEMA IF NOT EXISTS `CerescosDB` DEFAULT CHARACTER SET utf8mb4;
USE `CerescosDB`;


/* ================================================================
   TABLA: Usuarios
   ================================================================
   Personas que ingresan al sistema. El rol define que puede
   hacer cada una: Creador, Jefe de Depositos, Jefe de Cadenas
   o Cartera.
   ================================================================ */

CREATE TABLE IF NOT EXISTS `Usuarios`
(
    `Id`                INT             NOT NULL AUTO_INCREMENT,

    `Nombre`            VARCHAR(100)    NOT NULL,
    `Apellido`          VARCHAR(100)        NULL,
    `Cedula`            VARCHAR(100)        NULL,

    `Correo`            VARCHAR(150)    NOT NULL,
    `UsuarioNombre`     VARCHAR(100)        NULL,
    `PasswordHash`      VARCHAR(500)    NOT NULL,

    `Rol`               VARCHAR(50)     NOT NULL DEFAULT 'Creador',
    `Activo`            TINYINT(1)      NOT NULL DEFAULT 1,

    `FechaCreacion`     DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `FechaRegistro`     DATETIME            NULL,

    PRIMARY KEY (`Id`),

    UNIQUE INDEX `UQ_Usuarios_Correo` (`Correo`),

    INDEX `IX_Usuarios_UsuarioNombre` (`UsuarioNombre`),
    INDEX `IX_Usuarios_Rol_Activo` (`Rol`, `Activo`)
)
ENGINE = InnoDB;


/* ================================================================
   TABLA: Solicitudes
   ================================================================
   Solicitud de cartera y su recorrido de aprobacion.

   Apunta TRES VECES a Usuarios, porque una misma persona
   participa con tres papeles distintos:

     UsuarioCreadorId  quien la registra          (obligatorio)
     JefeUsuarioId     jefe que aprueba           (opcional)
     CarteraUsuarioId  quien aprueba al final     (opcional)

   Los dos ultimos son opcionales porque se llenan a medida
   que la solicitud avanza en el flujo.
   ================================================================ */

CREATE TABLE IF NOT EXISTS `Solicitudes`
(
    `Id`                        INT             NOT NULL AUTO_INCREMENT,

    `UsuarioCreadorId`          INT             NOT NULL,

    `Canal`                     VARCHAR(50)     NOT NULL,
    `NombreCliente`             VARCHAR(150)    NOT NULL,
    `FechaSolicitud`            DATE            NOT NULL,
    `TipoSolicitud`             VARCHAR(500)    NOT NULL,
    `DetalleSolicitud`          TEXT            NOT NULL,

    `EstadoJefe`                VARCHAR(30)     NOT NULL DEFAULT 'Pendiente',
    `JefeUsuarioId`             INT                 NULL,
    `FechaAprobacionJefe`       DATETIME            NULL,
    `ObservacionJefe`           TEXT                NULL,

    `EstadoCartera`             VARCHAR(30)     NOT NULL DEFAULT 'Pendiente',
    `CarteraUsuarioId`          INT                 NULL,
    `FechaAprobacionCartera`    DATETIME            NULL,
    `ObservacionCartera`        TEXT                NULL,

    `EstadoGeneral`             VARCHAR(50)     NOT NULL DEFAULT 'Pendiente',
    `FechaCreacion`             DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,

    /* Documento adjunto: el archivo vive en la carpeta
       "Adjuntos" del servidor, aqui solo la referencia. */

    `ArchivoNombre`             VARCHAR(255)        NULL,
    `ArchivoAlmacenado`         VARCHAR(100)        NULL,
    `ArchivoTipo`               VARCHAR(150)        NULL,
    `ArchivoTamano`             BIGINT              NULL,
    `ArchivoFecha`              DATETIME            NULL,

    PRIMARY KEY (`Id`),

    INDEX `IX_Solicitudes_Creador` (`UsuarioCreadorId`),
    INDEX `IX_Solicitudes_Jefe_Estado` (`JefeUsuarioId`, `EstadoJefe`),
    INDEX `IX_Solicitudes_Cartera` (`CarteraUsuarioId`),
    INDEX `IX_Solicitudes_EstadoJefe` (`EstadoJefe`),

    /* --------------------------------------------------------
       LAS TRES RELACIONES CON Usuarios
       --------------------------------------------------------
       Son las lineas que Workbench dibuja en el diagrama.
       Sin ON DELETE CASCADE: una solicitud es un documento
       historico y no debe borrarse al eliminar un usuario.
       -------------------------------------------------------- */

    CONSTRAINT `FK_Solicitudes_Creador`
        FOREIGN KEY (`UsuarioCreadorId`)
        REFERENCES `Usuarios` (`Id`),

    CONSTRAINT `FK_Solicitudes_Jefe`
        FOREIGN KEY (`JefeUsuarioId`)
        REFERENCES `Usuarios` (`Id`),

    CONSTRAINT `FK_Solicitudes_Cartera`
        FOREIGN KEY (`CarteraUsuarioId`)
        REFERENCES `Usuarios` (`Id`)
)
ENGINE = InnoDB;


/* ================================================================
   TABLA: Registros
   ================================================================
   Tabla independiente: no tiene llaves foraneas y ninguna
   pantalla del sitio la consulta. En el diagrama aparecera
   suelta, sin lineas.

   La columna UsuarioId existe pero NO es llave foranea.
   ================================================================ */

CREATE TABLE IF NOT EXISTS `Registros`
(
    `Id`                    INT             NOT NULL AUTO_INCREMENT,

    `Nombre`                VARCHAR(100)    NOT NULL,
    `Cedula`                VARCHAR(30)     NOT NULL,
    `Correo`                VARCHAR(150)    NOT NULL,

    `Estado`                VARCHAR(30)     NOT NULL DEFAULT 'Pendiente',
    `FechaSolicitud`        DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,

    `FechaAprobacionFinal`  DATETIME            NULL,
    `ComentarioAprobacion`  VARCHAR(500)        NULL,

    `UsuarioId`             INT                 NULL,
    `Canal`                 VARCHAR(30)         NULL,
    `NombreCliente`         VARCHAR(200)        NULL,
    `TipoSolicitud`         VARCHAR(500)        NULL,
    `DetalleSolicitud`      VARCHAR(2000)       NULL,

    PRIMARY KEY (`Id`)
)
ENGINE = InnoDB;
