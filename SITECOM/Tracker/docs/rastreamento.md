# Documentação - Funcionalidade de Rastreamento dos Correios

## Visão Geral

Esta funcionalidade permite rastrear encomendas dos Correios diretamente na aplicação, exibindo um ícone de avião na listagem de pedidos quando o pedido está enviado via Correios e possui código de rastreamento.
Foi feito baseado no projeto SLCOM (Gerencia do E-Commerce)

## Arquitetura

### Componentes Principais

1. **Serviço de Rastreamento** (`CorreiosService`)
2. **Controller API** (`OrderController`)
3. **DTOs** (Data Transfer Objects)
4. **View** (`SearchResults.cshtml`)
5. **Modelos de Dados**

---

## 1. Serviço de Rastreamento

### Arquivo: `Services/CorreiosService.cs`

O serviço é responsável por fazer a chamada à API dos Correios para obter informações de rastreamento.

#### Método Principal

```csharp
public async Task<CorreiosRastreamentoDTO> GetRastreamentoAsync(string codigoRastreamento)
```

**Parâmetros:**
- `codigoRastreamento`: Código de rastreamento (ex: "QN242960622BR")

**Retorno:**
- `CorreiosRastreamentoDTO`: Objeto com todas as informações de rastreamento

**Funcionamento:**
1. Obtém token de autenticação via `GetCorreiosAuthDTO()`
2. Configura header Authorization com Bearer token
3. Faz GET para: `https://api.correios.com.br/srorastro/v1/objetos/{codigoRastreamento}?resultado=T`
4. Deserializa a resposta JSON
5. Retorna o DTO com os dados

**Exemplo de Uso:**
```csharp
var rastreamento = await _correiosService.GetRastreamentoAsync("QN242960622BR");
```

---

## 2. DTOs (Data Transfer Objects)

### Arquivo: `DTO/CorreiosDTO.cs`

Estrutura completa dos DTOs usados para deserializar a resposta da API dos Correios:

#### CorreiosRastreamentoDTO
```csharp
public class CorreiosRastreamentoDTO
{
    public string versao { get; set; }
    public int quantidade { get; set; }
    public List<ObjetoRastreamentoDTO> objetos { get; set; }
    public string tipoResultado { get; set; }
}
```

#### ObjetoRastreamentoDTO
```csharp
public class ObjetoRastreamentoDTO
{
    public string codObjeto { get; set; }
    public TipoPostalDTO tipoPostal { get; set; }
    public DateTime? dtPrevista { get; set; }
    public string contrato { get; set; }
    public int? largura { get; set; }
    public int? comprimento { get; set; }
    public int? altura { get; set; }
    public double? peso { get; set; }
    public string formato { get; set; }
    public string modalidade { get; set; }
    public List<EventoDTO> eventos { get; set; }
}
```

#### EventoDTO
```csharp
public class EventoDTO
{
    public string codigo { get; set; }
    public string tipo { get; set; }
    public DateTime dtHrCriado { get; set; }
    public string descricao { get; set; }
    public string detalhe { get; set; }
    public UnidadeDTO unidade { get; set; }
    public UnidadeDTO unidadeDestino { get; set; }
}
```

#### Outros DTOs
- `TipoPostalDTO`: sigla, descricao, categoria
- `UnidadeDTO`: codSro, tipo, endereco
- `EnderecoDTO`: cep, logradouro, numero, bairro, cidade, uf

---

## 3. Interface do Serviço

### Arquivo: `Services/interfaces/ICorreiosService.cs`

```csharp
public interface ICorreiosService
{
    public Task<CorreiosDTO> GetCorreiosPACAsync(string cep, int peso);
    public Task<CorreiosRastreamentoDTO> GetRastreamentoAsync(string codigoRastreamento);
}
```

---

## 4. API Endpoint

### Arquivo: `Controllers/OrderController.cs`

#### Endpoint: `GET /Order/GetRastreamento/{codigoRastreamento}`

**Rota:** `/Order/GetRastreamento/{codigoRastreamento}`

**Método:** `GET`

**Parâmetros:**
- `codigoRastreamento` (path): Código de rastreamento da encomenda

