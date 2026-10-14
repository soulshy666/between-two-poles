$ErrorActionPreference = 'Stop'
$sceneRoot = Join-Path $PSScriptRoot '../Assets/BetweenPoles/Scenes'
$sceneNames = @('Chapter01_IceWorld', 'Chapter02_VerdantWorld', 'Chapter03_RingWorld', 'Chapter04_LavaWorld')
$random = [System.Random]::new(20261014)
$pattern = '(?m)^  blocked: 1\r?\n  rockStyle: (\d+)\r?\n  lowRockVariant: (\d+)\r?\n  rockTheme: (\d+)\r?\n  rockYaw: ([^\r\n]+)'
for ($i = 0; $i -lt $sceneNames.Count; $i++) {
    $scenePath = Join-Path $sceneRoot ($sceneNames[$i] + '.unity')
    $before = [System.IO.File]::ReadAllText($scenePath)
    $rocks = [regex]::Matches($before, $pattern)
    if ($rocks.Count -eq 0) { throw "No rocks found: $scenePath" }
    $theme = $i + 1
    $after = [regex]::Replace($before, $pattern, [System.Text.RegularExpressions.MatchEvaluator]{ param($match)
        $yaw = $random.Next(0, 3600) / 10.0
        $yawText = $yaw.ToString('0.0', [System.Globalization.CultureInfo]::InvariantCulture)
        $value = $match.Value -replace 'rockStyle: \d+', 'rockStyle: 2'
        $value = $value -replace 'lowRockVariant: \d+', 'lowRockVariant: 0'
        $value = $value -replace 'rockTheme: \d+', "rockTheme: $theme"
        return $value -replace 'rockYaw: [^\r\n]+', "rockYaw: $yawText"
    })
    if ([regex]::Replace($before, $pattern, '<rock>') -cne [regex]::Replace($after, $pattern, '<rock>')) {
        throw "Unexpected unrelated scene change: $scenePath"
    }
    [System.IO.File]::WriteAllText($scenePath, $after, [System.Text.UTF8Encoding]::new($false))
    $saved = [regex]::Matches([System.IO.File]::ReadAllText($scenePath), $pattern)
    foreach ($rock in $saved) {
        if ($rock.Groups[1].Value -ne '2' -or $rock.Groups[2].Value -ne '0' -or $rock.Groups[3].Value -ne "$theme") {
            throw "Incorrect saved rock configuration: $scenePath"
        }
    }
    $yaws = @($saved | ForEach-Object { $_.Groups[4].Value } | Sort-Object -Unique)
    Write-Output "$($sceneNames[$i]): $($saved.Count) original low rocks; theme=$theme; $($yaws.Count) distinct saved rotations"
}
