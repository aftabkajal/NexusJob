# syntax=docker/dockerfile:1

# Dev image only: hot-reloads the Host via `dotnet watch` (docker-compose.yml,
# `dev` profile). The full backend/ tree is bind-mounted over this at runtime;
# the project-file-only COPY below keeps the restore layer cacheable across
# rebuilds triggered by source edits — only a .csproj/.props change busts it.
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src

# Central Package Management resolves versions from these two root props files.
COPY Directory.Build.props Directory.Packages.props ./

# Only the project files NexusJob.Host actually depends on transitively.
COPY NexusJob.Host/NexusJob.Host.csproj NexusJob.Host/
COPY NexusJob.Modules.Identity/NexusJob.Modules.Identity.csproj NexusJob.Modules.Identity/
COPY NexusJob.Modules.Identity.Contracts/NexusJob.Modules.Identity.Contracts.csproj NexusJob.Modules.Identity.Contracts/
COPY NexusJob.Modules.JobPostings/NexusJob.Modules.JobPostings.csproj NexusJob.Modules.JobPostings/
COPY NexusJob.Modules.JobPostings.Contracts/NexusJob.Modules.JobPostings.Contracts.csproj NexusJob.Modules.JobPostings.Contracts/
COPY NexusJob.Modules.Applications/NexusJob.Modules.Applications.csproj NexusJob.Modules.Applications/
COPY NexusJob.Modules.Applications.Contracts/NexusJob.Modules.Applications.Contracts.csproj NexusJob.Modules.Applications.Contracts/
RUN dotnet restore NexusJob.Host/NexusJob.Host.csproj

COPY . .

ENV ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS=http://0.0.0.0:2052 \
    DOTNET_USE_POLLING_FILE_WATCHER=true

EXPOSE 2052
ENTRYPOINT ["dotnet", "watch", "run", "--project", "NexusJob.Host/NexusJob.Host.csproj", "--no-launch-profile"]
