Supabase + this C# app
======================

The frontend and API stay as they are. Supabase is only Postgres hosting.
Do not switch the login UI to Supabase Auth unless you rewrite AuthController.

1. In Supabase: SQL Editor -> paste Database/00-postgres-supabase.sql -> Run.
   "Success. 0 rows returned" on CREATE/INSERT is normal. The last SELECT should
   list 10 tables. Table Editor should show ProductCategories with 7 rows.

2. Turn OFF Row Level Security on these tables (Table Editor -> each table),
   or the C# app will see empty results / permission errors. The backend
   already enforces per-user access.

3. Project Settings -> Database -> Connection string -> URI, then Session pooler
   (port 5432) or Direct. Avoid Transaction pooler (6543) with Entity Framework.

   Convert URI:
     postgresql://postgres.[ref]:PASSWORD@aws-0-REGION.pooler.supabase.com:5432/postgres
   to:
     Host=aws-0-REGION.pooler.supabase.com; Port=5432; Database=postgres; Username=postgres.[ref]; Password=PASSWORD; SSL Mode=Require; Trust Server Certificate=true

4. Do not put the password in appsettings.json. From the project folder:

   dotnet user-secrets set "ConnectionStrings:RecipeApp" "Host=...; Password=...; SSL Mode=Require; Trust Server Certificate=true"

5. Also keep Spoonacular:ApiKey in user secrets.

This does not copy rows from the old SQL Server database. Schema + seed only.
To move old pantry data, export from SQL Server and INSERT into these quoted tables.
