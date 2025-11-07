# Documento Executivo - Sistema de Personalização de Produtos

## 1. Visão Geral

O sistema de personalização de produtos é uma solução desenvolvida em C# (.NET 8.0) que gera recomendações de produtos para clientes de e-commerce baseado no histórico de compras. O sistema utiliza a técnica **SIMS (Similar Items)** com foco em **co-compra (co-purchase)**, combinando múltiplas métricas estatísticas e decaimento temporal para identificar produtos frequentemente comprados juntos.

### 1.1 Objetivo

Gerar listas personalizadas de recomendações de produtos (top 5) para cada produto do catálogo, permitindo que o e-commerce apresente sugestões relevantes aos clientes durante a navegação e no processo de compra.

### 1.2 Características Principais

- **Snapshot incremental**: Toda execução reconstrói as recomendações com dados completos, difere do snapshot anterior e atualiza o DynamoDB apenas quando há mudança
- **Execução agendada**: Roda semanalmente via crontab
- **Armazenamento escalável**: Utiliza AWS DynamoDB para armazenar recomendações
- **Algoritmo robusto**: Combina múltiplas métricas estatísticas para maior precisão

---

## 2. Arquitetura do Sistema

### 2.1 Componentes

```
┌─────────────────────────────────────────────────────────────┐
│                    Personalize Job                          │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐ │
│  │   Program    │───▶│   Services   │───▶│    Models    │ │
│  │   (Main)     │    │              │    │              │ │
│  └──────────────┘    └──────────────┘    └──────────────┘ │
│         │                    │                    │         │
│         │                    │                    │         │
│         ▼                    ▼                    ▼         │
│  ┌──────────────────────────────────────────────────────┐  │
│  │          Fluxo de Processamento                      │  │
│  │  1. Buscar dados do SQL Server                       │  │
│  │  2. Calcular recomendações (SIMS)                    │  │
│  │  3. Comparar snapshot e atualizar DynamoDB           │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ SQL Server   │    │ DynamoDB     │    │ File System  │
│ (tbCompra,   │    │ (dynamo-     │    │ (Snapshot de │
│  tbProdutos) │    │  personalize)│    │  recomendações)│
└──────────────┘    └──────────────┘    └──────────────┘
```

### 2.2 Serviços

1. **SqlServerService**: Extrai dados de compras do banco de dados
2. **PersonalizationService**: Implementa o algoritmo SIMS de recomendação
3. **DynamoDBService**: Armazena, atualiza e remove recomendações no DynamoDB
4. **EmailService**: Envia relatórios de execução via Amazon SES

---

## 3. Algoritmo de Recomendação - SIMS (Co-Purchase)

### 3.1 Conceito

O algoritmo **SIMS (Similar Items)** baseado em **co-compra** identifica produtos que são frequentemente comprados juntos. A ideia central é: *"Se clientes compram o produto A e o produto B juntos com frequência, então recomende B quando um cliente visualiza A"*.

### 3.2 Processo de Cálculo

#### Passo 1: Construção da Matriz de Co-Compras

Para cada compra no histórico:

1. Identifica todos os produtos únicos na compra
2. Para cada par de produtos (A, B) na mesma compra:
   - Incrementa o contador de co-compra entre A e B
   - Incrementa o contador de co-compra entre B e A (matriz simétrica)
3. Aplica **decaimento temporal** ao peso da co-compra

**Exemplo:**
```
Compra 1: [Produto A, Produto B, Produto C]
  → Co-compras: (A,B), (A,C), (B,C)

Compra 2: [Produto A, Produto B]
  → Co-compras: (A,B)

Matriz resultante:
      A    B    C
  A   0    2    1
  B   2    0    1
  C   1    1    0
```

#### Passo 2: Cálculo de Estatísticas de Produtos

Para cada produto, calcula:
- **Frequência**: Número total de vezes que o produto foi comprado (com decaimento temporal)
- Utilizado para normalizar os escores

#### Passo 3: Cálculo de Múltiplos Escores

