# Hook Claude Code PreToolUse (matcher : Bash).
# Refuse tout `git commit` dont le depot cible est sur la branche `main`, et tout `git push` (AGENTS.md, D3).
# Reconnait `git -C <chemin> commit`, `cd <chemin> && git commit`, les commandes chainees et les wrappers
# (tout token `git` d'un segment, ou qu'il soit). Entree : JSON du hook sur stdin. Sortie : JSON `deny` ou rien.
$ErrorActionPreference = 'Stop'

$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }
try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }

$command = [string]$payload.tool_input.command
$cwd = [string]$payload.cwd
if ([string]::IsNullOrWhiteSpace($command)) { exit 0 }
if ([string]::IsNullOrWhiteSpace($cwd)) { $cwd = (Get-Location).Path }

function Deny([string]$reason) {
    $decision = @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'deny'
            permissionDecisionReason = $reason
        }
    }
    $decision | ConvertTo-Json -Compress -Depth 5
    exit 0
}

function Resolve-Dir([string]$base, [string]$target) {
    if ([System.IO.Path]::IsPathRooted($target)) { return $target }
    return (Join-Path $base $target)
}

# Les corps de heredoc (<<EOF ... EOF, <<'EOF', <<"EOF", <<-EOF) sont des donnees (messages de commit, contenus de fichiers) : on les retire avant l'analyse.
$command = [regex]::Replace($command, "<<-?\s*['""]?(\w+)['""]?[^\r\n]*\r?\n[\s\S]*?\r?\n\1(?=\s|$)", ' ')

# Segments d'une commande chainee : &&, ||, ;, |
$segments = [regex]::Split($command, '\s*(?:&&|\|\||;|\|)\s*')
$currentDir = $cwd

foreach ($segment in $segments) {
    $tokens = @([regex]::Matches($segment, '"[^"]*"|''[^'']*''|\S+') | ForEach-Object { $_.Value.Trim('"', "'") })
    if ($tokens.Count -eq 0) { continue }

    if ($tokens[0] -eq 'cd' -and $tokens.Count -ge 2) {
        $currentDir = Resolve-Dir $currentDir $tokens[1]
        continue
    }

    for ($i = 0; $i -lt $tokens.Count; $i++) {
        if ($tokens[$i] -ne 'git') { continue }

        $repoDir = $currentDir
        $unresolved = $false
        $sub = $null
        $j = $i + 1
        while ($j -lt $tokens.Count) {
            $t = $tokens[$j]
            if ($t -eq '-C') {
                if ($j + 1 -lt $tokens.Count) { $repoDir = Resolve-Dir $repoDir $tokens[$j + 1] }
                $j += 2; continue
            }
            if ($t -eq '-c') { $j += 2; continue }
            if ($t -like '--git-dir*' -or $t -like '--work-tree*') {
                $unresolved = $true
                $j += 1
                if (-not $t.Contains('=')) { $j += 1 }
                continue
            }
            if ($t.StartsWith('-')) { $j += 1; continue }
            $sub = $t
            break
        }
        if ($null -eq $sub) { continue }

        if ($sub -eq 'push') {
            Deny 'Push interdit sans demande explicite de l''auteur (AGENTS.md, D3).'
        }
        if ($sub -eq 'commit') {
            if ($unresolved) { Deny 'Commit refuse : depot cible non resolu (--git-dir ou --work-tree).' }
            $branch = ''
            try { $branch = ((& git -C $repoDir branch --show-current 2>$null) | Out-String).Trim() } catch { $branch = '' }
            if ($branch -eq 'main') { Deny 'Commit sur main interdit : cree une branche dediee (AGENTS.md, D3).' }
            if ($branch -eq '') { Deny 'Commit refuse : depot cible non resolu.' }
        }
    }
}
exit 0
