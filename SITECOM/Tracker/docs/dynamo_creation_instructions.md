# Instruções para Criação da Tabela DynamoDB - Rastreamento de Pedidos

## Visão Geral e Contexto

Esta tabela DynamoDB será utilizada para armazenar os dados de rastreamento de pedidos obtidos das APIs de diferentes transportadoras (Correios, Jadlog, Gollog, etc.). O fluxo de trabalho será:

1. **Consulta ao SQL**: Verificar novos pedidos com rastreamento
2. **Identificação da Transportadora**: Identificar qual tipo de envio foi feito (Correios, Jadlog, Gollog, etc.)
3. **Chamada à API**: Obter dados de rastreamento via API da transportadora correspondente
4. **Armazenamento**: Salvar o JSON completo no DynamoDB usando o ID da compra como chave, incluindo o tipo de envio
5. **Monitoramento Diário**: Ler todos os registros, comparar com novas consultas à API e atualizar quando houver mudanças
6. **Notificação**: Enviar comunicação ao cliente quando houver atualizações
7. **Limpeza**: Remover registro do DynamoDB quando o pedido for entregue

---

## Explicação Teórica: O que é DynamoDB?

### Conceitos Fundamentais

**Amazon DynamoDB** é um banco de dados NoSQL gerenciado pela AWS que oferece:
- **Performance**: Baixa latência (milissegundos) para leitura e escrita
- **Escalabilidade**: Escala automaticamente conforme a demanda
- **Gerenciamento**: Totalmente gerenciado pela AWS (sem servidores para administrar)
- **Modelo de Dados**: Armazena dados em formato de pares chave-valor e documentos JSON

### Estrutura de Dados

- **Tabela (Table)**: Container principal que armazena os dados
- **Item**: Uma linha/registro na tabela (equivalente a uma linha em SQL)
- **Atributo**: Um campo dentro de um item (equivalente a uma coluna em SQL)
- **Chave Primária (Primary Key)**: Identificador único de cada item
  - **Partition Key (HASH)**: Chave simples que determina em qual partição física o item será armazenado
  - **Sort Key (RANGE)**: Opcional, permite ordenação dentro da mesma partition key

### Por que DynamoDB para este caso?

1. **Acesso por ID único**: Você precisa buscar rapidamente por `pedido_id` - ideal para DynamoDB
2. **Armazenamento de JSON**: DynamoDB suporta nativamente documentos JSON complexos
3. **Operações simples**: Put (inserir/atualizar), Get (buscar por chave), Scan (ler todos), Delete (remover)
4. **Custo-benefício**: Paga apenas pelo que usa (on-demand) ou provisiona capacidade fixa
5. **Integração AWS**: Funciona perfeitamente com Lambda, que provavelmente executará sua lógica

### Modelo de Dados para Rastreamento

```
Tabela: tracker-pedidos
├── id_pedido (Chave Primária - String)
├── tipo_envio (String) - Transportadora: "Correios", "Jadlog", "Gollog", etc.
├── cod_rastreamento (String)
├── rastreamento_json (String - JSON completo da API)
└── data_criacao (String - Timestamp ISO 8601)
```

**Campos da Tabela**:
- `id_pedido`: Chave primária que identifica unicamente cada pedido
- `tipo_envio`: Identifica qual transportadora foi utilizada (ex: "Correios", "Jadlog", "Gollog")
- `cod_rastreamento`: Código de rastreamento fornecido pela transportadora
- `rastreamento_json`: JSON completo retornado pela API de rastreamento
- `data_criacao`: Timestamp de quando o registro foi criado no DynamoDB

**Por que `id_pedido` como chave primária?**
- Cada pedido é único
- Busca rápida e direta: `GetItem(id_pedido)`
- Evita duplicatas naturalmente
- Permite atualização eficiente do mesmo pedido

---

## Instruções Passo a Passo - AWS Console

### Passo 1: Acessar o AWS Console

1. Abra seu navegador e acesse: https://console.aws.amazon.com/
2. Faça login com suas credenciais AWS
3. No topo da página, verifique se está na região correta (ex: `us-east-1`, `sa-east-1`)
   - **Dica**: Clique no seletor de região no canto superior direito para alterar se necessário

### Passo 2: Navegar até DynamoDB

1. No campo de busca no topo (barra de serviços), digite: **"DynamoDB"**
2. Clique no serviço **"DynamoDB"** nos resultados
3. Você será direcionado para a página principal do DynamoDB

### Passo 3: Criar Nova Tabela

