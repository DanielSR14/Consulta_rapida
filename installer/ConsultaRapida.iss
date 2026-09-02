; Script do instalador (Inno Setup 6) do "Consulta Rápida".
;
; Como gerar um novo instalador (ex: depois de adicionar uma feature nova):
;   1. Atualize <Version> em src\ClienteConsulta.App\ClienteConsulta.App.csproj
;   2. Atualize MyAppVersion abaixo (mesmo número)
;   3. Rode installer\build-installer.ps1 (publica self-contained + compila este .iss)
;   4. O instalador final fica em dist\ConsultaRapida-Setup-<versao>.exe
;
; O AppId abaixo é fixo de propósito: é o que faz o Inno Setup reconhecer uma
; instalação futura como ATUALIZAÇÃO da mesma aplicação (substitui os arquivos,
; mantém o atalho e as configurações do usuário em %AppData%) em vez de instalar
; uma cópia paralela. Nunca troque esse GUID entre versões.
;
; Observação: o app NÃO é assinado digitalmente (ver seção "Distribuição" no
; CLAUDE.md). O SmartScreen do Windows mostra um aviso na primeira execução —
; é esperado, não é vírus.

#define MyAppId "{{7230CCDC-FE37-4165-BCF3-A4CC35A453AA}}"
#define MyAppName "Consulta Rápida"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Consulta Rápida"
#define MyAppExeName "ClienteConsulta.exe"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Consulta Rapida
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=ConsultaRapida-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\ClienteConsulta.App\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no
DisableWelcomePage=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos adicionais:"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir {#MyAppName} agora"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
; Garante que o app (ícone na bandeja) seja encerrado antes de remover os arquivos.
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#MyAppExeName} /F"; Flags: runhidden skipifdoesntexist; RunOnceId: "KillAppBeforeUninstall"
