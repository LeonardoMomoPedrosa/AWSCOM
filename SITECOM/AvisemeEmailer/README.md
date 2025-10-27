# Aviseme Email Sender

Aplicação console .NET 8 para enviar emails de notificação de produtos em estoque via AWS SES.

## 📋 Funcionalidades

- ✅ Consulta banco SQL Server para produtos com estoque disponível
- ✅ Envia emails personalizados via AWS SES
- ✅ Remove registros após envio bem-sucedido
- ✅ Logs detalhados de execução
- ✅ Suporte para AWS Secrets Manager (opcional)

---

## 🚀 Deploy no EC2 Linux

### **Pré-requisitos no EC2:**

1. **.NET 8 Runtime instalado:**
```bash
# Ubuntu/Debian
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
sudo ./dotnet-install.sh --channel 8.0 --runtime dotnet

# Amazon Linux 2023
sudo dnf install dotnet-runtime-8.0 -y
```

2. **AWS CLI configurado** (com IAM Role ou credenciais):
```bash
aws configure
# Ou usar IAM Role anexada ao EC2
```

3. **Permissões IAM necessárias:**
   - `ses:SendEmail`
   - `ses:SendRawEmail`
   - `secretsmanager:GetSecretValue` (se usar Secrets Manager)

---

### **Passo 1: Build da aplicação**

No seu **computador local** (Windows):

```powershell
cd c:\OPUSGit\AWSCOM\SITECOM\AvisemeEmailer
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish
```

---

### **Passo 2: Copiar para EC2**

```powershell
# Via SCP
scp -r ./publish/* usuario@seu-ec2:/opt/aviseme-emailer/

# Ou criar ZIP e copiar
Compress-Archive -Path ./publish/* -DestinationPath aviseme-emailer.zip
scp aviseme-emailer.zip usuario@seu-ec2:/tmp/
```

No EC2:
```bash
sudo mkdir -p /opt/aviseme-emailer
cd /opt/aviseme-emailer
sudo unzip /tmp/aviseme-emailer.zip -d .
sudo chmod +x AvisemeEmailer
```

---

### **Passo 3: Configurar appsettings.json**

Edite o arquivo no EC2:

```bash
nano ~/AVISEME/appsettings.json
```

**Opção A: Connection String direta**
```json
{
  "ConnectionString": "Server=seu-servidor.com,1433;Database=seu_db;User Id=usuario;Password=senha;Encrypt=true;TrustServerCertificate=true;",
  "SES": {
    "FromEmail": "aquanimal@aquanimal.com.br",
    "CcEmail": "pedrosa.leonardo@gmail.com",
    "Region": "us-east-1"
  },
  "UseSecretsManager": false,
  "SecretArn": ""
}
```

**Opção B: Usando Secrets Manager**
```json
{
  "ConnectionString": "",
  "SES": {
    "FromEmail": "aquanimal@aquanimal.com.br",
    "CcEmail": "pedrosa.leonardo@gmail.com",
    "Region": "us-east-1"
  },
  "UseSecretsManager": true,
  "SecretArn": "arn:aws:secretsmanager:us-east-1:615283740315:secret:prod/sqlserver/ecom-QABqVU"
}
```

---

### **Passo 4: Testar execução manual**

```bash
cd ~/AVISEME
dotnet AvisemeEmailer.dll
```

Você deve ver:
```
===========================================
=== AVISEME EMAIL SENDER ===
Iniciado em: 2025-10-27 19:30:00
===========================================

[STEP 1] Obtendo connection string...
✅ Connection string obtida

[STEP 2] Consultando banco de dados...
✅ Total de registros encontrados: 5

[STEP 3] Filtrando registros com estoque disponível...
✅ Registros com estoque = 1: 2

📦 Produtos a serem notificados:
   - Produto XYZ → Cliente ABC (email@example.com)
   ...
```

---

### **Passo 5: Configurar Cron Job**

Editar crontab:
```bash
crontab -e
```

Adicionar linha para executar **todo dia às 9h da manhã**:
```bash
0 9 * * * cd ~/AVISEME && /usr/bin/dotnet AvisemeEmailer.dll >> ~/AVISEME/aviseme.log 2>&1
```

