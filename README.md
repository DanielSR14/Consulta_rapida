# Consulta Rápida

**[⬇ Baixar para Windows](https://github.com/DanielSR14/Consulta_rapida/releases/latest)**
 · **[Site](https://danielsr14.github.io/Consulta_rapida/)**

App de bandeja para Windows (WPF, .NET 8) para escritórios de contabilidade que
usam o sistema **Domínio** (Thomson Reuters). Fica em segundo plano e, com um
atalho global (`Ctrl+Alt+D`, configurável), abre uma janela flutuante estilo
Spotlight/Raycast com busca instantânea de clientes: achou → `Enter` mostra os
detalhes (CNPJ/CPF, IE, Cidade, código da empresa) com botão "Copiar" em cada
campo → "Mais informações" abre a ficha completa (endereço, contato, CNAE,
inscrições...).

Feito para contadores, não técnicos: banco local em `%AppData%`, atalho com
fallback automático, instalador com um clique. A marca (cor + logo) é escolhida
pelo próprio usuário em Configurações e **não há base de dados embutida** — cada
escritório importa a sua.

## Como funciona a fonte de dados

O app lê de `%AppData%\ConsultaRapida\clientes.db` (SQLite). Formas de popular:

- **Configurações → "Importar clientes..."** — lê direto o relatório que a
  Domínio exporta (`Relatórios → Cadastrais → Empresas → Modelo "Completo" →
  Exportar para excel`). Substitui a base inteira.
- **Configurações → "+ Cadastrar nova empresa"** — adiciona uma empresa por vez.
- **CLI** `ClienteConsulta.exe --import <planilha.xlsx> <destino.db>` — modo
  utilitário para automação/migração.

Na primeira execução, sem base, a janela mostra um estado amigável apontando
para "Importar clientes...".

## Desenvolvimento

Requer o **.NET 8 SDK** (`winget install Microsoft.DotNet.SDK.8`).

```powershell
dotnet build ClienteConsulta.sln -c Debug
dotnet run --project src\ClienteConsulta.App\ClienteConsulta.App.csproj
```

O app abre sem janela visível (só o ícone na bandeja) — pressione `Ctrl+Alt+D`.
Configurações e banco ficam em `%AppData%\ConsultaRapida\`; para testar do zero,
apague essa pasta.

### Estrutura

```
src/
  ClienteConsulta.Core          modelos + busca (sem dependência de Excel/SQLite/WPF)
  ClienteConsulta.Data.Excel    leitura da planilha .xlsx e do relatório .xls da Domínio
  ClienteConsulta.Data.Sqlite   banco local (fonte real em runtime)
  ClienteConsulta.App           WPF: janela, bandeja, atalho global, configurações, marca
installer/                      script de publicação + Inno Setup (.iss)
tools/                          geração dos assets de marca
docs/                           landing page estática (GitHub Pages)
```

Arquitetura em camadas: `App` → `Data.Excel` + `Data.Sqlite` → `Core`. As
camadas `Data.*` não se conhecem — quem liga as duas é o `App` (`App.xaml.cs`,
composition root). Trocar a fonte de dados (SQL Server, API REST) é implementar
`ICustomerRepository` num novo projeto `Data.*` e mudar uma linha no `App`.

O relatório "Completo" da Domínio em `.xls` não é lido por ExcelDataReader nem
NPOI (terminam com 0 planilhas, sem exceção), então `Biff8Reader.cs` é um leitor
BIFF8 próprio e mínimo — só os tipos de célula que aparecem nesse relatório.

## Instalador

```powershell
installer\build-installer.ps1
```

Publica self-contained single-file (`win-x64`, não precisa .NET no destino) e
compila `installer\ConsultaRapida.iss` com o Inno Setup
(`winget install JRSoftware.InnoSetup`). Saída em `dist\`.

Checklist de nova versão: bumpar `<Version>` em
`src\ClienteConsulta.App\ClienteConsulta.App.csproj` **e** `MyAppVersion` em
`installer\ConsultaRapida.iss` (têm que bater), rodar o script, e publicar o
instalador como release:

```
gh release create vX.Y.Z dist\ConsultaRapida-Setup-X.Y.Z.exe --title "Consulta Rápida X.Y.Z" --notes "..."
```

O botão "Baixar" do site aponta para `releases/latest`, então cada release nova
vira automaticamente o download atual.

O `.exe` e o instalador **não são assinados digitalmente** — na primeira
execução o Windows mostra o aviso do SmartScreen ("Mais informações" →
"Executar assim mesmo"). É esperado.

## Licença

[MIT](LICENSE).
