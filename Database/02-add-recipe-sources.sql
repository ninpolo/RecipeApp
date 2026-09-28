USE RecipeAppDb;
GO

IF COL_LENGTH(N'dbo.Recipes', N'SourceProvider') IS NULL
    ALTER TABLE dbo.Recipes ADD SourceProvider NVARCHAR(50) NULL;
GO

IF COL_LENGTH(N'dbo.Recipes', N'ProviderRecipeId') IS NULL
    ALTER TABLE dbo.Recipes ADD ProviderRecipeId NVARCHAR(150) NULL;
GO

IF COL_LENGTH(N'dbo.Recipes', N'SourceName') IS NULL
    ALTER TABLE dbo.Recipes ADD SourceName NVARCHAR(150) NULL;
GO

IF COL_LENGTH(N'dbo.Recipes', N'SourceUrl') IS NULL
    ALTER TABLE dbo.Recipes ADD SourceUrl NVARCHAR(2048) NULL;
GO

IF COL_LENGTH(N'dbo.Recipes', N'ImageUrl') IS NULL
    ALTER TABLE dbo.Recipes ADD ImageUrl NVARCHAR(2048) NULL;
GO

IF OBJECT_ID(N'dbo.CK_Recipes_Source', N'C') IS NOT NULL
    ALTER TABLE dbo.Recipes DROP CONSTRAINT CK_Recipes_Source;
GO

ALTER TABLE dbo.Recipes WITH CHECK
ADD CONSTRAINT CK_Recipes_Source
CHECK (Source IN (N'Manual', N'External', N'AI'));
GO