**Resposta de Sucesso (200 OK):**
```json
{
  "versao": "3.4.25",
  "quantidade": 1,
  "objetos": [
    {
      "codObjeto": "QN242960622BR",
      "tipoPostal": {
        "sigla": "QN",
        "descricao": "ETIQUETA LOGICA PAC QN",
        "categoria": "ENCOMENDA PAC"
      },
      "dtPrevista": "2025-10-30T23:59:59",
      "eventos": [
        {
          "codigo": "BDE",
          "tipo": "01",
          "dtHrCriado": "2025-10-28T13:51:43",
          "descricao": "Objeto entregue ao destinatário",
          "detalhe": "...",
          "unidade": {
            "codSro": "36500971",
            "tipo": "Unidade de Distribuição",
            "endereco": {
              "cidade": "UBA",
              "uf": "MG"
            }
          }
        }
      ]
    }
  ],
  "tipoResultado": "Todos os Eventos"
}
```

**Resposta de Erro (400 Bad Request):**
```json
{
  "error": "Código de rastreamento é obrigatório"
}
```

**Resposta de Erro (500 Internal Server Error):**
```json
{
  "error": "Erro ao buscar rastreamento: [mensagem do erro]"
}
```

**Exemplo de Uso:**
```javascript
fetch('/Order/GetRastreamento/QN242960622BR')
  .then(response => response.json())
  .then(data => {
    console.log(data);
  });
```

---

## 5. Modelos de Dados

### Atualização dos Modelos

Para exibir o ícone de rastreamento na listagem, foram adicionados os campos `Via` e `Track` nos modelos:

#### OrderSummaryModel (`Models/Entities/OrderSummaryModel.cs`)
```csharp
public string Via { get; set; }
public string Track { get; set; }
```

#### OrderSummaryViewModel (`Models/OrderSummaryViewModel.cs`)
```csharp
public string Via { get; set; }
public string Track { get; set; }
```

#### Atualização do Banco de Dados
- Campo `c.track` adicionado em `_orderFields` em `OrderDB.cs`
- Query `SearchOrderByCustomerNameDBAsync` atualizada para buscar `track` e `via`
- Método `MapOrderSummaryModel` atualizado para mapear `Track`

#### Atualização do Mapper
- `OrderViewMapper.MapOrderSummary()` atualizado para mapear `Via` e `Track`

---

## 6. View - Interface do Usuário

### Arquivo: `Views/Order/SearchResults.cshtml`

#### Condições para Exibir Ícone

O ícone de avião é exibido quando:
- `Status == "V"` (pedido enviado)
- `Via == "C"` (via Correios)
- `Track` não está vazio

#### Estrutura do Modal

O modal possui:
- **Cabeçalho**: Título "Rastreamento de Encomenda"
- **Corpo**: 
  - Informações do objeto (código, tipo postal, previsão)
  - Lista de eventos em cards
  - Evento mais recente destacado
- **Rodapé**: Botão "Fechar"

#### JavaScript

A função `carregarRastreamento(codigoRastreamento)`:
1. Mostra spinner de carregamento
2. Faz chamada à API `/Order/GetRastreamento/{codigoRastreamento}`
3. Renderiza os eventos de forma organizada
4. Trata erros

---

## 7. Integração no E-Commerce

### Passos para Implementar

#### 1. Registrar o Serviço (se ainda não estiver)

```csharp
// Program.cs ou Startup.cs
builder.Services.AddScoped<ICorreiosService, CorreiosService>();
builder.Services.AddHttpClient<ICorreiosService, CorreiosService>();
```

#### 2. Criar o Endpoint API

```csharp
[HttpGet]
[Route("/api/rastreamento/{codigoRastreamento}")]
public async Task<IActionResult> GetRastreamento(string codigoRastreamento)
{
    try
    {
        if (string.IsNullOrWhiteSpace(codigoRastreamento))
        {
            return BadRequest(new { error = "Código de rastreamento é obrigatório" });
        }

        var rastreamento = await _correiosService.GetRastreamentoAsync(codigoRastreamento);
        return Ok(rastreamento);
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { error = "Erro ao buscar rastreamento: " + ex.Message });
    }
}
```