Outras opções de horário:
```bash
# A cada hora
0 * * * * cd ~/AVISEME && /usr/bin/dotnet AvisemeEmailer.dll >> ~/AVISEME/aviseme.log 2>&1

# Segunda a sexta às 9h e 15h
0 9,15 * * 1-5 cd ~/AVISEME && /usr/bin/dotnet AvisemeEmailer.dll >> ~/AVISEME/aviseme.log 2>&1

# A cada 30 minutos
*/30 * * * * cd ~/AVISEME && /usr/bin/dotnet AvisemeEmailer.dll >> ~/AVISEME/aviseme.log 2>&1
```

---

### **Passo 6: Verificar logs**

```bash
# Ver últimas execuções
tail -f ~/AVISEME/aviseme.log

# Ver apenas erros
grep "❌" ~/AVISEME/aviseme.log

# Ver resumo de emails enviados
grep "email(s) enviado(s)" ~/AVISEME/aviseme.log
```

---

## 📊 Estrutura de Logs

```
===========================================
=== AVISEME EMAIL SENDER ===
Iniciado em: 2025-10-27 19:30:00
===========================================

[STEP 1] Obtendo connection string...
✅ Connection string obtida

[STEP 2] Consultando banco de dados...
   🔌 Conectando ao banco...
   ✅ Conectado: EcomDB
   📝 Executando query...
✅ Total de registros encontrados: 10

[STEP 3] Filtrando registros com estoque disponível...
✅ Registros com estoque = 1: 3

📦 Produtos a serem notificados:
   - Ração Premium → João Silva (joao@example.com)
   - Aquário 50L → Maria Santos (maria@example.com)
   - Filtro XYZ → Pedro Costa (pedro@example.com)

[STEP 4] Enviando emails via AWS SES...
   📧 From: aquanimal@aquanimal.com.br
   📧 CC: pedrosa.leonardo@gmail.com
   📍 Region: US East (N. Virginia)
   ✅ Email enviado para joao@example.com (MessageId: 010...)
   ✅ Email enviado para maria@example.com (MessageId: 011...)
   ✅ Email enviado para pedro@example.com (MessageId: 012...)

   📊 Resumo: 3 enviados, 0 falhas
✅ 3 email(s) enviado(s) com sucesso

[STEP 5] Removendo registros do banco de dados...
   🗑️  3 registro(s) removido(s)
✅ Registros removidos do banco

===========================================
=== CONCLUÍDO COM SUCESSO ===
Finalizado em: 2025-10-27 19:30:15
===========================================
```

---

## 🔧 Troubleshooting

### **Erro: Connection timed out (SQL Server)**
```bash
# Verificar conectividade
telnet seu-servidor.com 1433

# Verificar se IP do EC2 está liberado no firewall
curl ifconfig.me
```

### **Erro: Access Denied (SES)**
```bash
# Verificar IAM Role do EC2
aws sts get-caller-identity

# Verificar se email está verificado no SES
aws ses list-verified-email-addresses --region us-east-1
```

### **Erro: Email address not verified**
```bash
# Verificar identidades verificadas no SES
aws ses list-identities --region us-east-1

# Se não estiver, adicionar:
# Console SES → Verified identities → Create identity
```

---

## 🔄 Atualização da Aplicação

Quando fizer mudanças no código:

```powershell
# No Windows
cd c:\OPUSGit\AWSCOM\SITECOM\AvisemeEmailer
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish
scp -r ./publish/* usuario@seu-ec2:/opt/aviseme-emailer/
```

No EC2 (se necessário):
```bash
# Parar processos em execução (se houver)
sudo pkill -f AvisemeEmailer

# Aplicação será usada na próxima execução do cron
```

---

## 💰 Custos

- **EC2:** Já existente (R$0 adicional)
- **AWS SES:** 
  - Primeiros 62.000 emails/mês: Grátis (se enviar de EC2)
  - Após isso: $0.10 por 1.000 emails
- **Secrets Manager:** $0.40/secret/mês + $0.05 por 10.000 chamadas

**Custo estimado mensal: ~R$2-5** (dependendo do volume)

---

## 📝 Notas

- ✅ Não precisa de NAT Gateway (R$200/mês economizados!)
- ✅ Roda no EC2 que você já tem
- ✅ Logs simples e claros
- ✅ Fácil de debugar e manter
- ✅ Pode rodar manualmente a qualquer momento

---

## 🆘 Suporte

Em caso de dúvidas ou problemas, verificar:
1. Logs: `/var/log/aviseme-emailer.log`
2. Cron logs: `/var/log/syslog` ou `/var/log/cron`
3. Permissões IAM no EC2
4. Conectividade com SQL Server
5. Emails verificados no SES Console