1. Na página do DynamoDB, localize o botão **"Create table"** (canto superior direito)
2. Clique em **"Create table"**

### Passo 4: Configurar Nome e Chave Primária

Na seção **"Table details"**:

1. **Table name**: 
   - Digite: `tracker-pedidos`
   - **Importante**: Nomes de tabela são case-sensitive e devem seguir regras específicas
   - Use apenas letras minúsculas, números e hífens

2. **Partition key** (Chave de Partição):
   - **Partition key**: `id_pedido`
   - **Data type**: Selecione **"String"** (S)
   - Esta será a chave primária que identifica unicamente cada pedido

3. **Sort key** (Chave de Ordenação):
   - **Deixe em branco** - não é necessário para este caso
   - Você só precisa buscar por ID do pedido, não precisa de ordenação adicional

### Passo 5: Configurar Capacidade (Table Settings)

Na seção **"Table settings"**:

1. **Table class**:
   - Selecione **"Standard"** (padrão)
   - **Standard-IA** é mais barato, mas tem limitações (não recomendado para este caso)

2. **Capacity settings**:
   - **Opção recomendada**: Selecione **"On-demand"**
     - **Vantagens**: 
       - Paga apenas pelo que usar
       - Escala automaticamente
       - Sem necessidade de provisionar capacidade
       - Ideal para cargas variáveis
   - **Alternativa**: Selecione **"Provisioned"** (apenas se souber exatamente o volume)
     - **Read capacity units (RCU)**: Ex: 5
     - **Write capacity units (WCU)**: Ex: 5
     - **Auto scaling**: Pode ativar para ajuste automático

### Passo 6: Configurações Adicionais (Opcional mas Recomendado)

Role a página para baixo até **"Additional settings"**:

1. **Encryption**:
   - **Encryption at rest**: Selecione **"Enable"**
   - **Encryption key**: Escolha **"AWS owned key"** (mais simples) ou **"AWS managed key"** (mais controle)
   - **Por que ativar?**: Protege dados sensíveis de rastreamento

2. **Tags** (Opcional, mas útil para organização):
   - Clique em **"Add tag"**
   - **Tag key**: `Project`
   - **Tag value**: `Tracker`
   - Adicione mais tags se desejar (ex: `Environment: Production`)

3. **Point-in-time recovery (PITR)**:
   - **Deixe desabilitado** por enquanto (pode ativar depois se necessário)
   - Permite restaurar tabela para um ponto no tempo específico

4. **Time to Live (TTL)**:
   - **Deixe desabilitado** por enquanto
   - Você removerá manualmente quando detectar entrega
   - Pode ativar depois como backup de limpeza automática

### Passo 7: Revisar e Criar

1. Role até o final da página
2. Revise todas as configurações:
   - ✅ Nome: `tracker-pedidos`
   - ✅ Partition key: `id_pedido` (String)
   - ✅ Billing mode: On-demand
   - ✅ Encryption: Enabled
3. Clique no botão **"Create table"** (canto inferior direito)

### Passo 8: Aguardar Criação

1. Você será redirecionado para a página de detalhes da tabela
2. Aguarde alguns segundos até o status mudar de **"Creating"** para **"Active"**
3. Quando o status estiver **"Active"**, a tabela está pronta para uso!

---

## Verificação da Tabela Criada

### Como Verificar no Console

1. Na página da tabela, verifique:
   - **Status**: Deve estar como **"Active"** (verde)
   - **Table name**: `tracker-pedidos`
   - **Partition key**: `id_pedido` (String)

2. Na aba **"Items"** (no menu lateral):
   - A tabela estará vazia inicialmente
   - Aqui você poderá visualizar os itens depois de inserir dados

3. Na aba **"Overview"**:
   - Veja informações sobre capacidade, métricas, etc.

### Estrutura Final da Tabela

```
Nome: tracker-pedidos
Partition Key: id_pedido (String)
Status: Active
Billing Mode: On-demand
Encryption: Enabled
```

---

## Próximos Passos (Após Criação)

### 1. Configurar Permissões IAM

