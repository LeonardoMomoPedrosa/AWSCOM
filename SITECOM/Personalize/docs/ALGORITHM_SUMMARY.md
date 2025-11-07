# Resumo do Algoritmo de Personalização

## 📊 Visão Geral em 3 Etapas

```
1. COLETA     2. PROCESSAMENTO     3. ARMAZENAMENTO
   │              │                     │
   ▼              ▼                     ▼
SQL Server   →  SIMS Algorithm   →  DynamoDB
(Compras)        (Cálculo)           (Recomendações)
```

---

## 🎯 Algoritmo SIMS (Co-Purchase)

### Conceito Simples

> **"Se produtos A e B são frequentemente comprados juntos, recomende B quando cliente visualiza A"**

### Como Funciona

#### 1️⃣ Identificar Co-Compras

```
Compra 1: [Produto A, Produto B, Produto C]
Compra 2: [Produto A, Produto B]
Compra 3: [Produto B, Produto C]

Co-compras identificadas:
- A ↔ B (2 vezes)
- A ↔ C (1 vez)
- B ↔ C (2 vezes)
```

#### 2️⃣ Calcular 3 Escores

Para cada par de produtos, calculamos:

| Score | O que mede | Fórmula |
|-------|------------|---------|
| **COUNT** | Quantas vezes foram comprados juntos | `número de co-compras` |
| **LIFT** | Força da associação estatística | `P(A e B) / (P(A) × P(B))` |
| **COSINE** | Similaridade de padrões | `coPurchase / sqrt(freqA × freqB)` |

#### 3️⃣ Combinar Escores

```
Score Final = 30% COUNT + 40% LIFT + 30% COSINE
```

**Por quê esses pesos?**
- **LIFT (40%)**: Prioriza associações estatisticamente significativas
- **COUNT (30%)**: Considera popularidade
- **COSINE (30%)**: Considera similaridade de padrões

#### 4️⃣ Selecionar Top 5

Para cada produto, seleciona os **5 produtos** com maior score combinado.

---

## ⏱️ Decaimento Temporal

### Conceito

**"Compras recentes têm mais peso que compras antigas"**

### Fórmula

```
Peso = 2^(-dias / 30)
```

### Exemplo Prático

| Tempo | Peso | Significado |
|-------|------|-------------|
| Hoje | 100% | Peso total |
| 15 dias | 71% | Ainda muito relevante |
| 30 dias | 50% | Meia-vida (metade do peso) |
| 60 dias | 25% | Pouco relevante |
| 90 dias | 13% | Quase ignorado |

**Benefício**: Sistema se adapta a tendências recentes automaticamente.

---

## 🔄 Fluxo de Processamento

### Primeira Execução

```
┌─────────────────────────────────────────┐
│ 1. Buscar TODAS as compras do banco    │
└─────────────────────────────────────────┘
           │
           ▼
┌─────────────────────────────────────────┐
│ 2. Calcular co-compras com decaimento  │
│    - Para cada compra:                 │
│      * Identificar produtos            │
│      * Calcular pesos temporais        │
│      * Construir matriz                │
└─────────────────────────────────────────┘
           │
           ▼
┌─────────────────────────────────────────┐
│ 3. Calcular escores (COUNT, LIFT, COS) │
│    - Para cada par de produtos:        │
│      * COUNT: número de co-compras     │
│      * LIFT: força da associação       │
│      * COSINE: similaridade            │
└─────────────────────────────────────────┘
           │
           ▼
┌─────────────────────────────────────────┐
│ 4. Normalizar e combinar escores       │
│    - Normalizar para 0-1               │
│    - Combinar com pesos                │
└─────────────────────────────────────────┘
           │
           ▼
┌─────────────────────────────────────────┐
│ 5. Selecionar Top 5 para cada produto  │
└─────────────────────────────────────────┘
           │
           ▼
┌─────────────────────────────────────────┐
│ 6. Salvar no DynamoDB                  │
│    - Um registro por produto           │
│    - Top 5 recomendações               │
└─────────────────────────────────────────┘
```

### Execuções Semanais (Delta)

```
┌─────────────────────────────────────────┐
│ 1. Ler última data processada          │
│    - Arquivo: last_processed_date.txt  │
└─────────────────────────────────────────┘
           │
           ▼
┌─────────────────────────────────────────┐
│ 2. Buscar apenas compras NOVAS         │
│    - Desde última execução             │
│    - Margem de segurança: 60 minutos   │
│    - Processa apenas dados novos       │
└─────────────────────────────────────────┘
           │
           ▼
┌─────────────────────────────────────────┐
│ 3. Recalcular recomendações            │
│    - Apenas para produtos novos        │
│    - Mesmo processo da primeira exec.  │
└─────────────────────────────────────────┘
           │
           ▼
┌─────────────────────────────────────────┐
│ 4. Atualizar DynamoDB                  │
│    - Upsert (atualiza ou insere)       │
│    - Salvar nova data                  │
└─────────────────────────────────────────┘
```

