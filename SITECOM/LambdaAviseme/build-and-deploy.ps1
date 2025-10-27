# Build e Deploy Lambda Aviseme
Write-Host "Build Lambda Aviseme..." -ForegroundColor Green

dotnet publish --configuration Release --output ./publish --property PublishReadyToRun=false

if ($LASTEXITCODE -eq 0) {
    Compress-Archive -Path ".\publish\*" -DestinationPath "lambda-aviseme.zip" -Force
    $size = "{0:N1} MB" -f ((Get-Item "lambda-aviseme.zip").Length / 1MB)
    Write-Host "ZIP criado: lambda-aviseme.zip ($size)" -ForegroundColor Green
} else {
    Write-Host "Erro no build!" -ForegroundColor Red
}