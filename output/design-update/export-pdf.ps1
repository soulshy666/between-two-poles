param([string]$InputDoc,[string]$PdfOut)
$ErrorActionPreference='Stop'
$app=New-Object -ComObject Word.Application
$document=$null
try {
 $app.Visible=$false
 $app.DisplayAlerts=0
 $document=$app.Documents.Open($InputDoc,$false,$true)
 $document.ExportAsFixedFormat($PdfOut,17)
} finally {
 if($null -ne $document){try{$document.Close(0)}catch{}}
 try{$app.Quit()}catch{}
}
if(!(Test-Path -LiteralPath $PdfOut)){throw 'PDF export did not create a file'}
