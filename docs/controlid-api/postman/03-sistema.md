# Sistema

<!-- fonte: https://documenter.getpostman.com/view/10800185/SztHW4xo -->

## Reiniciar

`POST {{host}}/reboot.fcgi?session={{session}}`

## Reiniciar sem sessão ativa

`GET {{host}}/reboot`

**Exemplo de resposta — Com endpoint de reinício ativado** (200 OK):

```
<html style="background-color: rgb(61, 61, 61);">
    <head>
        <link rel="shortcut icon" href="/images/favicon.ico" />
        <meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
        <meta content="width=device-width, initial-scale=1.0" name="viewport">
        <meta content="width=device-width, initial-scale=1.0" name="viewport">
        <meta content="" name="description">
        <meta content="" name="author">




    </head>
    <body style="background-color: rgb(61, 61, 61);" class="page-header-fixed page-sidebar-fixed page-footer-fixed">
        <!-- BEGIN GLOBAL MANDATORY STYLES -->
        <link href="/assets/css/themes/default.css" rel="stylesheet" type="text/css" id="style_color">
        <link href="/assets/plugins/bootstrap/css/bootstrap.css" rel="stylesheet" type="text/css">
        <link href="/assets/plugins/bootstrap/css/bootstrap-responsive.css" rel="stylesheet" type="text/css">
        <link href="/assets/plugins/font-awesome/css/font-awesome.css" rel="stylesheet" type="text/css">
        <link href="/assets/css/style-metro.css" rel="stylesheet" type="text/css">
        <link href="/assets/css/style.css" rel="stylesheet" type="text/css">
        <link href="/assets/css/style-responsive.css" rel="stylesheet" type="text/css">
        <link href="/assets/css/error.css" rel="stylesheet" type="text/css">
        <link href="/assets/plugins/uniform/css/uniform.default.css" rel="stylesheet" type="text/css">
        <link href="/assets/plugins/bootstrap-switch/static/stylesheets/bootstrap-switch-metro.css" rel="stylesheet" type="text/css">
        <link href="/assets/plugins/glyphicons_halflings/css/halflings.css" rel="stylesheet" type="text/css">
        <link href="/assets/plugins/glyphicons/css/glyphicons.css" rel="stylesheet" type="text/css">
        <link rel="stylesheet" type="text/css" href="/assets/plugins/datepicker/datepicker.css">
        <link rel="stylesheet" type="text/css" href="/assets/plugins/timepicker/timepicker.css">
        <link rel="stylesheet" type="text/css" href="/assets/plugins/modal/bootstrap-modal.css">
        <link rel="stylesheet" type="text/css" href="/assets/plugins/table/table.css">
        <link rel="stylesheet" type="text/css" href="/assets/plugins/chosen/chosen.css">
        <link rel="stylesheet" type="text/css" href="/assets/plugins/timeline/timeline.css">
        <link rel="stylesheet" type="text/css" href="/assets/plugins/wColorPicker/wColorPicker.css">
        <!-- END GLOBAL MANDATORY STYLES -->
        <!-- BEGIN HEADER -->
        <div id="MasterPage_header" class="header navbar navbar-inverse navbar-fixed-top">
            <!-- BEGIN TOP NAVIGATION BAR -->
            <div class="navbar-inner">
                <div class="container-fluid">
                    <!-- BEGIN LOGO -->
                    <a class="brand" href="/pt_BR/html/index.html">
                        <img src="/images/logoW.png" alt="logo" width="120px">
			
                    </a>
                    <!-- END TOP NAVIGATION MENU -->
		
                </div>
            </div>
            <!-- END TOP NAVIGATION BAR -->
        </div>
        <!-- END HEADER -->
        <div id="MasterConteudo" style="" class="page-container row-fluid">
            <div class="col-md-6 page-404">
                <br>
                <br>
                <br>
                <div class="details font-light-grey">
                    <h3>Reiniciando dispositivo!</h3>
                </div>
            </div>
        </div>
        <div id="MasterPage_footer" class="footer" style="">
            <div class="footer-inner">

	</div>
            <div class="footer-tools">
                <br />
            </div>
        </div>
    </body>
</html>
```

**Exemplo de resposta — Com endpoint de reinício desativado** (404 Not Found):

```
<?xml version="1.0" encoding="iso-8859-1"?>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN"
         "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml" xml:lang="en" lang="en">
    <head>
        <title>404 Not Found</title>
    </head>
    <body>
        <h1>404 Not Found</h1>
    </body>
</html>
```

