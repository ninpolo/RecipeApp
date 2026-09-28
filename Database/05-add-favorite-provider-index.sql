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

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Recipes_SourceProvider_ProviderRecipeId'
      AND object_id = OBJECT_ID(N'dbo.Recipes')
)
BEGIN
    CREATE UNIQUE INDEX UX_Recipes_SourceProvider_ProviderRecipeId
        ON dbo.Recipes(SourceProvider, ProviderRecipeId)
        WHERE SourceProvider IS NOT NULL AND ProviderRecipeId IS NOT NULL;
END;
GO
