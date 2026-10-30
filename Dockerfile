FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore PRN232.Api/PRN232.Api.csproj
RUN dotnet publish PRN232.Api/PRN232.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV Urls=http://0.0.0.0:10000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PRN232.Api.dll"]
