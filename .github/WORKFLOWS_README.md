# 🚀 GitHub Actions Workflows

## 📋 Workflows Disponíveis

### **build.yml** - AvisemeEmailer
Build e deploy automático do executável AvisemeEmailer para o Bastion (EC2 Linux).

---

## 🔐 Secrets Necessários

Configure em: **Settings → Secrets and variables → Actions → New repository secret**

### **AWS Credentials:**
```
AWS_ACCESS_KEY_ID       = AKIA...
AWS_SECRET_ACCESS_KEY   = secret...
AWS_REGION              = us-east-1
BASTION_SG_ID           = sg-xxxxx (ID do Security Group do Bastion)
```

### **Bastion SSH:**
```
BASTION_HOST            = 54.123.45.67 (IP ou hostname do Bastion)
BASTION_USERNAME        = ec2-user
BASTION_SSH_KEY         = -----BEGIN RSA PRIVATE KEY-----
                          (conteúdo completo da chave privada)
                          -----END RSA PRIVATE KEY-----
```

---

## 🔄 Como Funciona

### **Workflow: build.yml**

```
1. Pre-actions          → Inicializa pipeline
2. Open SSH Port        → Abre porta 22 no Security Group (temporário)
3. Build                → Compila .NET 8 para linux-x64
4. Transfer Bastion     → Envia ZIP via SCP
5. Deploy Bastion       → Descompacta em /opt/aviseme-emailer
6. Close SSH Port       → Fecha porta 22 (segurança)
7. Post-actions         → Resumo do pipeline
```

---

## ⚡ Triggers

O workflow executa automaticamente quando:

✅ **Push** para `main` ou `develop` (com mudanças em `SITECOM/AvisemeEmailer/**`)
✅ **Pull Request** para `main`
✅ **Manual** via "Run workflow" no GitHub

---

## 📂 Estrutura de Deploy

```
Bastion (EC2 Linux):
└── ~/AVISEME/
    ├── AvisemeEmailer          # Executável
    ├── AvisemeEmailer.dll      # DLL principal
    ├── appsettings.json        # Configuração (preservada no deploy)
    ├── BUILD_INFO.txt          # Informações do build
    └── ... (outras DLLs)
```

---

## 🎯 Execução Manual no Bastion

### **Conectar no Bastion:**
```bash
ssh -i sua-chave.pem ec2-user@bastion-ip
```

### **Executar aplicação:**
```bash
cd ~/AVISEME
dotnet AvisemeEmailer.dll
```

### **Ver logs:**
```bash
tail -f ~/AVISEME/aviseme.log
```

---

## ⏰ Agendar no Cron

```bash
crontab -e
```

Adicionar linha (todo dia às 9h):
```bash
0 9 * * * cd ~/AVISEME && /usr/bin/dotnet AvisemeEmailer.dll >> ~/AVISEME/aviseme.log 2>&1
```

---

## 🔧 Configuração no Bastion

### **Primeira execução - Editar appsettings.json:**
```bash
nano ~/AVISEME/appsettings.json
```

Configurar:
```json
{
  "ConnectionString": "Server=...",
  "SES": {
    "FromEmail": "aquanimal@aquanimal.com.br",
    "CcEmail": "pedrosa.leonardo@gmail.com",
    "Region": "us-east-1"
  },
  "UseSecretsManager": false
}
```

⚠️ **Importante:** O `appsettings.json` é **preservado** entre deploys!

---

## 🐛 Troubleshooting

### **Erro: .NET Runtime não encontrado**
```bash
sudo dnf install dotnet-runtime-8.0 -y
# OU para Ubuntu:
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
sudo ./dotnet-install.sh --channel 8.0 --runtime dotnet
```

### **Erro: Porta SSH já aberta**
Normal. O workflow tenta abrir e ignora se já estiver aberta.

### **Erro: Permission denied**
```bash
chmod +x ~/AVISEME/AvisemeEmailer
```

### **Ver logs do workflow:**
- GitHub → Actions → Selecionar execução → Ver job

---

## 📊 Artifacts

Cada build gera um artifact no GitHub:
- **Nome:** AvisemeEmailer
- **Conteúdo:** aviseme-emailer.zip
- **Retenção:** 30 dias

Para baixar:
1. GitHub → Actions → Selecionar build
2. Seção "Artifacts"
3. Download "AvisemeEmailer"

---

## 🔄 Atualização

Para atualizar a aplicação:

1. **Faça commit** das mudanças
2. **Push** para `main` ou `develop`
3. **Aguarde** o workflow terminar (~3-5 min)
4. **Pronto!** Nova versão em `~/AVISEME`

---

## 🆘 Suporte

Em caso de problemas:

1. Verificar logs do GitHub Actions
2. Conectar no Bastion e verificar:
   - `~/AVISEME` existe?
   - `.NET Runtime` instalado?
   - `appsettings.json` configurado?
3. Executar manualmente para debug:
   ```bash
   cd ~/AVISEME
   dotnet AvisemeEmailer.dll
   ```

---

## 📝 Notas

- ✅ Porta SSH é aberta/fechada automaticamente (segurança)
- ✅ `appsettings.json` é preservado entre deploys
- ✅ Backup automático da versão anterior
- ✅ Execução manual (não é Windows Service)
- ✅ Build otimizado para Linux (linux-x64)

