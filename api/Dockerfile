# Build with the SDK, run on the ASP.NET runtime as a non-root user.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DeskBook.Api/DeskBook.Api.csproj DeskBook.Api/
RUN dotnet restore DeskBook.Api/DeskBook.Api.csproj
COPY DeskBook.Api/ DeskBook.Api/
RUN dotnet publish DeskBook.Api/DeskBook.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
# Time zone data (office hours are checked in Africa/Casablanca time) and the
# Kerberos library Npgsql loads at startup.
RUN apk add --no-cache tzdata krb5-libs
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=5 \
  CMD wget -qO- http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "DeskBook.Api.dll"]
