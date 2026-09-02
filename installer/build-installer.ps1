<#
.SYNOPSIS
    Publica o "Consulta Rápida" (self-contained, arquivo único) e gera o instalador (Setup.exe).

.DESCRIPTION
    Use este script sempre que quiser gerar um novo instalador para distribuir - seja a
    primeira versão, seja depois de adicionar uma feature nova.

    O app NÃO traz banco de dados embutido: cada escritório importa a própria base pela tela
    Configurações -> "Importar clientes..." (relatório de empresas do sistema Domínio).

    Passos que ele executa:
      1. dotnet publish self-contained (win-x64, não precisa de .NET no PC de destino)
      2. (opcional, com -Sign) assina o .exe publicado - ver bloco "assinatura" abaixo
      3. Compila installer\ConsultaRapida.iss com o Inno Setup (ISCC.exe)
      4. (opcional, com -Sign) assina o instalador gerado
      5. O instalador final fica em dist\ConsultaRapida-Setup-<versao>.exe

.PARAMETER Sign
    Assina o .exe e o instalador (requer um certificado / Azure Trusted Signing configurado -
    ver a seção "Distribuição e o aviso do Windows" no CLAUDE.md). Sem esse parâmetro, os
    binários saem sem assinatura (padrão atual).

.NOTES
    Antes de rodar para uma nova versão: atualize a tag <Version> em
    src\ClienteConsulta.App\ClienteConsulta.App.csproj E a linha MyAppVersion em
    installer\ConsultaRapida.iss (os dois precisam bater).
#>

param(
    [switch]$Sign
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$appCsproj = Join-Path $repoRoot "src\ClienteConsulta.App\ClienteConsulta.App.csproj"
$publishDir = Join-Path $repoRoot "publish\win-x64"
$issFile = Join-Path $repoRoot "installer\ConsultaRapida.iss"

function Stop-RunningApp {
    # Se o .exe publicado (ou instalado) estiver aberto - ex: deixado rodando de um teste
    # manual anterior - build/publish/Remove-Item falham com "acesso negado" no arquivo
    # travado. O nome do processo é o do .exe (ClienteConsulta), mantido de propósito.
    $procs = Get-Process -Name "ClienteConsulta" -ErrorAction SilentlyContinue
    if ($procs) {
        Write-Host "==> Encerrando instância(s) do Consulta Rápida em execução (PID: $($procs.Id -join ', '))..." -ForegroundColor Yellow
        $procs | Stop-Process -Force
        Start-Sleep -Milliseconds 500
    }
}

$isccCandidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup (ISCC.exe) não encontrado. Instale com: winget install JRSoftware.InnoSetup"
}

Stop-RunningApp

Write-Host "==> Publicando aplicativo (self-contained, win-x64)..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish $appCsproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou." }

# --- assinatura (opcional) ----------------------------------------------------------------
# Hoje os binários saem SEM assinatura (decisão de projeto). Para ligar assinatura no futuro
# (ex: Azure Trusted Signing, ~US$10/mês, sem token físico), preencha o bloco abaixo e rode
# com -Sign. Ver "Distribuição e o aviso do Windows (SmartScreen)" no CLAUDE.md.
if ($Sign) {
    # $signtool = "C:\Program Files (x86)\Windows Kits\10\bin\x64\signtool.exe"
    # & $signtool sign /fd SHA256 /tr http://timestamp.acs.microsoft.com /td SHA256 `
    #     /dlib "<Azure.CodeSigning.Dlib>" /dmdf "<metadata.json>" `
    #     (Join-Path $publishDir "ClienteConsulta.exe")
    # if ($LASTEXITCODE -ne 0) { throw "Assinatura do .exe falhou." }
    Write-Host "==> -Sign informado, mas o bloco de assinatura ainda não foi configurado (ver CLAUDE.md)." -ForegroundColor Yellow
}

Write-Host "==> Gerando instalador com Inno Setup..." -ForegroundColor Cyan
& $iscc $issFile
if ($LASTEXITCODE -ne 0) { throw "ISCC (Inno Setup) falhou." }

$setupExe = Get-ChildItem (Join-Path $repoRoot "dist") -Filter "ConsultaRapida-Setup-*.exe" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($Sign -and $setupExe) {
    # & $signtool sign /fd SHA256 /tr http://timestamp.acs.microsoft.com /td SHA256 `
    #     /dlib "<Azure.CodeSigning.Dlib>" /dmdf "<metadata.json>" $setupExe.FullName
    # if ($LASTEXITCODE -ne 0) { throw "Assinatura do instalador falhou." }
}

Write-Host "==> Pronto! Instalador em: $($setupExe.FullName)" -ForegroundColor Green
