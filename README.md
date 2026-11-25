# PC Monitor

Monitor local de desempenho e eventos do Windows, com interface em português e servidor MCP para consultar os dados em um assistente compatível.

Versão do código: **2.0.2**.

## Recursos

- Métricas de CPU, memória, atividade dos discos, espaço livre, rede e maiores processos.
- Dados de GPU NVIDIA quando disponíveis pelo coletor.
- Histórico, erros e avisos dos registros System e Application do Windows.
- Marcadores manuais para investigar lentidão e travamentos.
- Interface WPF com temas claro e escuro, ajuda e execução na bandeja.
- Inicialização opcional com o Windows, com atraso de 30 segundos.
- Servidor MCP local, com transporte stdio e sete ferramentas.

## Requisitos

Windows x64, .NET Framework 4.8 e componentes WPF. A compilação usa o compilador do .NET Framework presente no Windows; não exige SDK do .NET, Python ou Node.js.

As bibliotecas ModernWpfUI 0.9.6 necessárias à interface estão incluídas, com a licença em `MODERNWPF-LICENSE.txt` e a identificação em `THIRD-PARTY.txt`.

## Compilar e executar

Abra o PowerShell na pasta deste repositório e execute:

```powershell
.\build.ps1
.\PCMonitor.exe
```

A compilação gera os executáveis, o núcleo, o arquivo de interface e os binários da integração MCP. Os arquivos gerados não são versionados.

Para iniciar apenas na bandeja:

```powershell
.\PCMonitor.exe --tray --startup-delay 30
```

Fechar a janela mantém o programa na bandeja. Use **Sair** no menu do ícone para encerrar. A inicialização automática é opcional e pode ser configurada em Preferências.

## Executar os testes existentes

Os testes devem usar uma pasta isolada, sem os registros reais do computador:

```powershell
$testData = Join-Path $env:TEMP ('pc-monitor-test-' + [guid]::NewGuid().ToString('N'))
$testRun = Start-Process -FilePath (Join-Path $PWD 'PCMonitor.exe') -ArgumentList @('--self-test', '--data-dir', ('"' + $testData + '"')) -WindowStyle Hidden -PassThru -Wait
Get-Content -LiteralPath (Join-Path $testData 'selftest-result.json')
if ($testRun.ExitCode -ne 0) { throw 'Os testes falharam.' }
```

## Integração MCP

O servidor `PCMonitor.Mcp.exe` oferece:

| Ferramenta | Finalidade |
| --- | --- |
| `get_status` | Estado da coleta, versão e capacidades |
| `get_metrics` | Amostra atual, fonte e idade |
| `get_history` | Histórico e resumo das métricas |
| `get_events` | Erros e avisos do Windows registrados |
| `get_incidents` | Alertas, falhas da coleta e marcadores |
| `get_system_info` | Dados básicos do sistema e fuso |
| `mark_incident` | Registrar um marcador solicitado pelo usuário |

Para verificar o servidor sem configurar um cliente MCP:

```powershell
.\PCMonitor.Mcp.exe --data-dir "$env:LOCALAPPDATA\PCMonitor" --query get_status
```

Para conectar um assistente, configure um servidor stdio apontando para o caminho absoluto de `PCMonitor.Mcp.exe`. Caso use `--data-dir`, passe o caminho absoluto da pasta de dados como argumento separado. O diretório padrão é `%LOCALAPPDATA%\PCMonitor`.

O diretório `integration/pc-monitor-plugin` inclui o manifesto, a configuração MCP e a skill. Compile antes de usar o pacote: `build.ps1` preenche a subpasta `server`. Os caminhos com `%LOCALAPPDATA%` no arquivo de referência são exemplos e precisam ser substituídos pelo caminho real da conta ao configurar o cliente.

O protocolo implementado é MCP 2025-11-25, com mensagens JSON-RPC UTF-8 por linha. A comunicação com o monitor usa um canal local do Windows restrito à conta atual; não abre uma porta HTTP.

## Dados e privacidade

Os dados ficam em `%LOCALAPPDATA%\PCMonitor`. O repositório contém somente o programa e sua documentação, sem os logs pessoais usados durante o desenvolvimento.

Mensagens de eventos e observações podem conter dados pessoais. A ocultação automática de segredos é parcial: revise uma exportação antes de compartilhá-la. Os arquivos `.jsonl`, logs, exportações, configurações locais e arquivos de credenciais estão ignorados pelo Git.

## Frequências e limites

Por padrão, métricas são coletadas a cada 2 segundos; processos, a cada 5 segundos; GPU, a cada 10 segundos; e eventos, a cada 15 segundos. A retenção padrão é de até 7 dias ou 200 MB, o primeiro limite atingido.

As consultas de histórico têm leitura limitada: a cobertura efetivamente retornada pode ser menor que o intervalo solicitado. Valores `null` representam dados indisponíveis.

Não são medidos FPS, temperatura da CPU, saúde SMART, integridade física da RAM ou inventário de drivers. Alertas não comprovam defeito nem estabelecem a causa de uma queda de desempenho.

## Organização

- `src/`: código C#, interface XAML, ícone e manifesto.
- `build.ps1`: compilação e montagem do servidor da integração.
- `integration/pc-monitor-plugin/`: integração MCP e instruções do assistente.
- `AJUDA.txt`: guia detalhado do aplicativo.
- `NOVIDADES.txt`: alterações da versão 2.0.2.
- `THIRD-PARTY.txt` e `MODERNWPF-LICENSE.txt`: dependência da interface e sua licença.

O arquivo `MODERNWPF-LICENSE.txt` se aplica à biblioteca ModernWpfUI. Ele não define uma licença para o código do PC Monitor.
