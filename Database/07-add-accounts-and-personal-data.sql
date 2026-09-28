USE RecipeAppDb;
GO

IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppUsers
    (
        Id NVARCHAR(32) NOT NULL CONSTRAINT PK_AppUsers PRIMARY KEY,
        Email NVARCHAR(254) NOT NULL,
        NormalizedEmail NVARCHAR(254) NOT NULL,
        PasswordHash NVARCHAR(500) NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_AppUsers_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_AppUsers_NormalizedEmail UNIQUE (NormalizedEmail)
    );
END;
GO

IF COL_LENGTH(N'dbo.InventoryItems', N'UserId') IS NULL
    ALTER TABLE dbo.InventoryItems ADD UserId NVARCHAR(32) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_InventoryItems_AppUsers')
    ALTER TABLE dbo.InventoryItems ADD CONSTRAINT FK_InventoryItems_AppUsers
        FOREIGN KEY (UserId) REFERENCES dbo.AppUsers(Id) ON DELETE CASCADE;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_InventoryItems_User_Product_Unit' AND object_id = OBJECT_ID(N'dbo.InventoryItems'))
    CREATE UNIQUE INDEX UX_InventoryItems_User_Product_Unit
        ON dbo.InventoryItems(UserId, ProductId, Unit);
GO

IF OBJECT_ID(N'dbo.RecipeFavorites', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecipeFavorites
    (
        UserId NVARCHAR(32) NOT NULL,
        RecipeId INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_RecipeFavorites_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_RecipeFavorites PRIMARY KEY (UserId, RecipeId),
        CONSTRAINT FK_RecipeFavorites_AppUsers FOREIGN KEY (UserId) REFERENCES dbo.AppUsers(Id) ON DELETE CASCADE,
        CONSTRAINT FK_RecipeFavorites_Recipes FOREIGN KEY (RecipeId) REFERENCES dbo.Recipes(RecipeId) ON DELETE CASCADE
    );
END;
GO
