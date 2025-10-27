# Script de Build Local - Aviseme Email Sender
# Uso: .\build-local.ps1

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Aviseme Email Sender - Build Local     " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Passo 1: Restaurar dependências
Write-Host "[STEP 1] Restaurando dependências..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Erro ao restaurar dependências" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Dependências restauradas" -ForegroundColor Green
Write-Host ""

# Passo 2: Build
Write-Host "[STEP 2] Compilando projeto..." -ForegroundColor Yellow
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Erro ao compilar projeto" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Projeto compilado" -ForegroundColor Green
Write-Host ""

# Passo 3: Publish
Write-Host "[STEP 3] Publicando para Linux..." -ForegroundColor Yellow
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Erro ao publicar projeto" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Projeto publicado em ./publish" -ForegroundColor Green
Write-Host ""

# Passo 4: Criar ZIP
Write-Host "[STEP 4] Criando arquivo ZIP..." -ForegroundColor Yellow
$zipFile = "aviseme-emailer.zip"
if (Test-Path $zipFile) {
    Remove-Item $zipFile -Force
}
Compress-Archive -Path ./publish/* -DestinationPath $zipFile
Write-Host "✅ ZIP criado: $zipFile" -ForegroundColor Green
Write-Host ""

Write-Host "==========================================" -ForegroundColor Green
Write-Host "✅ Build concluído com sucesso!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "📦 Arquivo pronto para deploy:" -ForegroundColor Yellow
Write-Host "   $((Get-Item $zipFile).FullName)" -ForegroundColor Cyan
Write-Host ""
Write-Host "📋 Para fazer deploy manual:" -ForegroundColor Yellow
Write-Host "   scp $zipFile usuario@seu-ec2:/tmp/" -ForegroundColor Cyan
Write-Host "   ssh usuario@seu-ec2" -ForegroundColor Cyan
Write-Host "   sudo unzip -o /tmp/$zipFile -d /opt/aviseme-emailer/" -ForegroundColor Cyan
Write-Host ""

