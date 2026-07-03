CREATE DATABASE IF NOT EXISTS telegram_bot
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE telegram_bot;

CREATE TABLE IF NOT EXISTS Usuarios (
    Id BIGINT NOT NULL AUTO_INCREMENT,
    TelegramUsuarioId BIGINT NOT NULL,
    NombreUsuario VARCHAR(255) NULL,
    NombreCompleto VARCHAR(255) NULL,
    FechaAlta DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    UltimaInteraccion DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (Id),
    UNIQUE KEY UX_Usuarios_TelegramUsuarioId (TelegramUsuarioId)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Publicaciones (
    Id BIGINT NOT NULL AUTO_INCREMENT,
    UsuarioId BIGINT NOT NULL,
    Texto TEXT NULL,
    Estado VARCHAR(30) NOT NULL DEFAULT 'Borrador',
    FechaCreacion DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    FechaFinalizacion DATETIME(6) NULL,
    PRIMARY KEY (Id),
    KEY IX_Publicaciones_UsuarioId (UsuarioId),
    CONSTRAINT FK_Publicaciones_Usuarios FOREIGN KEY (UsuarioId)
        REFERENCES Usuarios (Id) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS ArchivosPublicacion (
    Id BIGINT NOT NULL AUTO_INCREMENT,
    PublicacionId BIGINT NOT NULL,
    Tipo VARCHAR(20) NOT NULL,
    RutaRelativa VARCHAR(1000) NOT NULL,
    NombreOriginal VARCHAR(255) NULL,
    TelegramFileId VARCHAR(255) NULL,
    Orden INT NOT NULL,
    FechaCreacion DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (Id),
    UNIQUE KEY UX_ArchivosPublicacion_PublicacionId_Orden (PublicacionId, Orden),
    CONSTRAINT FK_ArchivosPublicacion_Publicaciones FOREIGN KEY (PublicacionId)
        REFERENCES Publicaciones (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS Programaciones (
    Id BIGINT NOT NULL AUTO_INCREMENT,
    PublicacionId BIGINT NOT NULL,
    Tipo VARCHAR(30) NOT NULL,
    FechaProximaEjecucion DATETIME(6) NOT NULL,
    IntervaloMinutos INT NULL,
    Estado VARCHAR(30) NOT NULL DEFAULT 'Pendiente',
    UltimaEjecucion DATETIME(6) NULL,
    FechaCreacion DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (Id),
    UNIQUE KEY UX_Programaciones_PublicacionId (PublicacionId),
    KEY IX_Programaciones_Estado_Fecha (Estado, FechaProximaEjecucion),
    CONSTRAINT FK_Programaciones_Publicaciones FOREIGN KEY (PublicacionId)
        REFERENCES Publicaciones (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS EnviosPublicacion (
    Id BIGINT NOT NULL AUTO_INCREMENT,
    PublicacionId BIGINT NOT NULL,
    FechaIntento DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    Exitoso BIT NOT NULL,
    MensajeError TEXT NULL,
    TelegramMensajeId INT NULL,
    PRIMARY KEY (Id),
    KEY IX_EnviosPublicacion_PublicacionId (PublicacionId),
    CONSTRAINT FK_EnviosPublicacion_Publicaciones FOREIGN KEY (PublicacionId)
        REFERENCES Publicaciones (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS SesionesConversacion (
    Id BIGINT NOT NULL AUTO_INCREMENT,
    UsuarioId BIGINT NOT NULL,
    PublicacionActualId BIGINT NULL,
    Estado VARCHAR(40) NOT NULL DEFAULT 'Inicio',
    DatosTemporales VARCHAR(1000) NULL,
    FechaActualizacion DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (Id),
    UNIQUE KEY UX_SesionesConversacion_UsuarioId (UsuarioId),
    CONSTRAINT FK_SesionesConversacion_Usuarios FOREIGN KEY (UsuarioId)
        REFERENCES Usuarios (Id) ON DELETE CASCADE,
    CONSTRAINT FK_SesionesConversacion_Publicaciones FOREIGN KEY (PublicacionActualId)
        REFERENCES Publicaciones (Id) ON DELETE SET NULL
) ENGINE=InnoDB;
