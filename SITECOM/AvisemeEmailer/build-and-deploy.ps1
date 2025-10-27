# Script de Build e Deploy - Aviseme Email Sender
# Uso: .\build-and-deploy.ps1

param(
    [string]$EC2Host = "",
    [string]$EC2User = "ec2-user",
    [string]$KeyFile = ""
)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Aviseme Email Sender - Build & Deploy  " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Verificar parâmetros
if ([string]::IsNullOrEmpty($EC2Host)) {
    Write-Host "❌ Erro: Host do EC2 não fornecido!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Uso:" -ForegroundColor Yellow
    Write-Host "  .\build-and-deploy.ps1 -EC2Host <ip-ou-hostname> -EC2User <usuario> -KeyFile <caminho-chave.pem>" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Exemplo:" -ForegroundColor Yellow
    Write-Host "  .\build-and-deploy.ps1 -EC2Host 54.123.45.67 -EC2User ec2-user -KeyFile C:\keys\minha-chave.pem" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

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

# Passo 5: Deploy via SCP
Write-Host "[STEP 5] Fazendo deploy para EC2..." -ForegroundColor Yellow
Write-Host "   Host: $EC2Host" -ForegroundColor Cyan
Write-Host "   User: $EC2User" -ForegroundColor Cyan

$scpArgs = @()
if (-not [string]::IsNullOrEmpty($KeyFile)) {
    $scpArgs += "-i", $KeyFile
    Write-Host "   Key:  $KeyFile" -ForegroundColor Cyan
}
$scpArgs += $zipFile, "${EC2User}@${EC2Host}:/tmp/"

Write-Host ""
Write-Host "   📤 Copiando arquivo..." -ForegroundColor Yellow
scp @scpArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Erro ao copiar arquivo para EC2" -ForegroundColor Red
    exit 1
}
Write-Host "   ✅ Arquivo copiado" -ForegroundColor Green
Write-Host ""

# Passo 6: Extrair e configurar no EC2
Write-Host "[STEP 6] Configurando no EC2..." -ForegroundColor Yellow

$sshArgs = @()
if (-not [string]::IsNullOrEmpty($KeyFile)) {
    $sshArgs += "-i", $KeyFile
}
$sshArgs += "${EC2User}@${EC2Host}"

$commands = @"
sudo mkdir -p /opt/aviseme-emailer
cd /opt/aviseme-emailer
sudo unzip -o /tmp/aviseme-emailer.zip
sudo chmod +x AvisemeEmailer
sudo chown -R ${EC2User}:${EC2User} /opt/aviseme-emailer
ls -la
"@

ssh @sshArgs $commands

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Erro ao configurar no EC2" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "✅ Deploy concluído com sucesso!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Próximos passos:" -ForegroundColor Yellow
Write-Host "   1. Edite o appsettings.json no EC2:" -ForegroundColor White
Write-Host "      sudo nano /opt/aviseme-emailer/appsettings.json" -ForegroundColor Cyan
Write-Host ""
Write-Host "   2. Teste a execução:" -ForegroundColor White
Write-Host "      cd /opt/aviseme-emailer && dotnet AvisemeEmailer.dll" -ForegroundColor Cyan
Write-Host ""
Write-Host "   3. Configure o cron job:" -ForegroundColor White
Write-Host "      crontab -e" -ForegroundColor Cyan
Write-Host "      0 9 * * * cd /opt/aviseme-emailer && /usr/bin/dotnet AvisemeEmailer.dll >> /var/log/aviseme-emailer.log 2>&1" -ForegroundColor Cyan
Write-Host ""

