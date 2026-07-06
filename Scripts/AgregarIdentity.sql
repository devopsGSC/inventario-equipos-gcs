-- Ejecutar DESPUÉS de tener la BD existente
-- Las tablas de Identity se crean con este script

CREATE TABLE [AspNetRoles] (
    [Id]               NVARCHAR(450) NOT NULL PRIMARY KEY,
    [Name]             NVARCHAR(256) NULL,
    [NormalizedName]   NVARCHAR(256) NULL,
    [ConcurrencyStamp] NVARCHAR(MAX) NULL
);

CREATE TABLE [AspNetUsers] (
    [Id]                   NVARCHAR(450) NOT NULL PRIMARY KEY,
    [NombreCompleto]       NVARCHAR(MAX) NOT NULL DEFAULT '',
    [Cargo]                NVARCHAR(MAX) NOT NULL DEFAULT '',
    [RutaFirmaIT]          NVARCHAR(MAX) NULL,
    [Activo]               BIT NOT NULL DEFAULT 1,
    [FechaCreacion]        DATETIME2 NOT NULL DEFAULT GETDATE(),
    [UserName]             NVARCHAR(256) NULL,
    [NormalizedUserName]   NVARCHAR(256) NULL,
    [Email]                NVARCHAR(256) NULL,
    [NormalizedEmail]      NVARCHAR(256) NULL,
    [EmailConfirmed]       BIT NOT NULL DEFAULT 0,
    [PasswordHash]         NVARCHAR(MAX) NULL,
    [SecurityStamp]        NVARCHAR(MAX) NULL,
    [ConcurrencyStamp]     NVARCHAR(MAX) NULL,
    [PhoneNumber]          NVARCHAR(MAX) NULL,
    [PhoneNumberConfirmed] BIT NOT NULL DEFAULT 0,
    [TwoFactorEnabled]     BIT NOT NULL DEFAULT 0,
    [LockoutEnd]           DATETIMEOFFSET NULL,
    [LockoutEnabled]       BIT NOT NULL DEFAULT 1,
    [AccessFailedCount]    INT NOT NULL DEFAULT 0
);

CREATE TABLE [AspNetUserRoles] (
    [UserId] NVARCHAR(450) NOT NULL,
    [RoleId] NVARCHAR(450) NOT NULL,
    PRIMARY KEY ([UserId], [RoleId]),
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE,
    FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles]([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserClaims] (
    [Id]         INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [UserId]     NVARCHAR(450) NOT NULL,
    [ClaimType]  NVARCHAR(MAX) NULL,
    [ClaimValue] NVARCHAR(MAX) NULL,
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetRoleClaims] (
    [Id]         INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [RoleId]     NVARCHAR(450) NOT NULL,
    [ClaimType]  NVARCHAR(MAX) NULL,
    [ClaimValue] NVARCHAR(MAX) NULL,
    FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles]([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider]       NVARCHAR(450) NOT NULL,
    [ProviderKey]         NVARCHAR(450) NOT NULL,
    [ProviderDisplayName] NVARCHAR(MAX) NULL,
    [UserId]              NVARCHAR(450) NOT NULL,
    PRIMARY KEY ([LoginProvider], [ProviderKey]),
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId]        NVARCHAR(450) NOT NULL,
    [LoginProvider] NVARCHAR(450) NOT NULL,
    [Name]          NVARCHAR(450) NOT NULL,
    PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE
);

CREATE UNIQUE INDEX [UserNameIndex]  ON [AspNetUsers]([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
CREATE INDEX       [EmailIndex]      ON [AspNetUsers]([NormalizedEmail]);
CREATE UNIQUE INDEX [RoleNameIndex]  ON [AspNetRoles]([NormalizedName])    WHERE [NormalizedName] IS NOT NULL;
GO