Sua aplicação/Lambda precisará de permissões para acessar a tabela. Exemplo de política IAM:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "dynamodb:PutItem",
        "dynamodb:GetItem",
        "dynamodb:UpdateItem",
        "dynamodb:DeleteItem",
        "dynamodb:Scan"
      ],
      "Resource": "arn:aws:dynamodb:REGION:ACCOUNT_ID:table/tracker-pedidos"
    }
  ]
}
```

**Ações explicadas**:
- `PutItem`: Inserir novo registro ou substituir existente
- `GetItem`: Buscar registro por chave primária (`id_pedido`)
- `UpdateItem`: Atualizar campos específicos de um registro
- `DeleteItem`: Remover registro quando pedido for entregue
- `Scan`: Ler todos os registros (usado no monitoramento diário)

### 2. Estrutura de Dados Esperada

Quando você inserir dados, um item típico terá esta estrutura:

```json
{
  "id_pedido": "12345",
  "tipo_envio": "Correios",
  "cod_rastreamento": "QN242960622BR",
  "rastreamento_json": "{\"versao\":\"1.0\",\"quantidade\":1,\"objetos\":[...]}",
  "data_criacao": "2024-01-15T08:00:00Z"
}
```

**Exemplo com outras transportadoras**:

```json
{
  "id_pedido": "12346",
  "tipo_envio": "Jadlog",
  "cod_rastreamento": "JDL123456789BR",
  "rastreamento_json": "{...}",
  "data_criacao": "2024-01-15T08:00:00Z"
}
```

```json
{
  "id_pedido": "12347",
  "tipo_envio": "Gollog",
  "cod_rastreamento": "GOL987654321BR",
  "rastreamento_json": "{...}",
  "data_criacao": "2024-01-15T08:00:00Z"
}
```

**Observações**:
- `id_pedido` é obrigatório (chave primária)
- `tipo_envio` identifica qual API chamar durante o monitoramento
- `cod_rastreamento` é o código fornecido pela transportadora
- `rastreamento_json` armazena o JSON completo retornado pela API
- `data_criacao` registra quando o pedido foi adicionado ao DynamoDB

### 3. Operações que Você Implementará

#### Inserir Novo Pedido (quando encontrar no SQL)
```
PutItem com:
- id_pedido (obrigatório - chave primária)
- tipo_envio (ex: "Correios", "Jadlog", "Gollog")
- cod_rastreamento
- rastreamento_json (JSON completo da API)
- data_criacao (timestamp atual)
```

#### Ler Todos os Pedidos (monitoramento diário)
```
Scan - retorna todos os itens da tabela
Para cada item:
- Identificar tipo_envio para chamar a API correta
- Comparar novo JSON com rastreamento_json armazenado
- Se houver mudança, atualizar rastreamento_json
```

#### Atualizar Pedido (quando houver mudança)
```
UpdateItem - atualiza:
- rastreamento_json (com novo JSON da API)
```

**Nota**: Como a estrutura é simples, você apenas substitui o `rastreamento_json` quando houver atualização. O status e outras informações podem ser extraídas do próprio JSON quando necessário.

#### Remover Pedido (quando entregue)
```
DeleteItem usando id_pedido
```

**Importante**: O campo `tipo_envio` permite que durante o monitoramento diário você:
1. Leia todos os registros (Scan)
2. Para cada registro, identifique a transportadora pelo campo `tipo_envio`
3. Chame a API correspondente (Correios, Jadlog, Gollog, etc.)
4. Compare e atualize se necessário

---

## Dicas e Boas Práticas

### Performance

1. **Use GetItem para busca por ID**: Muito mais rápido que Scan
2. **Evite Scan quando possível**: Scan lê toda a tabela (pode ser caro)
3. **Para monitoramento diário**: Scan é aceitável se a tabela não for muito grande

### Custos

- **On-demand**: Paga por:
  - Escritas: $1.25 por milhão de requisições
  - Leituras: $0.25 por milhão de requisições
  - Armazenamento: $0.25 por GB/mês

### Segurança

- ✅ Encryption at rest ativado
- ✅ Use IAM para controlar acesso
- ✅ Não exponha credenciais no código

### Monitoramento

- Use CloudWatch para monitorar:
  - Número de requisições
  - Latência
  - Erros
  - Capacidade consumida

---

## Resumo Rápido

1. ✅ Acesse AWS Console → DynamoDB
2. ✅ Clique em "Create table"
3. ✅ Nome: `tracker-pedidos`
4. ✅ Partition key: `id_pedido` (String)
5. ✅ Billing: On-demand
6. ✅ Encryption: Enable
7. ✅ Create table
8. ✅ Aguarde status "Active"

**Estrutura simplificada da tabela**:
- `id_pedido` (chave primária)
- `tipo_envio`
- `cod_rastreamento`
- `rastreamento_json`
- `data_criacao`

**Pronto!** Sua tabela está criada e pronta para armazenar dados de rastreamento.

