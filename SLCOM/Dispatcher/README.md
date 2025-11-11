# Dispatcher - Sistema de Envio de Emails

Sistema Python que roda a cada 5 minutos e verifica se existe email para enviar usando a tabela `TRANSACTION_LOG`.

## Variáveis de Ambiente

O sistema utiliza as seguintes variáveis de ambiente. Configure-as no servidor antes de executar:

### Banco de Dados SQL Server

- `DB_SERVER` - Servidor do banco de dados (ex: `aadbcloud.cu9zlyfmg2ii.us-east-1.rds.amazonaws.com`)
- `DB_DATABASE` - Nome do banco de dados (ex: `SL4AAProd`)
- `DB_UID` - Usuário do banco de dados (ex: `Admin`)
- `DB_PWD` - Senha do banco de dados
- `DB_PORT` - Porta do banco de dados (padrão: `1433`)

### AWS SES (Simple Email Service)

- `AWS_REGION` - Região AWS para SES (padrão: `us-east-1`)
- `SES_FROM_EMAIL` - Email remetente (padrão: `aquanimal@aquanimal.com.br`)
- `SES_CC_EMAIL` - Email para cópia quando necessário (padrão: `aquanimal@aquanimal.com.br`)
- `SES_BCC_EMAIL` - Email para cópia oculta (padrão: `pedrosa.leonardo@gmail.com`)

## Configuração no Servidor

### 1. Configurar variáveis de ambiente

Crie um arquivo `.env` ou configure as variáveis no sistema:

```bash
export DB_SERVER="seu-servidor.rds.amazonaws.com"
export DB_DATABASE="SL4AAProd"
export DB_UID="Admin"
export DB_PWD="sua-senha"
export DB_PORT="1433"
export AWS_REGION="us-east-1"
export SES_FROM_EMAIL="aquanimal@aquanimal.com.br"
export SES_CC_EMAIL="aquanimal@aquanimal.com.br"
export SES_BCC_EMAIL="pedrosa.leonardo@gmail.com"
```

### 2. Instalar dependências Python

```bash
pip3 install pyodbc boto3
```

### 3. Configurar ODBC Driver

Certifique-se de que o ODBC Driver 17 for SQL Server está instalado no servidor.

### 4. Executar manualmente

```bash
cd ~/Dispatcher2
python3 EmailJob.py
```

### 5. Configurar no Cron (executar a cada 5 minutos)

```bash
crontab -e
```

Adicione a linha:

```
*/5 * * * * cd ~/Dispatcher2 && /usr/bin/python3 EmailJob.py >> ~/Dispatcher2/dispatcher.log 2>&1
```

## Estrutura do Projeto

- `EmailJob.py` - Script principal que processa emails da TRANSACTION_LOG
- `LionDispatcher.py` - Script que adiciona novos registros na TRANSACTION_LOG
- `modules/Constants.py` - Constantes do sistema
- `modules/DataTypes.py` - Tipos de dados utilizados

## Tipos de Email Processados

1. **RECEIPT_EMAIL** (TRX_CODE=1) - Nota Fiscal gerada no LION
2. **SITE_0_EMAIL** (TRX_CODE=5) - Novo pedido criado no site
3. **SITE_V_EMAIL** (TRX_CODE=2) - Pedido enviado
4. **SITE_R_EMAIL** (TRX_CODE=3) - Pedido pronto para retirada
5. **SITE_N_EMAIL** (TRX_CODE=4) - Cartão não autorizado
6. **SITE_6_EMAIL** (TRX_CODE=6) - Reset de senha

## Deploy

O deploy é feito automaticamente via GitHub Actions quando há push na branch `main` ou `develop` que afeta arquivos em `SLCOM/Dispatcher/**`.

O workflow:
1. Prepara os arquivos
2. Compacta em um ZIP
3. Transfere para o Bastion
4. Descompacta em `~/Dispatcher2`

