-- =====================================================================
-- Bascule d'une base GescomSaas creee avec EnsureCreated() vers les
-- migrations EF Core versionnees.
--
-- A executer UNE SEULE FOIS sur les bases SQL Server existantes,
-- AVANT le premier deploiement contenant la bascule MigrateAsync().
--
-- Ce script :
--   1. Cree la table __EFMigrationsHistory si absente
--   2. Y inscrit la migration InitialCreate comme deja appliquee
--      (le schema existe deja, on dit juste a EF qu'il est a jour)
--
-- Apres ce script, les migrations futures s'appliqueront normalement.
-- =====================================================================

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426234848_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260426234848_InitialCreate', N'9.0.14');
    PRINT 'Migration InitialCreate marquee comme appliquee.';
END
ELSE
BEGIN
    PRINT 'Migration InitialCreate deja presente, aucune action.';
END;
