FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Directory.Build.props and .editorconfig carry the analyzer and audit
# settings. Without them MSBuild finds nothing walking up from the project,
# and the image would be built with the gates silently switched off.
COPY Directory.Build.props .editorconfig ./
COPY EcoMyceliumTracker/EcoMyceliumTracker.csproj EcoMyceliumTracker/
COPY EcoMyceliumTracker/packages.lock.json EcoMyceliumTracker/
RUN dotnet restore EcoMyceliumTracker/EcoMyceliumTracker.csproj --locked-mode

COPY EcoMyceliumTracker/ EcoMyceliumTracker/
RUN dotnet publish EcoMyceliumTracker/EcoMyceliumTracker.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app

ENTRYPOINT ["dotnet", "EcoMyceliumTracker.dll"]
