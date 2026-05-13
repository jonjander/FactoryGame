FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY nuget.config ./
COPY FactoryGame.sln ./
COPY src/FactoryGame.Domain/FactoryGame.Domain.csproj src/FactoryGame.Domain/
COPY src/FactoryGame.Contracts/FactoryGame.Contracts.csproj src/FactoryGame.Contracts/
COPY src/FactoryGame.Infrastructure/FactoryGame.Infrastructure.csproj src/FactoryGame.Infrastructure/
COPY src/FactoryGame.Api/FactoryGame.Api.csproj src/FactoryGame.Api/
RUN dotnet restore src/FactoryGame.Api/FactoryGame.Api.csproj
COPY src/ src/
RUN dotnet publish src/FactoryGame.Api/FactoryGame.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FactoryGame.Api.dll"]
