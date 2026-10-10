// Uso:
//   dotnet run --project samples/ControlId.Net.Sample -- access http://192.168.0.129 admin admin
//   dotnet run --project samples/ControlId.Net.Sample -- rep https://192.168.0.130 admin admin
using System;
using System.Linq;
using System.Threading.Tasks;
using ControlId;
using ControlId.Access;
using ControlId.Rep;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Uso: <access|rep> <url> [login] [senha]");
            return 1;
        }

        var login = args.Length > 2 ? args[2] : "admin";
        var password = args.Length > 3 ? args[3] : "admin";

        try
        {
            return args[0] switch
            {
                "access" => await RunAccessAsync(args[1], login, password),
                "rep" => await RunRepAsync(args[1], login, password),
                _ => 1,
            };
        }
        catch (ControlIdException ex)
        {
            Console.Error.WriteLine($"Erro: {ex.Message}");
            if (ex.ResponseBody is not null)
            {
                Console.Error.WriteLine(ex.ResponseBody);
            }

            return 2;
        }
    }

    private static async Task<int> RunAccessAsync(string url, string login, string password)
    {
        using var client = new AccessClient(url, login, password);

        var info = await client.GetSystemInformationAsync();
        Console.WriteLine($"Serial {info.Serial} | firmware {info.Version} | IP {info.Network?.Ip}");

        var users = await client.GetUsersAsync();
        Console.WriteLine($"{users.Count} usuários");
        foreach (var user in users.Take(10))
        {
            Console.WriteLine($"  #{user.Id} {user.Name} ({user.Registration})");
        }

        var logs = await client.GetAccessLogsAsync(limit: 10);
        foreach (var log in logs)
        {
            var when = DateTimeOffset.FromUnixTimeSeconds(log.Time);
            Console.WriteLine($"  log #{log.Id} {when:yyyy-MM-dd HH:mm:ss} evento={log.Event} usuário={log.UserId}");
        }

        await client.LogoutAsync();
        return 0;
    }

    private static async Task<int> RunRepAsync(string url, string login, string password)
    {
        using var client = new RepClient(url, login, password, RepMode.Portaria671);

        var about = await client.GetAboutAsync();
        var system = await client.GetSystemInformationAsync();
        Console.WriteLine($"Série {about.SerialNumber} | FW {about.FirmwareVersion} | último NSR {system.LastNsr}");
        Console.WriteLine($"Relógio do REP: {await client.GetDateTimeAsync():yyyy-MM-dd HH:mm:ss}");

        var afd = await client.GetAfdFileFromNsrAsync(Math.Max(1, system.LastNsr - 20));
        Console.WriteLine($"AFD ({afd.Layout}): {afd.Punches.Count} marcações, último NSR {afd.LastNsr}");
        foreach (var punch in afd.Punches.TakeLast(10))
        {
            Console.WriteLine($"  NSR {punch.Nsr} {punch.Time:yyyy-MM-dd HH:mm} {punch.EmployeeIdType} {punch.EmployeeId}");
        }

        foreach (var issue in afd.Issues)
        {
            Console.WriteLine($"  aviso: {issue}");
        }

        await client.LogoutAsync();
        return 0;
    }
}
