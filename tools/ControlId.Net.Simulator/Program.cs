// Simulador de equipamento Control iD (linha de acesso).
//
//   dotnet run --project tools/ControlId.Net.Simulator -- --urls http://0.0.0.0:8080
//   docker run -p 8080:8080 ghcr.io/daviluissantos/controlid-simulator
//
// Depois: new AccessClient("http://localhost:8080", "admin", "admin"). Veja tools/ControlId.Net.Simulator/README.md.
using ControlId.Simulator;

await SimulatorApp.Build(args).RunAsync();