Para cada par de produtos (A, B), calcula três escores:

##### 3.3.1 Score COUNT

**Definição**: Número absoluto de co-compras entre produtos A e B.

**Fórmula**:
```
COUNT(A,B) = número de vezes que A e B foram comprados juntos
```

**Características**:
- Valor absoluto (não normalizado)
- Favorece produtos muito populares
- Inclui decaimento temporal

##### 3.3.2 Score LIFT

**Definição**: Mede a força da associação entre dois produtos, comparando a frequência observada de co-compra com a frequência esperada se os produtos fossem independentes.

**Fórmula**:
```
LIFT(A,B) = P(A e B) / (P(A) × P(B))

Onde:
- P(A e B) = probabilidade de A e B serem comprados juntos
- P(A) = probabilidade de A ser comprado
- P(B) = probabilidade de B ser comprado

Implementação:
LIFT(A,B) = (coPurchaseCount × totalPurchases) / (freqA × freqB)
```

**Interpretação**:
- **LIFT = 1**: Produtos são independentes (sem associação)
- **LIFT > 1**: Associação positiva (produtos são comprados juntos mais que o esperado)
- **LIFT < 1**: Associação negativa (produtos raramente são comprados juntos)

**Exemplo**:
- Produto A comprado em 100 compras
- Produto B comprado em 50 compras
- A e B comprados juntos em 20 compras
- Total de compras: 1000

```
LIFT = (20 × 1000) / (100 × 50) = 20000 / 5000 = 4.0
```

Significa que A e B são comprados juntos **4 vezes mais** que o esperado se fossem independentes.

##### 3.3.3 Score COSINE

**Definição**: Similaridade cosseno entre produtos, medindo o quão "similar" é o padrão de compra entre dois produtos.

**Fórmula**:
```
COSINE(A,B) = coPurchaseCount / sqrt(freqA × freqB)
```

**Características**:
- Normalizado entre 0 e 1
- Mede similaridade de padrões de compra
- Independente da popularidade absoluta dos produtos

**Interpretação**:
- **COSINE = 1**: Produtos sempre comprados juntos
- **COSINE = 0**: Produtos nunca comprados juntos
- Valores intermediários indicam grau de associação

#### Passo 4: Normalização e Combinação de Escores

Como os escores têm escalas diferentes, são normalizados para o intervalo [0,1] antes da combinação:

```
normalizedScore = score / maxScore
```

**Combinação Final** (média ponderada):
```
COMBINED_SCORE = 0.3 × normalizedCOUNT + 0.4 × normalizedLIFT + 0.3 × normalizedCOSINE
```

**Pesos escolhidos**:
- **30% COUNT**: Considera popularidade absoluta
- **40% LIFT**: Prioriza associações estatisticamente significativas (peso maior)
- **30% COSINE**: Considera similaridade de padrões

#### Passo 5: Seleção dos Top Produtos

Para cada produto, seleciona os **5 produtos** com maior `COMBINED_SCORE` como recomendações.

---

## 4. Decaimento Temporal

### 4.1 Conceito

Produtos comprados recentemente têm maior relevância no cálculo de recomendações, refletindo tendências atuais do mercado e mudanças de comportamento dos clientes.

### 4.2 Implementação

Utiliza **decaimento exponencial** com meia-vida configurável (padrão: 730 dias ≈ 2 anos).

**Fórmula**:
```
weight = 2^(-daysSincePurchase / halfLifeDays)
```

**Onde**:
- `daysSincePurchase`: Dias desde a compra até a data de referência
- `halfLifeDays`: Meia-vida em dias (padrão: 730, mas o exemplo abaixo usa 30 para facilitar a visualização)

### 4.3 Exemplos

Com meia-vida de 30 dias:

| Dias desde a compra | Peso |
|---------------------|------|
| 0 dias (hoje)       | 1.00 |
| 15 dias             | 0.71 |
| 30 dias (meia-vida) | 0.50 |
| 60 dias             | 0.25 |
| 90 dias             | 0.13 |

