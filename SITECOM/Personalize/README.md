# Personalize - Sistema de Recomendação de Produtos

Sistema de personalização para produtos no e-commerce usando técnica SIMS (co-purchase) com escores e decaimento temporal.

## Descrição

Este projeto é um executável em C# que roda semanalmente via crontab para gerar listas de recomendações de produtos para clientes baseado em histórico de compras.

## Funcionalidades

- **Extração de dados**: Busca dados de vendas das tabelas `tbCompra` e `tbProdutosCompra`
- **Processamento incremental**: Na primeira execução processa todo o histórico, nas seguintes apenas o delta (últimos 7 dias)
- **Algoritmo SIMS**: Co-purchase (co-compra) para identificar produtos frequentemente comprados juntos
- **Múltiplos escores**: Count, Lift e Cosine com combinação ponderada
- **Decaimento temporal**: Produtos comprados recentemente têm maior peso (meia-vida de 30 dias)
- **Armazenamento**: Salva recomendações no DynamoDB (tabela `dynamo-personalize`)

## Estrutura do Projeto

```
Personalize/
├── Models/
│   ├── Purchase.cs              # Modelo de compra
│   ├── ProductPurchase.cs       # Modelo de produto comprado
│   └── RecommendationRecord.cs  # Modelo de recomendação
├── Services/
│   ├── SqlServerService.cs      # Serviço de acesso ao SQL Server
│   ├── DynamoDBService.cs       # Serviço de acesso ao DynamoDB
│   ├── PersonalizationService.cs # Lógica de recomendação (SIMS)
│   └── ExecutionStateService.cs  # Controle de estado de execução
├── Program.cs                   # Ponto de entrada
├── appsettings.json            # Configurações
└── Personalize.csproj          # Projeto .NET
```

## Configuração

### appsettings.json

```json
{
  "ConnectionString": "",
  "UseSecretsManager": true,
  "SecretArn": "arn:aws:secretsmanager:us-east-1:615283740315:secret:prod/sqlserver/ecom-QABqVU",
  "DynamoDB": {
    "TableName": "dynamo-personalize",
    "Region": "us-east-1"
  },
  "Personalization": {
    "TopRecommendations": 5,
    "TimeDecayHalfLifeDays": 730,
    "ExcludedProductIds": [1354],
    "SnapshotFilePath": "personalize_snapshot.txt",
    "ReportEmail": "pedrosa.leonardo@gmail.com"
  }
}
```

### Parâmetros

- **TopRecommendations**: Número de produtos recomendados por produto (padrão: 5)
- **TimeDecayHalfLifeDays**: Meia-vida para decaimento temporal em dias (padrão: 730 ≈ 2 anos)
- **ExcludedProductIds**: Lista de IDs de produtos que devem ser ignorados (padrão: `[1354]`)
- **SnapshotFilePath**: Caminho do arquivo texto que armazena o snapshot das recomendações
- **ReportEmail**: E-mail que receberá o relatório de execução
- **SES:FromEmail / SES:Region** (opcionais): remetente e região usados pelo Amazon SES para envio do relatório. Se não informados, o job tenta usar `ReportEmail` como remetente e a mesma região do DynamoDB.

### Snapshot e Relatório

- A cada execução o job calcula todas as recomendações (histórico completo), gera um snapshot ordenado (`productId:ID1;ID2;...`) e o salva em `SnapshotFilePath`.
- O snapshot anterior é carregado e comparado com o atual; apenas os produtos cujas linhas mudaram são sincronizados com o DynamoDB.
- O snapshot é gravado de forma atômica (`arquivo.tmp` → rename), evitando arquivos corrompidos em caso de falha.
- Um relatório com tempos por etapa e estatísticas (produtos alterados, upserts, remoções) é escrito no log e enviado por e-mail para `ReportEmail` (via Amazon SES), quando configurado.

## Algoritmo de Recomendação

### SIMS (Co-Purchase)

O algoritmo identifica produtos que são frequentemente comprados juntos e calcula escores:

1. **Count Score**: Número de co-compras (com decaimento temporal)
2. **Lift Score**: Mede a força da associação entre produtos
3. **Cosine Score**: Similaridade cosseno entre produtos

Os escores são combinados com pesos:
- 30% Count
- 40% Lift
- 30% Cosine

### Decaimento Temporal

Produtos comprados recentemente têm maior peso no cálculo:

```
weight = 2^(-daysSincePurchase / halfLifeDays)
```

## Estrutura do DynamoDB

### Tabela: dynamo-personalize

- **Chave Primária**: `productId` (String)
- **Atributos**:
  - `data`: JSON com array de produtos recomendados (top 5)
  - `lastUpdated`: Data da última atualização

### Exemplo de registro:

```json
{
  "productId": "123",
  "data": "[{\"ProductId\":456,\"Score\":0.85,\"ScoreType\":\"combined\"},...]",
  "lastUpdated": "2024-01-15T10:30:00Z"
}
```

## Execução

### Execução manual

```bash
dotnet Personalize.dll
```

O job gera/atualiza o snapshot (`SnapshotFilePath`) e sincroniza apenas os produtos cujas recomendações mudaram. Ao final, registra os tempos por etapa no log e envia o relatório por e-mail (quando configurado).

### Execução agendada

Configure o crontab (exemplo: toda segunda-feira às 2h):
   ```bash
   0 2 * * 1 cd ~/Personalize && ~/.dotnet/dotnet Personalize.dll >> ~/Personalize/personalize.log 2>&1
   ```

## Dependências

- .NET 8.0
- Microsoft.Data.SqlClient
- AWSSDK.DynamoDBv2
- AWSSDK.SecretsManager
- Microsoft.Extensions.Configuration

## Tabelas do Banco de Dados

### tbCompra
- PKId
- PKIdUsuario
- status
- idDados
- data
- dataMdSt

### tbProdutosCompra
- idUsuario
- idProduto
- quantidade
- PKId
- PKIdCompra
- preco
- nome
- sys_creation_date
- sys_update_date

## Logs

O sistema gera logs detalhados sobre:
- Número de compras processadas
- Número de produtos únicos
- Número de recomendações calculadas
- Estatísticas de salvamento/atualização no DynamoDB

## Notas

- O sistema processa apenas compras com `status = 'V'`.
- Recomendações são sincronizadas seletivamente: somente produtos com mudanças entre snapshots sofrem upsert/remoção no DynamoDB.
- O snapshot (`SnapshotFilePath`) é reescrito a cada execução e serve como base para detectar alterações futuras.

