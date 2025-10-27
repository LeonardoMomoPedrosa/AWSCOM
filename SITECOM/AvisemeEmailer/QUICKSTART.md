# 🚀 Quick Start - Aviseme Email Sender

## ⚡ Deploy Rápido (5 minutos)

### **1. Build (no Windows)**

```powershell
cd c:\OPUSGit\AWSCOM\SITECOM\AvisemeEmailer
.\build-local.ps1
```

✅ Isso cria o arquivo `aviseme-emailer.zip`

---

### **2. Copiar para EC2**

```powershell
# Com chave PEM
scp -i C:\caminho\sua-chave.pem aviseme-emailer.zip ec2-user@seu-ip:/tmp/

# Sem chave (usando senha)
scp aviseme-emailer.zip usuario@seu-ip:/tmp/
```

---

### **3. Instalar no EC2**

```bash
# Conectar no EC2
ssh -i sua-chave.pem ec2-user@seu-ip

# Instalar .NET 8 (se não tiver)
sudo dnf install dotnet-runtime-8.0 -y
# OU para Ubuntu:
# wget https://dot.net/v1/dotnet-install.sh && chmod +x dotnet-install.sh && sudo ./dotnet-install.sh --channel 8.0 --runtime dotnet

# Extrair aplicação
mkdir -p ~/AVISEME
cd ~/AVISEME
unzip -o /tmp/aviseme-emailer.zip
chmod +x AvisemeEmailer
```

---

### **4. Configurar**

```bash
nano ~/AVISEME/appsettings.json
```

Edite a connection string:

```json
{
  "ConnectionString": "Server=SEU-SERVIDOR,1433;Database=SEU-DB;User Id=USUARIO;Password=SENHA;Encrypt=true;TrustServerCertificate=true;",
  "SES": {
    "FromEmail": "aquanimal@aquanimal.com.br",
    "CcEmail": "pedrosa.leonardo@gmail.com",
    "Region": "us-east-1"
  },
  "UseSecretsManager": false,
  "SecretArn": ""
}
```

Salvar: `Ctrl+O` → `Enter` → `Ctrl+X`

---

### **5. Testar**

```bash
cd ~/AVISEME
dotnet AvisemeEmailer.dll
```

Você deve ver:
```
===========================================
=== AVISEME EMAIL SENDER ===
Iniciado em: 2025-10-27 20:00:00
===========================================

[STEP 1] Obtendo connection string...
✅ Connection string obtida

[STEP 2] Consultando banco de dados...
✅ Total de registros encontrados: X
...
```

---

### **6. Agendar Cron**

```bash
crontab -e
```

Adicionar linha (executar todo dia às 9h):

```bash
0 9 * * * cd ~/AVISEME && /usr/bin/dotnet AvisemeEmailer.dll >> ~/AVISEME/aviseme.log 2>&1
```

Salvar e sair.

---

### **7. Verificar Logs**

```bash
# Ver logs em tempo real
tail -f ~/AVISEME/aviseme.log

# Ver últimas 50 linhas
tail -50 ~/AVISEME/aviseme.log

# Ver apenas erros
grep "❌" ~/AVISEME/aviseme.log
```

---

## ✅ Pronto!

A aplicação vai rodar automaticamente todo dia às 9h e enviar emails para os clientes.

---

## 🔧 Comandos Úteis

### **Executar manualmente**
```bash
cd ~/AVISEME && dotnet AvisemeEmailer.dll
```

### **Ver cron jobs configurados**
```bash
crontab -l
```

### **Atualizar aplicação**
```bash
# No Windows: fazer build
.\build-local.ps1

# Copiar novo ZIP
scp aviseme-emailer.zip usuario@ec2:/tmp/

# No EC2: extrair
cd ~/AVISEME
unzip -o /tmp/aviseme-emailer.zip
```

---

## 📋 Checklist de Pré-requisitos

- [ ] .NET 8 Runtime instalado no EC2
- [ ] AWS CLI configurado (ou IAM Role anexada)
- [ ] Email verificado no AWS SES Console
- [ ] Permissões IAM: `ses:SendEmail`
- [ ] Connection string configurada
- [ ] IP do EC2 liberado no firewall do SQL Server

---

## 💰 Custo

**R$ 0 adicional** (além do EC2 que você já tem)

- SES: 62.000 emails/mês grátis
- EC2: Já existente
- Logs: Incluído

---

## 🆘 Problemas Comuns

### **Erro ao conectar no banco**
```bash
# Verificar se IP do EC2 está liberado
curl ifconfig.me

# Testar conexão
telnet seu-servidor.com 1433
```

### **Erro de permissão SES**
```bash
# Verificar IAM
aws sts get-caller-identity

# Ver emails verificados
aws ses list-verified-email-addresses --region us-east-1
```

### **Email não verificado**
```
Console AWS → SES → Verified identities → Create identity
```

---

Para mais detalhes, veja o **README.md** completo! 📖

