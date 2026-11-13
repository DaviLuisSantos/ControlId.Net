using System;
using System.Threading.Tasks;
using ControlId.Access;
using ControlId.Access.Configuration;
using ControlId.Rep;
using ControlId.Rep.Afd;

// Só precisa compilar e publicar: o objetivo é contar os avisos de trim/AOT, não conversar com equipamento.
var access = new AccessClient("http://127.0.0.1:1");
var rep = new RepClient("http://127.0.0.1:1");

try
{
    await access.GetUsersAsync();
    await access.LoadObjectsAsync<AccessUser>(new LoadObjectsRequest("users"));
    await access.GetConfigurationSectionAsync<BuzzerSettings>();
    await rep.GetAboutAsync();
}
catch (Exception ex)
{
    Console.WriteLine(ex.GetType().Name);
}

var afd = AfdParser.Parse("");
Console.WriteLine(afd.GetType().Name);