#### 3. Criar Componente/View de Rastreamento

**Exemplo em React:**
```jsx
function RastreamentoModal({ codigoRastreamento, isOpen, onClose }) {
  const [rastreamento, setRastreamento] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (isOpen && codigoRastreamento) {
      setLoading(true);
      fetch(`/api/rastreamento/${codigoRastreamento}`)
        .then(res => res.json())
        .then(data => {
          setRastreamento(data);
          setLoading(false);
        })
        .catch(err => {
          setError(err.message);
          setLoading(false);
        });
    }
  }, [isOpen, codigoRastreamento]);

  if (!isOpen) return null;

  return (
    <Modal onClose={onClose}>
      {loading && <Spinner />}
      {error && <Alert>{error}</Alert>}
      {rastreamento && (
        <div>
          <h3>Código: {rastreamento.objetos[0]?.codObjeto}</h3>
          {rastreamento.objetos[0]?.eventos.map((evento, index) => (
            <Card key={index} highlighted={index === 0}>
              <h6>{evento.descricao}</h6>
              <p>{new Date(evento.dtHrCriado).toLocaleString('pt-BR')}</p>
              {evento.unidade?.endereco && (
                <p>{evento.unidade.endereco.cidade}/{evento.unidade.endereco.uf}</p>
              )}
            </Card>
          ))}
        </div>
      )}
    </Modal>
  );
}
```

**Exemplo em Vue.js:**
```vue
<template>
  <Modal v-if="isOpen" @close="onClose">
    <div v-if="loading">Carregando...</div>
    <div v-if="error">{{ error }}</div>
    <div v-if="rastreamento">
      <h3>Código: {{ rastreamento.objetos[0]?.codObjeto }}</h3>
      <div 
        v-for="(evento, index) in rastreamento.objetos[0]?.eventos" 
        :key="index"
        class="card"
        :class="{ 'highlighted': index === 0 }"
      >
        <h6>{{ evento.descricao }}</h6>
        <p>{{ formatDate(evento.dtHrCriado) }}</p>
        <p v-if="evento.unidade?.endereco">
          {{ evento.unidade.endereco.cidade }}/{{ evento.unidade.endereco.uf }}
        </p>
      </div>
    </div>
  </Modal>
</template>

<script>
export default {
  props: ['codigoRastreamento', 'isOpen'],
  data() {
    return {
      rastreamento: null,
      loading: false,
      error: null
    };
  },
  watch: {
    isOpen(newVal) {
      if (newVal && this.codigoRastreamento) {
        this.buscarRastreamento();
      }
    }
  },
  methods: {
    async buscarRastreamento() {
      this.loading = true;
      try {
        const response = await fetch(`/api/rastreamento/${this.codigoRastreamento}`);
        this.rastreamento = await response.json();
      } catch (err) {
        this.error = err.message;
      } finally {
        this.loading = false;
      }
    },
    formatDate(date) {
      return new Date(date).toLocaleString('pt-BR');
    },
    onClose() {
      this.$emit('close');
    }
  }
};
</script>
```

#### 4. Adicionar Botão/Ícone na Listagem de Pedidos

**Exemplo:**
```html
@if (pedido.Status == "V" && pedido.Via == "C" && !string.IsNullOrEmpty(pedido.Track))
{
    <button onclick="abrirRastreamento('@pedido.Track')">
        <i class="bi bi-airplane-fill"></i>
    </button>
}
```

---

## 8. Autenticação com API dos Correios

### Configuração Necessária

O serviço usa autenticação via token Bearer. A autenticação é feita automaticamente pelo método `GetCorreiosAuthDTO()` que:

1. Verifica se há token em cache (Redis)
2. Se não houver, faz autenticação via Basic Auth usando:
   - `Correios:key` (chave de autenticação)
   - `Correios:cartaopostal` (número do cartão postal)
3. Armazena o token em cache por determinado tempo

### Configuração no appsettings.json

```json
{
  "Correios": {
    "key": "sua_chave_basic_auth",
    "cartaopostal": "0078034701",
    "ceporigem": "01310100"
  }
}
```

---

## 9. Estrutura de Resposta da API dos Correios