---

## 📈 Exemplo Prático

### Cenário

Histórico de compras:
- **Compra 1** (30 dias atrás): [Produto A, Produto B]
- **Compra 2** (15 dias atrás): [Produto A, Produto B, Produto C]
- **Compra 3** (1 dia atrás): [Produto A, Produto C]

### Cálculo para Produto A

#### 1. Co-compras (com decaimento)

- A ↔ B:
  - Compra 1: peso = 0.5 (30 dias)
  - Compra 2: peso = 0.71 (15 dias)
  - Total: 1.21

- A ↔ C:
  - Compra 2: peso = 0.71 (15 dias)
  - Compra 3: peso = 1.0 (1 dia)
  - Total: 1.71

#### 2. Frequências (com decaimento)

- Produto A: 0.5 + 0.71 + 1.0 = 2.21
- Produto B: 0.5 + 0.71 = 1.21
- Produto C: 0.71 + 1.0 = 1.71
- Total: 5.13

#### 3. Escores

**Para Produto B:**
- COUNT: 1.21
- LIFT: (1.21 × 5.13) / (2.21 × 1.21) = 6.21 / 2.67 = 2.33
- COSINE: 1.21 / sqrt(2.21 × 1.21) = 1.21 / 1.63 = 0.74

**Para Produto C:**
- COUNT: 1.71
- LIFT: (1.71 × 5.13) / (2.21 × 1.71) = 8.77 / 3.78 = 2.32
- COSINE: 1.71 / sqrt(2.21 × 1.71) = 1.71 / 1.94 = 0.88

#### 4. Normalização e Combinação

**Produto B:**
- COUNT norm: 1.21 / 1.71 = 0.71
- LIFT norm: 2.33 / 2.33 = 1.0
- COSINE norm: 0.74 / 0.88 = 0.84
- **Score Final**: 0.3×0.71 + 0.4×1.0 + 0.3×0.84 = **0.87**

**Produto C:**
- COUNT norm: 1.71 / 1.71 = 1.0
- LIFT norm: 2.32 / 2.33 = 1.0
- COSINE norm: 0.88 / 0.88 = 1.0
- **Score Final**: 0.3×1.0 + 0.4×1.0 + 0.3×1.0 = **1.0**

#### 5. Resultado

**Recomendações para Produto A:**
1. Produto C (score: 1.0)
2. Produto B (score: 0.87)

---

## 🎨 Diagrama de Decisão

```
                    ┌──────────────┐
                    │  Nova Compra │
                    └──────┬───────┘
                           │
                           ▼
              ┌────────────────────────┐
              │ Produtos na compra?    │
              └──────┬─────────┬───────┘
                     │         │
            ┌────────┘         └────────┐
            ▼                           ▼
    ┌───────────────┐          ┌───────────────┐
    │ 2+ produtos   │          │ 1 produto     │
    └───────┬───────┘          └───────┬───────┘
            │                          │
            ▼                          ▼
    ┌───────────────┐          ┌───────────────┐
    │ Calcular      │          │ Ignorar       │
    │ co-compras    │          │ (sem co-compra)│
    │ para todos    │          └───────────────┘
    │ os pares      │
    └───────┬───────┘
            │
            ▼
    ┌───────────────┐
    │ Aplicar       │
    │ decaimento    │
    │ temporal      │
    └───────┬───────┘
            │
            ▼
    ┌───────────────┐
    │ Atualizar     │
    │ matriz de     │
    │ co-compras    │
    └───────────────┘
```

---

## 🔑 Pontos-Chave

### ✅ Vantagens

1. **Simples e Eficiente**: Não requer machine learning complexo
2. **Interpretável**: Fácil entender por que um produto foi recomendado
3. **Adaptativo**: Decaimento temporal mantém recomendações atualizadas
4. **Escalável**: Processamento incremental eficiente
5. **Robusto**: Combina múltiplas métricas para maior precisão

### ⚠️ Limitações

1. **Cold Start**: Produtos novos não têm recomendações imediatas
2. **Sazonalidade**: Mudanças sazonais podem demorar para refletir
3. **Produtos nicho**: Menos preciso para produtos pouco comprados

### 🚀 Melhorias Futuras

1. Incorporar características do produto (categoria, marca)
2. Considerar preferências do cliente
3. A/B testing de diferentes pesos
4. Machine learning para maior precisão

---

## 📊 Métricas de Qualidade

### Como Avaliar

1. **Taxa de Clique**: % de clientes que clicam nas recomendações
2. **Taxa de Conversão**: % de recomendações que resultam em compra
3. **Receita Gerada**: Receita total de produtos recomendados
4. **Diversidade**: Variedade de produtos recomendados

### Meta

- Taxa de clique: > 5%
- Taxa de conversão: > 2%
- Receita gerada: > 10% do total

---

**Última atualização**: Janeiro 2024

