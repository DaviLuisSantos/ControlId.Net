# Dashboard em tempo real. Construa da raiz do repositório (o projeto referencia src/):
#
#   docker build -f samples/ControlId.Net.Dashboard/Dockerfile -t controlid-dashboard .
#   docker run --rm -p 8081:8080 -e Dashboard__DeviceAddress=http://IP-do-equipamento -e Dashboard__MonitorHostname=IP-desta-maquina \
#     -e Dashboard__MonitorPort=8081 controlid-dashboard
#
# Com o simulador, use o docker-compose: docker compose -f samples/docker-compose.yml up --build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/ src/
COPY samples/ControlId.Net.Dashboard/ControlId.Net.Dashboard.csproj samples/ControlId.Net.Dashboard/
RUN dotnet restore samples/ControlId.Net.Dashboard/ControlId.Net.Dashboard.csproj
COPY samples/ControlId.Net.Dashboard/ samples/ControlId.Net.Dashboard/
RUN dotnet publish samples/ControlId.Net.Dashboard/ControlId.Net.Dashboard.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# Chiseled: sem shell nem gerenciador de pacotes, e roda como usuário sem privilégios.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "ControlId.Net.Dashboard.dll"]
