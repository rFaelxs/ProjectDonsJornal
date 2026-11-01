<#
.SYNOPSIS
    Registra a coleta diária no Agendador de Tarefas do Windows.

.DESCRIPTION
    O marco M2 (spec 09) é caminho crítico: sem dias de acervo acumulado não existe
    demonstração de conteúdos relacionados (RF21), que olham 30 dias para trás. Este
    script liga a coleta agora, antes do ciclo completo do M5 existir.

    A tarefa roda `jornal coletar`, que lê os feeds ativos, normaliza, deduplica e indexa.
    É idempotente: rodar de novo não duplica item nem trecho.

.PARAMETER Horario
    Hora da execução diária, em HH:mm. Padrão 05:30 — uma hora antes do horário padrão
    de geração da edição (06:30), para o acervo do dia já estar indexado.

.PARAMETER Remover
    Remove a tarefa em vez de registrá-la.

.EXAMPLE
    .\scripts\registrar-coleta-diaria.ps1
    .\scripts\registrar-coleta-diaria.ps1 -Horario 04:00
    .\scripts\registrar-coleta-diaria.ps1 -Remover

.NOTES
    Precisa de PowerShell com permissão para criar tarefas do usuário atual. Não exige
    administrador, porque a tarefa roda no contexto do próprio usuário.
#>
[CmdletBinding()]
param(
    [ValidatePattern('^([01]\d|2[0-3]):[0-5]\d$')]
    [string]$Horario = '05:30',

    [switch]$Remover
)

$ErrorActionPreference = 'Stop'

$nomeTarefa = 'JornalPersonalizado-ColetaDiaria'
$raizRepo = Split-Path -Parent $PSScriptRoot
$projetoCli = Join-Path $raizRepo 'src\Jornal.Cli'

if ($Remover) {
    $existente = Get-ScheduledTask -TaskName $nomeTarefa -ErrorAction SilentlyContinue
    if ($existente) {
        Unregister-ScheduledTask -TaskName $nomeTarefa -Confirm:$false
        Write-Output "Tarefa '$nomeTarefa' removida."
    }
    else {
        Write-Output "Tarefa '$nomeTarefa' não existe."
    }
    return
}

if (-not (Test-Path $projetoCli)) {
    throw "Projeto do CLI não encontrado em $projetoCli. Rode o script de dentro do repositório."
}

# Publicar deixa a tarefa independente do SDK e muito mais rápida para iniciar que
# `dotnet run`, que recompila a cada execução.
$pastaPublicacao = Join-Path $raizRepo 'artifacts\cli'
Write-Output "Publicando o CLI em $pastaPublicacao ..."
dotnet publish $projetoCli -c Release -o $pastaPublicacao --nologo | Out-Null

$executavel = Join-Path $pastaPublicacao 'jornal.exe'
if (-not (Test-Path $executavel)) {
    throw "Publicação não gerou $executavel."
}

# O diretório de trabalho precisa ser a raiz do repo: o CLI resolve `data/` e `prompts/`
# relativos a ele.
$acao = New-ScheduledTaskAction -Execute $executavel -Argument 'coletar' -WorkingDirectory $raizRepo
$gatilho = New-ScheduledTaskTrigger -Daily -At $Horario

$configuracao = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -DontStopOnIdleEnd `
    -ExecutionTimeLimit (New-TimeSpan -Hours 1) `
    -MultipleInstances IgnoreNew

# StartWhenAvailable cobre o caso da máquina desligada no horário: a coleta roda assim
# que o Windows subir, no mesmo espírito do catch-up do agendador da spec 07.

$existente = Get-ScheduledTask -TaskName $nomeTarefa -ErrorAction SilentlyContinue
if ($existente) {
    Write-Output "Atualizando a tarefa '$nomeTarefa'..."
    Unregister-ScheduledTask -TaskName $nomeTarefa -Confirm:$false
}

Register-ScheduledTask `
    -TaskName $nomeTarefa `
    -Action $acao `
    -Trigger $gatilho `
    -Settings $configuracao `
    -Description 'Jornal Personalizado: coleta e indexa os feeds ativos (spec 04, marco M2).' `
    | Out-Null

Write-Output ''
Write-Output "Tarefa '$nomeTarefa' registrada para todos os dias às $Horario."
Write-Output ''
Write-Output 'Conferir  : Get-ScheduledTask -TaskName JornalPersonalizado-ColetaDiaria'
Write-Output 'Rodar já  : Start-ScheduledTask -TaskName JornalPersonalizado-ColetaDiaria'
Write-Output 'Histórico : Get-ScheduledTaskInfo -TaskName JornalPersonalizado-ColetaDiaria'
Write-Output 'Remover   : .\scripts\registrar-coleta-diaria.ps1 -Remover'
Write-Output ''
Write-Output 'A tarefa precisa do Postgres e do Ollama de pé. Mantenha o Docker Desktop'
Write-Output 'configurado para iniciar com o Windows, ou a coleta do dia falha.'