**Efeito prático**:
- Compra de 1 dia atrás: peso = 0.98 (quase total)
- Compra de 30 dias atrás: peso = 0.50 (metade)
- Compra de 1 ano atrás: peso ≈ 0.00 (praticamente ignorada)

### 4.4 Aplicação

O decaimento temporal é aplicado em:
1. **Matriz de co-compras**: Cada co-compra é ponderada pelo tempo
2. **Estatísticas de produtos**: Frequência de produtos é ponderada pelo tempo

---

## 5. Fluxo de Processamento

### Visão Geral

```
┌─────────────────────────────────────────────────────────┐
│ 1. Inicialização                                        │
│    - Carrega config (snapshot, e-mail, exclusões)       │
│    - Conecta ao SQL Server, DynamoDB e SES              │
└─────────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────┐
│ 2. Coleta de Dados                                      │
│    - Consulta FULL no SQL Server (status = 'V')         │
│    - Aplica exclusões de produtos                       │
└─────────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────┐
│ 3. Processamento SIMS                                   │
│    - Calcula co-compras com decaimento temporal         │
│    - Gera top N recomendações por produto               │
└─────────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────┐
│ 4. Snapshot                                             │
│    - Carrega snapshot anterior (arquivo texto)          │
│    - Gera snapshot atual ordenado                       │
│    - Compara linha a linha                              │
└─────────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────┐
│ 5. Sincronização                                        │
│    - Atualiza/Remove do DynamoDB somente os produtos    │
│      cujas recomendações mudaram                       │
│    - Persistência atômica do novo snapshot              │
└─────────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────┐
│ 6. Observabilidade                                      │
│    - Registra tempos por etapa                          │
│    - Envia relatório por e-mail (SES)                   │
└─────────────────────────────────────────────────────────┘
```

### Snapshot Incremental

- O snapshot é um arquivo texto (`productId:ID1;ID2;...`) com IDs ordenados.
- Toda execução gera o snapshot completo e o compara com o anterior.
- Somente os produtos com linhas diferentes (incluindo remoções) são sincronizados no DynamoDB.
- A escrita do snapshot é atômica (`arquivo.tmp` → `File.Move(..., overwrite: true)`), evitando arquivos corrompidos.
- O relatório final inclui tempos por etapa, produtos alterados, upserts, remoções e o caminho do snapshot utilizado.

---

## 6. Estrutura de Dados

### 6.1 DynamoDB - Tabela: dynamo-personalize

**Chave Primária**:
- `productId` (String): ID do produto

**Atributos**:
- `data` (String): JSON com array de produtos recomendados
- `lastUpdated` (String): Data da última atualização (ISO 8601)

**Exemplo de Registro**:
```json
{
  "productId": "12345",
  "data": "[{\"ProductId\":67890,\"Score\":0.85,\"ScoreType\":\"combined\"},{\"ProductId\":11111,\"Score\":0.72,\"ScoreType\":\"combined\"},{\"ProductId\":22222,\"Score\":0.68,\"ScoreType\":\"combined\"},{\"ProductId\":33333,\"Score\":0.65,\"ScoreType\":\"combined\"},{\"ProductId\":44444,\"Score\":0.61,\"ScoreType\":\"combined\"}]",
  "lastUpdated": "2024-01-15T10:30:00Z"
}
```

## 7. Configurações

### 7.1 Parâmetros Principais

| Parâmetro | Descrição | Valor Padrão |
|-----------|-----------|--------------|
| `TopRecommendations` | Número de produtos recomendados por produto | 5 |
| `TimeDecayHalfLifeDays` | Meia-vida para decaimento temporal (dias) | 730 |
| `ExcludedProductIds` | Lista de IDs a serem ignorados durante o processamento | [1354] |
| `SnapshotFilePath` | Caminho do arquivo de snapshot das recomendações (texto) | personalize_snapshot.txt |
| `ReportEmail` | Endereço para envio do relatório de execução | pedrosa.leonardo@gmail.com |

