FROM ://microsoft.com AS base
WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

FROM ://microsoft.com AS build
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
ENTRYPOINT ["dotnet", "RecipeApp.dll"]