## Voltar todas configurações de fábrica

`POST {{host}}/reset_to_factory_default.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"keep_network_info": false
}
```

## Voltar configurações de fábrica exceto rede

`POST {{host}}/reset_to_factory_default.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"keep_network_info": true
}
```

## Remover todos administradores

`POST {{host}}/delete_admins.fcgi?session={{session}}`

## Reiniciar em modo de recuperação

`POST {{host}}/reboot_recovery.fcgi?session={{session}}`

## Alterar data/hora

`POST {{host}}/set_system_time.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"day": 21,
	"month": 8,
	"year": 2019,
	"hour": 0,
	"minute": 59,
	"second": 50
}
```

## Alterar senha mestre

`POST {{host}}/master_password.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
	"password": "000123"
}
```

## Alterar configuração de rede

`POST {{host}}/set_system_network.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "ip": "192.168.110.200",
    "netmask": "255.255.128.0",
    "gateway": "192.168.0.1",
    "custom_hostname_enabled": true,
    "device_hostname": "Control iD",
    "web_server_port": 80,
    "dns_primary": "1.1.1.1",
    "dns_secondary": "1.1.4.4",
    "ssl_enabled": false,
    "self_signed_certificate": true
}
```

## Habilitar 802.1X

`POST {{host}}/configure_802_1X.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "enabled": true,
    "login": "testing",
    "password": "password",
    "inner_auth": 0
}
```

## Alterar configuração de OpenVPN

`POST {{host}}/set_vpn_information.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "enabled": true,
    "login_enabled": false,
    "login": "Admin",
    "password": "Admin"
}
```

## Alterar arquivo de configuração de VPN com arquivo .config

`POST {{host}}/set_vpn_file.fcgi?file_type=config&session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:** arquivo binário `/home/lucasgranela/Downloads/Exemplo de uso - Usuario e Senha/vpn_config.conf`

## Alterar arquivo de configuração de VPN com arquivo .zip

`POST {{host}}/set_vpn_file.fcgi?file_type=zip&session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:** arquivo binário `/home/lucasgranela/Downloads/Exemplo de uso - Usuario e Senha/zip.zip`

## Receber arquivo de Exemplo OpenVPN

`POST {{host}}/get_vpn_file.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ }
```

**Exemplo de resposta — Receber arquivo de Exemplo OpenVPN** (200 OK):

*(corpo binário omitido, 8043 caracteres; veja `colecao.json`)*

## Recebe as configurações de OpenVPN

`POST {{host}}/get_vpn_information.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ }
```

**Exemplo de resposta — Recebe as configurações de OpenVPN** (200 OK):

```json
{
    "enabled": true,
    "login_enabled": false,
    "login": "Admin",
    "password": true
}
```

## Recebe Status da conexão OpenVPN

`POST {{host}}/get_vpn_status.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ }
```

**Exemplo de resposta — Recebe Status da conexão OpenVPN** (200 OK):

```json
{
    "status": 9
}
```

## Recebe a confirmação se já possui um arquivo de configuração

`POST {{host}}/has_vpn_file.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{ }
```

**Exemplo de resposta — Recebe a confirmação se já possui um arquivo de configuração** (200 OK):

```json
{
    "has_file": true
}
```

## Obter informações do sistema

`POST {{host}}/system_information.fcgi?session={{session}}`

**Exemplo de resposta — Sucesso** (200 OK):

```json
{
    "uptime": {
        "days": 0,
        "hours": 0,
        "minutes": 12,
        "seconds": 8
    },
    "time": 1562277503,
    "memory": {
        "disk": {
            "free": 33812480,
            "total": 33976320
        },
        "ram": {
            "free": 9199616,
            "total": 61255680
        }
    },
    "license": {
        "users": 0,
        "device": 0,
        "type": 0
    },
    "biometrics": {
        "max_num_records": 10000
    },
    "network": {
        "mac": "mac",
        "ip": "192.168.110.200",
        "netmask": "255.255.128.0",
        "gateway": "192.168.0.1",
        "primary_dns": "1.1.1.1",
        "secondary_dns": "1.1.4.4",
        "custom_hostname_enabled": true,
        "device_hostname": "Control iD",
        "web_server_port": 80,
        "ssl_enabled": false,
        "dhcp_enabled": false,
        "self_signed_certificate": true,
        "ten_mbps": true
    },
    "auth802_1x": {
    "enabled": false,
    "login": "",
    "inner_auth": 3
    },
    "serial": "serial",
    "version": "5.9.3",
    "device_id": "device_id",
    "secbox_version": "2.1.2",
    "iDCloud_code": "iDCloud_code",
    "online": false,
    "online_available": false
}
```