### 7.2 Ajustes Recomendados

**Para maior precisão**:
- Aumentar `TopRecommendations` para 10
- Reduzir `TimeDecayHalfLifeDays` para valores menores (ex.: 90-180 dias) para reagir mais rápido a tendências

**Para maior performance**:
- Reduzir `TopRecommendations` para 3
- Aumentar `TimeDecayHalfLifeDays` para valores ainda maiores (ex.: > 730) quando mudanças são muito raras

**Para atualizações mais frequentes**:
- Executar o job com maior frequência (ex.: diariamente) para capturar novas co-compras rapidamente

---

## 8. Métricas e Performance

### 8.1 Complexidade Computacional

- **Tempo**: O(n × m²), onde:
  - n = número de compras
  - m = número médio de produtos por compra
- **Espaço**: O(p²), onde p = número de produtos únicos

### 8.2 Otimizações Implementadas

1. **Snapshot incremental**: compara o resultado completo com o snapshot anterior e atualiza apenas o que mudou no DynamoDB
2. **Matriz esparsa**: apenas produtos co-comprados são armazenados durante o cálculo
3. **Normalização eficiente**: normalização dos escores feita em uma única passada

### 8.3 Escalabilidade

- **DynamoDB**: Suporta milhões de produtos
- **Processamento**: Pode ser distribuído se necessário
- **Snapshot diff**: Upserts mínimos reduzem custo de escrita no DynamoDB

---

## 9. Casos de Uso

### 9.1 Recomendação na Página de Produto

**Cenário**: Cliente visualiza produto A

**Ação**: Buscar recomendações do produto A no DynamoDB e exibir top 5 produtos recomendados.

### 9.2 Recomendação no Carrinho

**Cenário**: Cliente adiciona produto A ao carrinho

**Ação**: Buscar recomendações do produto A e exibir "Frequentemente comprado junto".

### 9.3 Recomendação Pós-Compra

**Cenário**: Cliente finaliza compra com produto A

**Ação**: Exibir recomendações do produto A na página de confirmação.

---

## 10. Limitações e Considerações

### 10.1 Limitações Conhecidas

1. **Cold Start**: Produtos novos não têm recomendações até serem comprados algumas vezes
2. **Sazonalidade**: Mudanças sazonais podem não ser capturadas rapidamente
3. **Produtos nicho**: Produtos pouco comprados podem ter recomendações menos precisas

### 10.2 Melhorias Futuras

1. **Filtragem colaborativa**: Incorporar preferências de clientes similares
2. **Conteúdo do produto**: Combinar com características do produto (categoria, marca, etc.)
3. **Machine Learning**: Utilizar modelos de aprendizado de máquina para maior precisão
4. **A/B Testing**: Testar diferentes pesos de escores e configurações

---

## 11. Conclusão

O sistema de personalização implementado utiliza técnicas comprovadas de recomendação (SIMS co-purchase) combinadas com múltiplas métricas estatísticas e decaimento temporal para gerar recomendações precisas e relevantes. A arquitetura é escalável, eficiente e permite processamento incremental para manter as recomendações atualizadas com baixo custo computacional.

O algoritmo balanceia popularidade (COUNT), associação estatística (LIFT) e similaridade de padrões (COSINE) para fornecer recomendações que são tanto relevantes quanto estatisticamente significativas, enquanto o decaimento temporal garante que tendências recentes tenham maior impacto nas recomendações.

---

## 12. Referências

- **SIMS (Similar Items)**: Técnica de recomendação baseada em similaridade de itens
- **Lift**: Medida de associação entre variáveis em análise de dados
- **Cosine Similarity**: Medida de similaridade vetorial amplamente utilizada em sistemas de recomendação
- **Temporal Decay**: Técnica de ponderação temporal para dados temporais

---

**Versão do Documento**: 1.0  
**Data**: Janeiro 2024  
**Autor**: Sistema de Personalização - E-commerce

