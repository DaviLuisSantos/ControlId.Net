# Simulador de equipamento Control iD. Construa da raiz do repositório:
#
#   docker build -f tools/ControlId.Net.Simulator/Dockerfile -t controlid-simulator .
#   docker run --rm -p 8080:8080 controlid-simulator
#
# Configuração por variáveis de ambiente: Simulator__Password, Simulator__Doors, Simulator__SeedSampleData...

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY tools/ControlId.Net.Simulator/ControlId.Net.Simulator.csproj tools/ControlId.Net.Simulator/
RUN dotnet restore tools/ControlId.Net.Simulator/ControlId.Net.Simulator.csproj
COPY tools/ControlId.Net.Simulator/ tools/ControlId.Net.Simulator/
RUN dotnet publish tools/ControlId.Net.Simulator/ControlId.Net.Simulator.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# Chiseled: sem shell nem gerenciador de pacotes, e roda como usuário sem privilégios.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app ./
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT ["dotnet", "ControlId.Net.Simulator.dll"]
