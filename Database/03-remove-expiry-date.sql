USE RecipeAppDb;
GO

IF COL_LENGTH(N'dbo.InventoryItems', N'ExpiryDate') IS NOT NULL
BEGIN
    ALTER TABLE dbo.InventoryItems DROP COLUMN ExpiryDate;
END;
GO
