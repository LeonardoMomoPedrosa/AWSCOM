# 📋 INSTRUÇÕES DE DEPLOY MANUAL - Lambda Aviseme

## 🎯 Objetivo
Este guia explica como fazer o deploy manual da função Lambda Aviseme no AWS Lambda usando apenas o AWS Console.

---

## 🛠️ PASSO A PASSO - AWS CONSOLE

### **PASSO 1: Criar a Função Lambda**

1. Acesse o AWS Console:
   - URL: https://console.aws.amazon.com/lambda/
   - Faça login na sua conta AWS

2. Criar nova função:
   - Clique em **"Create function"**
   - Selecione **"Author from scratch"**
   - **Function name**: `aviseme`
   - **Runtime**: `.NET 8`
   - **Architecture**: `x86_64`
   - Clique em **"Create function"**

---

### **PASSO 2: Configurar Handler e Runtime**

1. Na função criada, vá para a aba **"Code"**
2. Role para baixo até **"Runtime settings"**
3. Clique em **"Edit"**
4. Configure:
   - **Handler**: `LambdaAviseme::LambdaAviseme.Function::FunctionHandler`
   - **Memory**: `512 MB`
   - **Timeout**: `15 minutes`
5. Clique em **"Save"**

---

### **PASSO 2.1: Configurar VPC (IMPORTANTE para conectar ao banco de dados)**

1. Na aba **"Configuration"** → **"VPC"**
2. Clique em **"Edit"**
3. Configure a VPC:
   - **VPC**: Selecione a VPC onde seu SQL Server está
   - **Subnets**: Selecione pelo menos 2 subnets privadas
   - **Security groups**: Selecione um security group com:
     - Regra de saída para porta 1433 (SQL Server)
     - Regra de saída para AWS Secrets Manager
4. Clique em **"Save"**
5. ⚠️ **ATENÇÃO**: A Lambda precisa ser colocada na mesma VPC do banco de dados para acessá-lo!

---

### **PASSO 3: Fazer Upload do Código**

1. Na aba **"Code"**
2. Clique em **"Upload from"** → **".zip file"**
3. Selecione o arquivo `lambda-aviseme.zip`
4. Clique em **"Save"**
5. Aguarde o upload completar

---

### **PASSO 4: Configurar Variáveis de Ambiente**

1. Vá para a aba **"Configuration"**
2. Clique em **"Environment variables"** → **"Edit"**
3. Adicione as variáveis:
   - **Key**: `SECRET_ARN`
     **Value**: `arn:aws:secretsmanager:us-east-1:615283740315:secret:prod/sqlserver/ecom-QABqVU`
4. Clique em **"Save"**

---

### **PASSO 5: Configurar Permissões IAM**

1. Na aba **"Configuration"** → **"Permissions"**
2. Clique no link da **Execution role**
3. No IAM Console, clique em **"Add permissions"** → **"Attach policies"**
4. Procure e adicione as políticas:
   - `AWSLambdaBasicExecutionRole`
   - `SecretsManagerReadWrite` (para acessar Secrets Manager)
5. Se a Lambda estiver em VPC, adicione também:
   - `AWSLambdaVPCAccessExecutionRole` (para permitir acesso VPC)
6. Volte para a função Lambda

---

### **PASSO 5.1: Configurar Security Group (IMPORTANTE)**

O Security Group da Lambda deve ter regras de saída para:

1. **SQL Server (RDS ou EC2)**:
   - **Type**: Custom TCP
   - **Port**: 1433 (ou a porta do seu SQL Server)
   - **Destination**: Security Group do banco de dados OU 0.0.0.0/0

2. **AWS Secrets Manager**:
   - **Type**: HTTPS
   - **Port**: 443
   - **Destination**: 0.0.0.0/0

3. **DNS**:
   - **Type**: DNS (UDP)
   - **Port**: 53
   - **Destination**: 0.0.0.0/0

---

### **PASSO 6: Testar a Função**

1. Na aba **"Test"**
2. Clique em **"Test"** (mantenha o payload padrão `{}`)
3. Aguarde a execução
4. Verifique o resultado na aba **"Response"**
5. Verifique os logs na aba **"Logs"**

---

### **PASSO 7: Configurar Agendamento (EventBridge)**

1. Vá para: https://console.aws.amazon.com/events/
2. Clique em **"Rules"** → **"Create rule"**
3. Configure:
   - **Name**: `aviseme-schedule`
   - **Description**: `Executa Lambda a cada hora das 8h às 23h`
   - **Schedule**: `cron(0 8-23 * * ? *)`
4. Clique em **"Next"**
5. **Select target**:
   - **Target type**: `AWS service`
   - **Service**: `Lambda function`
   - **Function**: `aviseme`
6. Clique em **"Next"** → **"Create rule"**

---

## 🚨 CONFIGURAÇÃO DE FIREWALL DO BANCO DE DADOS

### **Como ver o Security Group da Lambda:**

1. Acesse a função Lambda no console
2. Aba **"Configuration"** → **"VPC"**
3. Você verá o **"VPC configuration"** com:
   - VPC ID
   - Subnets IDs
   - **Security groups IDs** ← Este é o Security Group da Lambda!
4. Clique nos IDs do Security Group para abrir no console EC2

### **Se o banco é SQL Server no RDS:**

1. Acesse o RDS Console
2. Selecione seu banco de dados
3. Clique em **"Connectivity & security"** → **"VPC security groups"**
4. Clique no Security Group
5. Na aba **"Inbound rules"**, adicione:
   - **Type**: MSSQL
   - **Port**: 1433 (ou a porta do seu banco)
   - **Source**: Security Group ID da Lambda (copiado no passo anterior)
6. Salve as regras

### **Se o banco é SQL Server no EC2:**

1. Configure o Windows Firewall para permitir a porta 1433
2. Configure o Security Group do EC2 para permitir tráfego da Lambda
3. Verifique se o SQL Server está configurado para aceitar conexões remotas

---

## 🧪 VERIFICAR EXECUÇÃO

### **CloudWatch Logs:**
- URL: https://console.aws.amazon.com/cloudwatch/home?region=us-east-1#logsV2:log-groups/log-group/$252Faws$252Flambda$252Faviseme

### **Visualizar Logs:**
1. Acesse a função Lambda
2. Aba **"Monitor"** → **"View CloudWatch logs"**
3. Verifique os logs de execução

---

## 🔗 LINKS ÚTEIS

- **Lambda Console**: https://console.aws.amazon.com/lambda/
- **IAM Console**: https://console.aws.amazon.com/iam/
- **EventBridge Console**: https://console.aws.amazon.com/events/
- **CloudWatch Logs**: https://console.aws.amazon.com/cloudwatch/

---

**🎉 Pronto! Sua função Lambda está configurada e funcionando!**