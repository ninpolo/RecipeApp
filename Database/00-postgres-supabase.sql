-- Postgres schema for RecipeApp (Supabase).
-- Replaces 01-create-database.sql through 07-add-accounts-and-personal-data.sql,
-- which were written in SQL Server T-SQL and do not run on Postgres.
-- Run this once in the Supabase SQL editor against a fresh database.
--
-- Column names are double-quoted to match EF Core's exact PascalCase model names.
-- Unquoted identifiers in Postgres are folded to lowercase, which would make
-- EF's generated queries (which always quote "AppUsers", "Id", etc.) fail to
-- find the tables.
--
-- Use TIMESTAMPTZ so Npgsql maps DateTime correctly (plain TIMESTAMP breaks EF).
-- Translation JSON columns default to [] so title-only cache rows can be saved.

CREATE TABLE IF NOT EXISTS "ProductCategories"
(
    "ProductCategoryId" INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "Name" VARCHAR(60) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS "RecipeCategories"
(
    "RecipeCategoryId" INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "Name" VARCHAR(60) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS "ProductCatalog"
(
    "ProductId" INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "Name" VARCHAR(100) NOT NULL UNIQUE,
    "CatalogProvider" VARCHAR(50) NULL,
    "CatalogItemId" VARCHAR(100) NULL,
    "ProductCategoryId" INT NULL
        REFERENCES "ProductCategories" ("ProductCategoryId")
);

CREATE UNIQUE INDEX IF NOT EXISTS "UQ_ProductCatalog_External"
    ON "ProductCatalog" ("CatalogProvider", "CatalogItemId")
    WHERE "CatalogProvider" IS NOT NULL AND "CatalogItemId" IS NOT NULL;

CREATE TABLE IF NOT EXISTS "AppUsers"
(
    "Id" VARCHAR(32) NOT NULL PRIMARY KEY,
    "Email" VARCHAR(254) NOT NULL,
    "NormalizedEmail" VARCHAR(254) NOT NULL UNIQUE,
    "PasswordHash" VARCHAR(500) NOT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT (timezone('utc', now()))
);

CREATE TABLE IF NOT EXISTS "InventoryItems"
(
    "InventoryItemId" INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "ProductId" INT NOT NULL REFERENCES "ProductCatalog" ("ProductId"),
    "Quantity" DECIMAL(10, 2) NOT NULL CHECK ("Quantity" >= 0),
    "Unit" VARCHAR(20) NOT NULL,
    "AddedAt" TIMESTAMPTZ NOT NULL DEFAULT (timezone('utc', now())),
    "UserId" VARCHAR(32) NULL
        REFERENCES "AppUsers" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_InventoryItems_User_Product_Unit"
    ON "InventoryItems" ("UserId", "ProductId", "Unit");

CREATE TABLE IF NOT EXISTS "Recipes"
(
    "RecipeId" INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "Name" VARCHAR(140) NOT NULL,
    "Description" VARCHAR(500) NULL,
    "RecipeCategoryId" INT NULL REFERENCES "RecipeCategories" ("RecipeCategoryId"),
    "PreparationMinutes" INT NULL CHECK ("PreparationMinutes" IS NULL OR "PreparationMinutes" > 0),
    "Source" VARCHAR(20) NOT NULL DEFAULT 'Manual'
        CHECK ("Source" IN ('Manual', 'External', 'AI')),
    "SourceProvider" VARCHAR(50) NULL,
    "ProviderRecipeId" VARCHAR(150) NULL,
    "SourceName" VARCHAR(150) NULL,
    "SourceUrl" VARCHAR(2048) NULL,
    "ImageUrl" VARCHAR(2048) NULL,
    "IsSaved" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT (timezone('utc', now()))
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_Recipes_SourceProvider_ProviderRecipeId"
    ON "Recipes" ("SourceProvider", "ProviderRecipeId")
    WHERE "SourceProvider" IS NOT NULL AND "ProviderRecipeId" IS NOT NULL;

CREATE TABLE IF NOT EXISTS "RecipeIngredients"
(
    "RecipeIngredientId" INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "RecipeId" INT NOT NULL REFERENCES "Recipes" ("RecipeId") ON DELETE CASCADE,
    "ProductId" INT NOT NULL REFERENCES "ProductCatalog" ("ProductId"),
    "Quantity" DECIMAL(10, 2) NOT NULL CHECK ("Quantity" > 0),
    "Unit" VARCHAR(20) NOT NULL,
    "IsOptional" BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS "RecipeSteps"
(
    "RecipeStepId" INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "RecipeId" INT NOT NULL REFERENCES "Recipes" ("RecipeId") ON DELETE CASCADE,
    "StepNumber" INT NOT NULL CHECK ("StepNumber" > 0),
    "Instruction" VARCHAR(1000) NOT NULL,
    UNIQUE ("RecipeId", "StepNumber")
);

CREATE TABLE IF NOT EXISTS "RecipeTranslationCache"
(
    "RecipeTranslationCacheId" INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "ProviderRecipeId" VARCHAR(150) NOT NULL,
    "Language" VARCHAR(10) NOT NULL,
    "SourceHash" VARCHAR(64) NOT NULL,
    "Title" VARCHAR(500) NOT NULL,
    "IngredientNamesJson" TEXT NOT NULL DEFAULT '[]',
    "IngredientUnitsJson" TEXT NOT NULL DEFAULT '[]',
    "StepInstructionsJson" TEXT NOT NULL DEFAULT '[]',
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT (timezone('utc', now())),
    UNIQUE ("ProviderRecipeId", "Language", "SourceHash")
);

CREATE TABLE IF NOT EXISTS "RecipeFavorites"
(
    "UserId" VARCHAR(32) NOT NULL REFERENCES "AppUsers" ("Id") ON DELETE CASCADE,
    "RecipeId" INT NOT NULL REFERENCES "Recipes" ("RecipeId") ON DELETE CASCADE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT (timezone('utc', now())),
    PRIMARY KEY ("UserId", "RecipeId")
);

INSERT INTO "ProductCategories" ("Name")
SELECT v."Name" FROM (VALUES
    ('Плодове'), ('Зеленчуци'), ('Млечни'), ('Месо и риба'),
    ('Основни продукти'), ('Подправки'), ('Други')
) AS v("Name")
WHERE NOT EXISTS (SELECT 1 FROM "ProductCategories");

INSERT INTO "RecipeCategories" ("Name")
SELECT v."Name" FROM (VALUES
    ('Салати'), ('Основни'), ('Супи'), ('Десерти'), ('Закуски'), ('Други')
) AS v("Name")
WHERE NOT EXISTS (SELECT 1 FROM "RecipeCategories");

-- Confirm tables exist (this SELECT *does* return rows in the Results panel).
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name IN (
    'ProductCategories', 'RecipeCategories', 'ProductCatalog', 'AppUsers',
    'InventoryItems', 'Recipes', 'RecipeIngredients', 'RecipeSteps',
    'RecipeTranslationCache', 'RecipeFavorites'
  )
ORDER BY table_name;
