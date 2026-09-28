IF DB_ID(N'RecipeAppDb') IS NULL
BEGIN
    CREATE DATABASE RecipeAppDb;
END;
GO

USE RecipeAppDb;
GO

IF OBJECT_ID(N'dbo.ProductCategories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductCategories
    (
        ProductCategoryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductCategories PRIMARY KEY,
        Name NVARCHAR(60) NOT NULL CONSTRAINT UQ_ProductCategories_Name UNIQUE
    );
END;
GO

IF OBJECT_ID(N'dbo.RecipeCategories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecipeCategories
    (
        RecipeCategoryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RecipeCategories PRIMARY KEY,
        Name NVARCHAR(60) NOT NULL CONSTRAINT UQ_RecipeCategories_Name UNIQUE
    );
END;
GO

IF OBJECT_ID(N'dbo.ProductCatalog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductCatalog
    (
        ProductId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductCatalog PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL CONSTRAINT UQ_ProductCatalog_Name UNIQUE,
        CatalogProvider NVARCHAR(50) NULL,
        CatalogItemId NVARCHAR(100) NULL,
        ProductCategoryId INT NULL,
        CONSTRAINT FK_ProductCatalog_ProductCategories FOREIGN KEY (ProductCategoryId)
            REFERENCES dbo.ProductCategories(ProductCategoryId)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_ProductCatalog_External' AND object_id = OBJECT_ID(N'dbo.ProductCatalog'))
    CREATE UNIQUE INDEX UQ_ProductCatalog_External
        ON dbo.ProductCatalog(CatalogProvider, CatalogItemId)
        WHERE CatalogProvider IS NOT NULL AND CatalogItemId IS NOT NULL;
GO

IF OBJECT_ID(N'dbo.InventoryItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryItems
    (
        InventoryItemId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InventoryItems PRIMARY KEY,
        ProductId INT NOT NULL,
        Quantity DECIMAL(10,2) NOT NULL,
        Unit NVARCHAR(20) NOT NULL,
        AddedAt DATETIME2 NOT NULL CONSTRAINT DF_InventoryItems_AddedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_InventoryItems_Quantity CHECK (Quantity >= 0),
        CONSTRAINT FK_InventoryItems_ProductCatalog FOREIGN KEY (ProductId)
            REFERENCES dbo.ProductCatalog(ProductId)
    );
END;
GO

IF OBJECT_ID(N'dbo.Recipes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Recipes
    (
        RecipeId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Recipes PRIMARY KEY,
        Name NVARCHAR(140) NOT NULL,
        Description NVARCHAR(500) NULL,
        RecipeCategoryId INT NULL,
        PreparationMinutes INT NULL,
        Source NVARCHAR(20) NOT NULL CONSTRAINT DF_Recipes_Source DEFAULT N'Manual',
        SourceProvider NVARCHAR(50) NULL,
        ProviderRecipeId NVARCHAR(150) NULL,
        SourceName NVARCHAR(150) NULL,
        SourceUrl NVARCHAR(2048) NULL,
        ImageUrl NVARCHAR(2048) NULL,
        IsSaved BIT NOT NULL CONSTRAINT DF_Recipes_IsSaved DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Recipes_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_Recipes_PreparationMinutes CHECK (PreparationMinutes IS NULL OR PreparationMinutes > 0),
        CONSTRAINT CK_Recipes_Source CHECK (Source IN (N'Manual', N'External', N'AI')),
        CONSTRAINT FK_Recipes_RecipeCategories FOREIGN KEY (RecipeCategoryId)
            REFERENCES dbo.RecipeCategories(RecipeCategoryId)
    );
END;
GO

IF OBJECT_ID(N'dbo.RecipeIngredients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecipeIngredients
    (
        RecipeIngredientId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RecipeIngredients PRIMARY KEY,
        RecipeId INT NOT NULL,
        ProductId INT NOT NULL,
        Quantity DECIMAL(10,2) NOT NULL,
        Unit NVARCHAR(20) NOT NULL,
        IsOptional BIT NOT NULL CONSTRAINT DF_RecipeIngredients_IsOptional DEFAULT 0,
        CONSTRAINT CK_RecipeIngredients_Quantity CHECK (Quantity > 0),
        CONSTRAINT FK_RecipeIngredients_Recipes FOREIGN KEY (RecipeId)
            REFERENCES dbo.Recipes(RecipeId) ON DELETE CASCADE,
        CONSTRAINT FK_RecipeIngredients_ProductCatalog FOREIGN KEY (ProductId)
            REFERENCES dbo.ProductCatalog(ProductId)
    );
END;
GO

IF OBJECT_ID(N'dbo.RecipeSteps', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecipeSteps
    (
        RecipeStepId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RecipeSteps PRIMARY KEY,
        RecipeId INT NOT NULL,
        StepNumber INT NOT NULL,
        Instruction NVARCHAR(1000) NOT NULL,
        CONSTRAINT UQ_RecipeSteps_Recipe_Step UNIQUE (RecipeId, StepNumber),
        CONSTRAINT CK_RecipeSteps_StepNumber CHECK (StepNumber > 0),
        CONSTRAINT FK_RecipeSteps_Recipes FOREIGN KEY (RecipeId)
            REFERENCES dbo.Recipes(RecipeId) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ProductCategories)
BEGIN
    INSERT INTO dbo.ProductCategories (Name)
    VALUES (N'Плодове'), (N'Зеленчуци'), (N'Млечни'), (N'Месо и риба'),
           (N'Основни продукти'), (N'Подправки'), (N'Други');
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.RecipeCategories)
BEGIN
    INSERT INTO dbo.RecipeCategories (Name)
    VALUES (N'Салати'), (N'Основни'), (N'Супи'), (N'Десерти'), (N'Закуски'), (N'Други');
END;
GO