## Obter logs de sistema

`POST {{host}}/get_ac_log.fcgi?session={{session}}`

**Exemplo de resposta — Sucesso** (200 OK):

```json
[19-07-04T21:46:26]
Invalid license
./lib/libac.so: ac::license_exception::license_exception(std::string const&)+0x14
./lib/libac.so: ac::license::license(std::istream&)+0x204
./lib/libac.so: ac::license::initialize()+0x50
./core: core_init()+0x5b0
./core: main+0x54
/lib/libc.so.6: __libc_start_main+0x110
[19-07-04T21:46:31]
Failed to obtain device file path
./plugins/barras.so: init+0x2b8
./lib/libac.so: ac::Plugin::init(std::string*)+0xc
./core: load_plugins(std::list<ac::Plugin, std::allocator<ac::Plugin> >&)+0x26c
./core: main+0x64
/lib/libc.so.6: __libc_start_main+0x110
[19-07-04T21:50:27]
Invalid member 'year' (int expected, got undefined)
./lib/libac.so: +0x31244
./lib/libac.so: ac::util::json::get_int(rapidjson::GenericValue<rapidjson::UTF8<char>, rapidjson::MemoryPoolAllocator<rapidjson::CrtAllocator> > const&, std::string const&, std::function<int ()>)+0xac
./plugins/fcgi_server.so: fcgi_server::fcgi_util::set_system_time(fcgi_server::request const&, fcgi_server::response&)+0xbc
./plugins/fcgi_server.so: +0x64244
./plugins/fcgi_server.so: fcgi_server::worker::operator()()+0x5c8
/usr/lib/libstdc++.so.6: +0x83978
Previous frame identical to this frame
```

## Obter relatório de logs de auditoria para todos os filtros

`POST {{host}}/export_audit_logs.fcgi?session={{session}}`

**Corpo:**

```json
{
    "config": 1,
    "api": 1,
    "usb": 1,
    "network": 1,
    "time": 1,
    "online": 1,
    "menu": 1
}
```

**Exemplo de resposta — Sucesso** (200 OK):

```json
[19-07-04T21:46:26]
Invalid license
./lib/libac.so: ac::license_exception::license_exception(std::string const&)+0x14
./lib/libac.so: ac::license::license(std::istream&)+0x204
./lib/libac.so: ac::license::initialize()+0x50
./core: core_init()+0x5b0
./core: main+0x54
/lib/libc.so.6: __libc_start_main+0x110
[19-07-04T21:46:31]
Failed to obtain device file path
./plugins/barras.so: init+0x2b8
./lib/libac.so: ac::Plugin::init(std::string*)+0xc
./core: load_plugins(std::list<ac::Plugin, std::allocator<ac::Plugin> >&)+0x26c
./core: main+0x64
/lib/libc.so.6: __libc_start_main+0x110
[19-07-04T21:50:27]
Invalid member 'year' (int expected, got undefined)
./lib/libac.so: +0x31244
./lib/libac.so: ac::util::json::get_int(rapidjson::GenericValue<rapidjson::UTF8<char>, rapidjson::MemoryPoolAllocator<rapidjson::CrtAllocator> > const&, std::string const&, std::function<int ()>)+0xac
./plugins/fcgi_server.so: fcgi_server::fcgi_util::set_system_time(fcgi_server::request const&, fcgi_server::response&)+0xbc
./plugins/fcgi_server.so: +0x64244
./plugins/fcgi_server.so: fcgi_server::worker::operator()()+0x5c8
/usr/lib/libstdc++.so.6: +0x83978
Previous frame identical to this frame
```

## Obter metadados de objetos

`POST {{host}}/object_metadata.fcgi?session={{session}}`

## Exportar objetos

`POST {{host}}/export_objects.fcgi?session={{session}}`

**Headers:** `Content-Type: application/json`

**Corpo:**

```json
{
    "objects":[
        {"object":"users"},
        {"object":"cards","columns":["value","user_id"]},{"object":"user_roles"},
        {"object":"c_users","columns":["id","user_id","cpf"]}
    ]
}
```