### Endpoint
`GET https://api.correios.com.br/srorastro/v1/objetos/{codigoRastreamento}?resultado=T`

### Parâmetros
- `codigoRastreamento`: Código de rastreamento (ex: "QN242960622BR")
- `resultado=T`: Retorna todos os eventos

### Headers Necessários
- `Authorization: Bearer {token}`
- `Accept: application/json`

---

## 10. Exemplos de Códigos de Rastreamento

Os códigos dos Correios seguem o padrão:
- **PAC**: QN (QN + 9 dígitos + BR)
- **SEDEX**: AA (AA + 9 dígitos + BR)
- **SEDEX 10**: AB (AB + 9 dígitos + BR)

Exemplos:
- `QN242960622BR`
- `AA123456789BR`
- `AB987654321BR`

---

## 11. Tratamento de Erros

### Erros Comuns

1. **Código inválido ou não encontrado**
   - A API retorna objeto vazio ou erro
   - Tratar no frontend mostrando mensagem apropriada

2. **Token expirado**
   - O serviço automaticamente renova o token
   - Não é necessário tratamento manual

3. **Erro de rede**
   - Tratar no JavaScript com try/catch
   - Mostrar mensagem de erro ao usuário

### Exemplo de Tratamento

```javascript
try {
  const response = await fetch(`/api/rastreamento/${codigo}`);
  if (!response.ok) {
    throw new Error('Erro ao buscar rastreamento');
  }
  const data = await response.json();
  if (!data.objetos || data.objetos.length === 0) {
    throw new Error('Nenhuma informação encontrada');
  }
  // Processar dados
} catch (error) {
  console.error(error);
  // Mostrar mensagem de erro ao usuário
}
```

---

## 12. Melhorias Futuras

### Possíveis Melhorias

1. **Cache de Rastreamento**
   - Cachear informações de rastreamento por algumas horas
   - Reduzir chamadas à API dos Correios

2. **Notificações**
   - Notificar quando houver atualização no rastreamento
   - Webhook ou polling periódico

3. **Histórico de Rastreamento**
   - Armazenar histórico de eventos no banco
   - Comparar eventos para detectar atualizações

4. **Múltiplos Objetos**
   - Suportar rastreamento de múltiplos objetos de uma vez
   - API dos Correios permite até 50 objetos por requisição

5. **Exportação**
   - Permitir exportar rastreamento em PDF
   - Enviar por email

---

## 13. Checklist de Implementação

### Backend
- [ ] Serviço `CorreiosService` implementado
- [ ] Interface `ICorreiosService` criada
- [ ] DTOs criados
- [ ] Endpoint API criado
- [ ] Serviço registrado no DI container
- [ ] Configuração no appsettings.json

### Frontend
- [ ] Componente/View de modal criado
- [ ] Função JavaScript para chamar API
- [ ] Ícone/botão na listagem de pedidos
- [ ] Tratamento de erros
- [ ] Loading state
- [ ] Estilização (CSS)

### Testes
- [ ] Testar com código válido
- [ ] Testar com código inválido
- [ ] Testar quando API dos Correios está offline
- [ ] Testar com token expirado
- [ ] Testar responsividade

---

## 14. Referências

### Documentação da API dos Correios
- Endpoint: `https://api.correios.com.br/srorastro/v1/objetos/{codigo}?resultado=T`
- Autenticação: Bearer Token (obtido via `/token/v1/autentica/cartaopostagem`)

### Arquivos Relacionados no Projeto
- `Services/CorreiosService.cs`
- `Services/interfaces/ICorreiosService.cs`
- `DTO/CorreiosDTO.cs`
- `Controllers/OrderController.cs`
- `Views/Order/SearchResults.cshtml`
- `Models/Entities/OrderSummaryModel.cs`
- `Models/OrderSummaryViewModel.cs`
- `Models/Mappers/OrderViewMapper.cs`
- `Models/Domains/DB/OrderDB.cs`

---

## Conclusão

Esta documentação fornece todas as informações necessárias para implementar a funcionalidade de rastreamento dos Correios em qualquer plataforma (backend ou frontend). O código está modularizado e pode ser facilmente adaptado para diferentes frameworks e tecnologias.

