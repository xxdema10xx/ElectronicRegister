# Build inputs contain source only; actual credentials/configuration are mounted at runtime.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY Backend/ElectronicRegisterAPI/src/ Backend/ElectronicRegisterAPI/src/
COPY migrations/mt1/ migrations/mt1/
COPY migrations/mt3/ migrations/mt3/
RUN dotnet restore Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/ElectronicRegisterAPI.csproj --locked-mode
RUN dotnet publish Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/ElectronicRegisterAPI.csproj --no-restore -c Release -o /out /p:UseAppHost=false
COPY migrations/bootstrap/ /release/migrations/bootstrap/
COPY migrations/identity-account/ /release/migrations/identity-account/
COPY migrations/release/manifest.json /release/migrations/release/manifest.json
COPY migrations/release/additive-contract.json /release/migrations/release/additive-contract.json
FROM build AS health-build
COPY tools/StagingSupport/ tools/StagingSupport/
RUN dotnet restore tools/StagingSupport/StagingSupport.csproj --locked-mode
RUN dotnet publish tools/StagingSupport/StagingSupport.csproj --no-restore -c Release -o /health /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG SOURCE_REVISION=unpublished
LABEL org.opencontainers.image.revision=$SOURCE_REVISION
WORKDIR /app
COPY --from=build /out/ ./
COPY --from=build /release/ /release/
COPY --from=health-build /health/ /health/
USER $APP_UID
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ElectronicRegisterAPI.dll"]
