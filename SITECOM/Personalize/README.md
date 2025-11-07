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
    "SafetyMarginMinutes": 60,
    "FirstRun": true,
    "LastProcessedDateFile": "last_processed_date.txt",
    "ExcludedProductIds": [1354]
  }
}
```

### Parâmetros

- **TopRecommendations**: Número de produtos recomendados por produto (padrão: 5)
- **TimeDecayHalfLifeDays**: Meia-vida para decaimento temporal em dias (padrão: 730 ≈ 2 anos)
- **SafetyMarginMinutes**: Margem de segurança em minutos para processamento incremental (padrão: 60)
- **FirstRun**: `true` para primeira execução (processa todo histórico), `false` para execuções incrementais
- **LastProcessedDateFile**: Arquivo para armazenar última data processada
- **ExcludedProductIds**: Lista de IDs de produtos que devem ser ignorados (padrão: `[1354]`)

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

### Primeira execução (processa todo histórico):

1. Configure `Personalization:FirstRun` como `true` no `appsettings.json`
2. Execute:
   ```bash
   dotnet Personalize.dll
   ```

### Execuções semanais (delta):

1. Configure `Personalization:FirstRun` como `false` no `appsettings.json`
2. Configure crontab (exemplo: toda segunda-feira às 2h):
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

- O sistema processa apenas compras com `status > 0`
- Recomendações são atualizadas (upsert) no DynamoDB
- O arquivo de estado (`last_processed_date.txt`) armazena a última data processada para execuções incrementais

