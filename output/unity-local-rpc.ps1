param([string]$Method,[string]$Json='{}')
$ErrorActionPreference='Stop'
$headers=@{Accept='application/json, text/event-stream'}
$init=@{jsonrpc='2.0';id=1;method='initialize';params=@{protocolVersion='2024-11-05';capabilities=@{};clientInfo=@{name='codex-local';version='1.0'}}}|ConvertTo-Json -Depth 8
$session=Invoke-WebRequest -Uri 'http://127.0.0.1:8080/mcp' -Method Post -Headers $headers -ContentType 'application/json' -Body $init -TimeoutSec 10
$headers['Mcp-Session-Id']=[string]$session.Headers['mcp-session-id'][0]
$body=@{jsonrpc='2.0';id=2;method=$Method;params=($Json|ConvertFrom-Json)}|ConvertTo-Json -Depth 30 -Compress
$response=Invoke-WebRequest -Uri 'http://127.0.0.1:8080/mcp' -Method Post -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 50
foreach($line in ($response.Content -split "`n")){
 if($line.StartsWith('data: ')){
  $message=$line.Substring(6)|ConvertFrom-Json
  if($message.result){$message.result|ConvertTo-Json -Depth 30}
  if($message.error){throw ($message.error|ConvertTo-Json -Compress)}
 }
}
