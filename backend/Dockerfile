FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

COPY src/ .

WORKDIR /app/SpreadsheetFilterApp.Web

RUN dotnet restore
RUN dotnet publish -c Release -o /app/out
RUN dotnet publish /app/SpreadsheetFilterApp.QuerySandboxHost/SpreadsheetFilterApp.QuerySandboxHost.csproj -c Release -o /app/sandboxhost

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build-env /app/out .
COPY --from=build-env /app/sandboxhost /app/sandboxhost
ENV QueryRuntime__SandboxHostDllPath=/app/sandboxhost/SpreadsheetFilterApp.QuerySandboxHost.dll

ENTRYPOINT ["dotnet", "SpreadsheetFilterApp.Web.dll"]
