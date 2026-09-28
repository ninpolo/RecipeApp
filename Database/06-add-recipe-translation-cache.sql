USE RecipeAppDb;
GO

SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO

IF OBJECT_ID(N'dbo.RecipeTranslationCache', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecipeTranslationCache
    (
        RecipeTranslationCacheId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_RecipeTranslationCache PRIMARY KEY,
        ProviderRecipeId NVARCHAR(150) NOT NULL,
        Language NVARCHAR(10) NOT NULL,
        SourceHash NVARCHAR(64) NOT NULL,
        Title NVARCHAR(500) NOT NULL,
        IngredientNamesJson NVARCHAR(MAX) NOT NULL,
        IngredientUnitsJson NVARCHAR(MAX) NOT NULL,
        StepInstructionsJson NVARCHAR(MAX) NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_RecipeTranslationCache_CreatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX UX_RecipeTranslationCache_Provider_Language_Hash
        ON dbo.RecipeTranslationCache(ProviderRecipeId, Language, SourceHash);
END;
GO
