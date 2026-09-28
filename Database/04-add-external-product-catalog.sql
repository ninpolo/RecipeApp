USE RecipeAppDb;
GO

IF COL_LENGTH(N'dbo.ProductCatalog', N'CatalogProvider') IS NULL
    ALTER TABLE dbo.ProductCatalog ADD CatalogProvider NVARCHAR(50) NULL;
GO

IF COL_LENGTH(N'dbo.ProductCatalog', N'CatalogItemId') IS NULL
    ALTER TABLE dbo.ProductCatalog ADD CatalogItemId NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_ProductCatalog_External' AND object_id = OBJECT_ID(N'dbo.ProductCatalog'))
    CREATE UNIQUE INDEX UQ_ProductCatalog_External
        ON dbo.ProductCatalog(CatalogProvider, CatalogItemId)
        WHERE CatalogProvider IS NOT NULL AND CatalogItemId IS NOT NULL;
GO
