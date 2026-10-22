# Build inputs contain source only; actual credentials/configuration are mounted at runtime.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY Backend/ElectronicRegisterAPI/src/ Backend/ElectronicRegisterAPI/src/
RUN dotnet restore Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/ElectronicRegisterAPI.csproj --locked-mode
RUN dotnet publish Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/ElectronicRegisterAPI.csproj --no-restore -c Release -o /out /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out/ ./
USER $APP_UID
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ElectronicRegisterAPI.dll"]
