FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
 
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["RecipeApp.csproj", "."]
RUN dotnet restore "RecipeApp.csproj"
COPY . .
RUN dotnet build "RecipeApp.csproj" -c Release -o /app/build
 
FROM build AS publish
RUN dotnet publish "RecipeApp.csproj" -c Release -o /app/publish
 
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:$PORT dotnet RecipeApp.dll"]
