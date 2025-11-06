# Documentação: Modal de Rastreamento - Formato HTML

## Visão Geral

O modal de rastreamento exibe informações de rastreamento de encomendas em um formato visual de timeline (linha do tempo), mostrando o progresso do envio desde a origem até o destino. O HTML é gerado dinamicamente via JavaScript a partir de dados JSON retornados pela API.

## Estrutura do Modal

### 1. Container Principal do Modal

```html
<div class="modal fade" id="modalRastreamento" tabindex="-1" aria-labelledby="modalRastreamentoLabel" aria-hidden="true">
    <div class="modal-dialog modal-lg modal-dialog-scrollable">
        <div class="modal-content">
            <!-- Header -->
            <div class="modal-header">
                <h5 class="modal-title d-flex align-items-center gap-2" id="modalRastreamentoLabel">
                    <img src="/images/track.png" alt="Rastreamento" style="width: 56px; height: auto;" />
                    Rastreamento de Encomenda
                </h5>
                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Fechar"></button>
            </div>
            
            <!-- Body (conteúdo dinâmico) -->
            <div class="modal-body" id="rastreamentoContent">
                <!-- Conteúdo será inserido aqui via JavaScript -->
            </div>
        </div>
    </div>
</div>
```

**Características:**
- `modal-lg`: Modal de tamanho grande
- `modal-dialog-scrollable`: Permite scroll quando o conteúdo é grande
- Header com ícone e título
- Body com ID `rastreamentoContent` para inserção dinâmica do conteúdo

### 2. Estado de Carregamento

Antes de carregar os dados, exibe um spinner:

```html
<div class="text-center">
    <div class="spinner-border text-primary" role="status">
        <span class="visually-hidden">Carregando...</span>
    </div>
    <p class="mt-2">Carregando informações de rastreamento...</p>
</div>
```

## Estrutura dos Dados JSON

### Formato da Resposta da API

```json
{
  "versao": "1.0",
  "quantidade": 1,
  "tipoResultado": "T",
  "objetos": [
    {
      "codObjeto": "QN242960622BR",
      "tipoPostal": {
        "sigla": "QN",
        "descricao": "Objeto Internacional",
        "categoria": "ENCOMENDA"
      },
      "dtPrevista": "2024-01-15T00:00:00",
      "eventos": [
        {
          "codigo": "BDE",
          "tipo": "01",
          "dtHrCriado": "2024-01-10T14:30:00",
          "descricao": "Objeto entregue ao destinatário",
          "detalhe": "Assinado por: João Silva",
          "unidade": {
            "codSro": "1234567",
            "tipo": "01",
            "endereco": {
              "cidade": "São Paulo",
              "uf": "SP",
              "cep": "01310-100",
              "logradouro": "Av. Paulista",
              "numero": "1000",
              "bairro": "Bela Vista"
            }
          }
        }
      ]
    }
  ]
}
```

## Estrutura HTML Gerada

### 1. Card de Informações do Objeto

```html
<div class="card mb-3">
    <div class="card-body">
        <h6 class="card-title mb-2">Informações do Objeto</h6>
        <div class="row">
            <div class="col-md-4">
                <p class="mb-1"><strong>Código:</strong> QN242960622BR</p>
            </div>
            <div class="col-md-4">
                <p class="mb-1"><strong>Tipo:</strong> Objeto Internacional</p>
            </div>
            <div class="col-md-4">
                <p class="mb-1"><strong>Previsão:</strong> 15/01/2024</p>
            </div>
        </div>
    </div>
</div>
```

**Campos exibidos:**
- **Código**: Código de rastreamento (ex: `codObjeto`)
- **Tipo**: Descrição ou sigla do tipo postal (ex: `tipoPostal.descricao` ou `tipoPostal.sigla`)
- **Previsão**: Data prevista de entrega formatada em `pt-BR` (ex: `dtPrevista`)

### 2. Título da Timeline

```html
<h6 class="mb-3">Histórico de Rastreamento</h6>
```

### 3. Container da Timeline

```html
<div class="timeline-container" style="position: relative; padding-left: 55px;">
    <!-- Eventos serão inseridos aqui -->
</div>
```

**Estilos:**
- `position: relative`: Permite posicionamento absoluto dos elementos filhos
- `padding-left: 55px`: Espaço para o ícone do evento mais recente

### 4. Estrutura de Cada Evento (Timeline Item)

#### Evento Mais Recente (Primeiro da Lista)

```html
<div class="timeline-item mb-4" style="position: relative;">
    <!-- Linha vertical conectando ao próximo evento -->
    <div style="position: absolute; left: -20px; top: 30px; width: 2px; height: calc(100% + 1rem); background-color: #dee2e6; z-index: 1;"></div>
    
    <div class="d-flex align-items-start">
        <!-- Ícone destacado para o evento mais recente -->
        <div style="position: absolute; left: -40px; top: 0; width: 48px; height: 48px; display: flex; align-items: center; justify-content: center; background-color: rgba(13, 110, 253, 0.1); border: 2px solid #0d6efd; border-radius: 50%; z-index: 10;">
            <span style="font-size: 28px; line-height: 1;">🐟</span>
        </div>
        
        <!-- Card com informações do evento -->
        <div class="flex-grow-1">
            <div class="card border-primary">
                <div class="card-body p-3">
                    <h6 class="card-title mb-1 text-primary">Objeto entregue ao destinatário</h6>
                    <p class="card-text mb-1 text-muted small">
                        <i class="bi bi-calendar3 me-1"></i>10/01/2024 14:30:00
                    </p>
                    <p class="card-text mb-1 text-muted small">
                        <i class="bi bi-geo-alt me-1"></i>São Paulo/SP
                    </p>
                    <p class="card-text mb-0 small">Assinado por: João Silva</p>
                </div>
            </div>
        </div>
    </div>
</div>
```

#### Eventos Anteriores (Sem Ícone)

```html
<div class="timeline-item mb-4" style="position: relative;">
    <!-- Linha vertical conectando ao próximo evento -->
    <div style="position: absolute; left: -20px; top: 30px; width: 2px; height: calc(100% + 1rem); background-color: #dee2e6; z-index: 1;"></div>
    
    <div class="d-flex align-items-start">
        <!-- Sem ícone para eventos anteriores -->
        
        <!-- Card com informações do evento -->
        <div class="flex-grow-1">
            <div class="card">
                <div class="card-body p-3">
                    <h6 class="card-title mb-1">Objeto em trânsito</h6>
                    <p class="card-text mb-1 text-muted small">
                        <i class="bi bi-calendar3 me-1"></i>09/01/2024 10:15:00
                    </p>
                    <p class="card-text mb-1 text-muted small">
                        <i class="bi bi-geo-alt me-1"></i>Rio de Janeiro/RJ
                    </p>
                </div>
            </div>
        </div>
    </div>
</div>
```

**Diferenças entre evento mais recente e anteriores:**
- **Evento mais recente**: 
  - Tem ícone circular destacado (emoji 🐟)
  - Card com borda azul (`border-primary`)
  - Título em azul (`text-primary`)
  - Ícone com fundo azul claro e borda azul
  
- **Eventos anteriores**:
  - Sem ícone
  - Card padrão (sem borda colorida)
  - Título em cor padrão

### 5. Linha Vertical da Timeline

A linha vertical conecta os eventos visualmente:

```html
<div style="position: absolute; left: -20px; top: 30px; width: 2px; height: calc(100% + 1rem); background-color: #dee2e6; z-index: 1;"></div>
```

**Características:**
- Posicionada à esquerda do conteúdo (-20px)
- Começa abaixo do topo do evento (top: 30px)
- Altura calculada dinamicamente (`calc(100% + 1rem)`)
- Cor cinza claro (#dee2e6)
- **Não exibida no último evento** (último item da lista)

## JavaScript - Geração Dinâmica do HTML

### Função Principal

```javascript
function abrirRastreamento(codigoRastreamento) {
    const modal = new bootstrap.Modal(document.getElementById('modalRastreamento'));
    const content = document.getElementById('rastreamentoContent');
    
    // Resetar conteúdo (mostrar loading)
    content.innerHTML = `
        <div class="text-center">
            <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Carregando...</span>
            </div>
            <p class="mt-2">Carregando informações de rastreamento...</p>
        </div>
    `;
    
    modal.show();
    
    // Buscar dados
    fetch('/Orders/GetRastreamento?codigoRastreamento=' + encodeURIComponent(codigoRastreamento))
        .then(response => {
            if (!response.ok) {
                return response.json().then(err => { 
                    throw new Error(err.error || 'Erro ao buscar rastreamento'); 
                });
            }
            return response.json();
        })
        .then(data => {
            // Validar dados
            if (!data.objetos || data.objetos.length === 0) {
                content.innerHTML = '<div class="alert alert-warning">Nenhuma informação de rastreamento encontrada.</div>';
                return;
            }
            
            const objeto = data.objetos[0];
            const eventos = objeto.eventos || [];
            
            // Ordenar eventos: mais recente primeiro (topo)
            eventos.sort((a, b) => new Date(b.dtHrCriado) - new Date(a.dtHrCriado));
            
            // Gerar HTML
            let html = gerarHTMLRastreamento(objeto, eventos);
            content.innerHTML = html;
        })
        .catch(error => {
            content.innerHTML = `<div class="alert alert-danger">Erro ao carregar rastreamento: ${error.message}</div>`;
        });
}
```

### Geração do HTML

```javascript
function gerarHTMLRastreamento(objeto, eventos) {
    // Card de informações do objeto
    let html = `
        <div class="card mb-3">
            <div class="card-body">
                <h6 class="card-title mb-2">Informações do Objeto</h6>
                <div class="row">
                    <div class="col-md-4">
                        <p class="mb-1"><strong>Código:</strong> ${objeto.codObjeto || 'N/A'}</p>
                    </div>
                    ${objeto.tipoPostal ? `
                        <div class="col-md-4">
                            <p class="mb-1"><strong>Tipo:</strong> ${objeto.tipoPostal.descricao || objeto.tipoPostal.sigla || 'N/A'}</p>
                        </div>
                    ` : ''}
                    ${objeto.dtPrevista ? `
                        <div class="col-md-4">
                            <p class="mb-1"><strong>Previsão:</strong> ${new Date(objeto.dtPrevista).toLocaleDateString('pt-BR')}</p>
                        </div>
                    ` : ''}
                </div>
            </div>
        </div>
        <h6 class="mb-3">Histórico de Rastreamento</h6>
        <div class="timeline-container" style="position: relative; padding-left: 55px;">
    `;
    
    // Gerar eventos
    eventos.forEach((evento, index) => {
        const isFirst = index === 0; // Evento mais recente
        const eventoDate = new Date(evento.dtHrCriado);
        
        // Formatar localização
        const localizacao = evento.unidade?.endereco 
            ? `${evento.unidade.endereco.cidade || ''}/${evento.unidade.endereco.uf || ''}`.replace(/^\/|\/$/g, '')
            : '';
        
        // Linha vertical (não exibir no último evento)
        const linhaVertical = index < eventos.length - 1 
            ? '<div style="position: absolute; left: -20px; top: 30px; width: 2px; height: calc(100% + 1rem); background-color: #dee2e6; z-index: 1;"></div>'
            : '';
        
        // Ícone do evento mais recente
        const icone = isFirst 
            ? `<div style="position: absolute; left: -40px; top: 0; width: 48px; height: 48px; display: flex; align-items: center; justify-content: center; background-color: rgba(13, 110, 253, 0.1); border: 2px solid #0d6efd; border-radius: 50%; z-index: 10;">
                  <span style="font-size: 28px; line-height: 1;">🐟</span>
               </div>`
            : '';
        
        // Classes do card
        const cardClasses = isFirst ? 'card border-primary' : 'card';
        const titleClasses = isFirst ? 'card-title mb-1 text-primary' : 'card-title mb-1';
        
        html += `
            <div class="timeline-item mb-4" style="position: relative;">
                ${linhaVertical}
                <div class="d-flex align-items-start">
                    ${icone}
                    <div class="flex-grow-1">
                        <div class="${cardClasses}">
                            <div class="card-body p-3">
                                <h6 class="${titleClasses}">${evento.descricao || 'N/A'}</h6>
                                <p class="card-text mb-1 text-muted small">
                                    <i class="bi bi-calendar3 me-1"></i>${eventoDate.toLocaleString('pt-BR')}
                                </p>
                                ${localizacao ? `
                                    <p class="card-text mb-1 text-muted small">
                                        <i class="bi bi-geo-alt me-1"></i>${localizacao}
                                    </p>
                                ` : ''}
                                ${evento.detalhe ? `
                                    <p class="card-text mb-0 small">${evento.detalhe}</p>
                                ` : ''}
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;
    });
    
    html += `</div>`;
    
    return html;
}
```

## Estilos CSS

### Estilos do Modal

```css
#modalRastreamento .modal-content {
    border-radius: 1rem;
}

#modalRastreamento .modal-header {
    border-top-left-radius: 1rem;
    border-top-right-radius: 1rem;
}

.airplane-tracking-btn {
    background: none;
    border: none;
}

.airplane-tracking-btn .track-icon {
    width: 40px;
    height: 40px;
    object-fit: contain;
}
```

**Características:**
- Bordas arredondadas no modal (`border-radius: 1rem`)
- Botão de rastreamento sem fundo/borda
- Ícone de rastreamento com tamanho fixo (40x40px)

## Dependências

### Bibliotecas Necessárias

1. **Bootstrap 5**: Para componentes do modal e cards
   ```html
   <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" rel="stylesheet">
   <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js"></script>
   ```

2. **Bootstrap Icons**: Para ícones (calendário, localização)
   ```html
   <link href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.0/font/bootstrap-icons.css" rel="stylesheet">
   ```

### Font Awesome (Alternativa)

Se preferir usar Font Awesome ao invés de Bootstrap Icons:

```html
<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css">
```

E substituir os ícones:
- `bi bi-calendar3` → `fas fa-calendar`
- `bi bi-geo-alt` → `fas fa-map-marker-alt`

## Estrutura Completa do HTML Gerado

### Exemplo Completo

```html
<!-- Modal -->
<div class="modal fade" id="modalRastreamento" tabindex="-1">
    <div class="modal-dialog modal-lg modal-dialog-scrollable">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title">
                    <img src="/images/track.png" alt="Rastreamento" style="width: 56px;" />
                    Rastreamento de Encomenda
                </h5>
                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
            </div>
            <div class="modal-body" id="rastreamentoContent">
                <!-- Card de Informações -->
                <div class="card mb-3">
                    <div class="card-body">
                        <h6 class="card-title mb-2">Informações do Objeto</h6>
                        <div class="row">
                            <div class="col-md-4">
                                <p class="mb-1"><strong>Código:</strong> QN242960622BR</p>
                            </div>
                            <div class="col-md-4">
                                <p class="mb-1"><strong>Tipo:</strong> Objeto Internacional</p>
                            </div>
                            <div class="col-md-4">
                                <p class="mb-1"><strong>Previsão:</strong> 15/01/2024</p>
                            </div>
                        </div>
                    </div>
                </div>
                
                <!-- Título da Timeline -->
                <h6 class="mb-3">Histórico de Rastreamento</h6>
                
                <!-- Timeline Container -->
                <div class="timeline-container" style="position: relative; padding-left: 55px;">
                    
                    <!-- Evento 1 (Mais Recente) -->
                    <div class="timeline-item mb-4" style="position: relative;">
                        <!-- Linha vertical -->
                        <div style="position: absolute; left: -20px; top: 30px; width: 2px; height: calc(100% + 1rem); background-color: #dee2e6; z-index: 1;"></div>
                        
                        <div class="d-flex align-items-start">
                            <!-- Ícone -->
                            <div style="position: absolute; left: -40px; top: 0; width: 48px; height: 48px; display: flex; align-items: center; justify-content: center; background-color: rgba(13, 110, 253, 0.1); border: 2px solid #0d6efd; border-radius: 50%; z-index: 10;">
                                <span style="font-size: 28px; line-height: 1;">🐟</span>
                            </div>
                            
                            <!-- Card -->
                            <div class="flex-grow-1">
                                <div class="card border-primary">
                                    <div class="card-body p-3">
                                        <h6 class="card-title mb-1 text-primary">Objeto entregue ao destinatário</h6>
                                        <p class="card-text mb-1 text-muted small">
                                            <i class="bi bi-calendar3 me-1"></i>10/01/2024 14:30:00
                                        </p>
                                        <p class="card-text mb-1 text-muted small">
                                            <i class="bi bi-geo-alt me-1"></i>São Paulo/SP
                                        </p>
                                        <p class="card-text mb-0 small">Assinado por: João Silva</p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                    
                    <!-- Evento 2 -->
                    <div class="timeline-item mb-4" style="position: relative;">
                        <!-- Linha vertical -->
                        <div style="position: absolute; left: -20px; top: 30px; width: 2px; height: calc(100% + 1rem); background-color: #dee2e6; z-index: 1;"></div>
                        
                        <div class="d-flex align-items-start">
                            <!-- Card (sem ícone) -->
                            <div class="flex-grow-1">
                                <div class="card">
                                    <div class="card-body p-3">
                                        <h6 class="card-title mb-1">Objeto em trânsito</h6>
                                        <p class="card-text mb-1 text-muted small">
                                            <i class="bi bi-calendar3 me-1"></i>09/01/2024 10:15:00
                                        </p>
                                        <p class="card-text mb-1 text-muted small">
                                            <i class="bi bi-geo-alt me-1"></i>Rio de Janeiro/RJ
                                        </p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                    
                    <!-- Evento 3 (Último - sem linha vertical) -->
                    <div class="timeline-item mb-4" style="position: relative;">
                        <div class="d-flex align-items-start">
                            <!-- Card (sem ícone) -->
                            <div class="flex-grow-1">
                                <div class="card">
                                    <div class="card-body p-3">
                                        <h6 class="card-title mb-1">Objeto postado</h6>
                                        <p class="card-text mb-1 text-muted small">
                                            <i class="bi bi-calendar3 me-1"></i>08/01/2024 09:00:00
                                        </p>
                                        <p class="card-text mb-1 text-muted small">
                                            <i class="bi bi-geo-alt me-1"></i>Brasília/DF
                                        </p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                    
                </div>
            </div>
        </div>
    </div>
</div>
```

## Implementação no Projeto Tracker

### Passo 1: Estrutura HTML do Modal

Crie o modal na sua view:

```html
<!-- Modal de Rastreamento -->
<div class="modal fade" id="modalRastreamento" tabindex="-1" aria-labelledby="modalRastreamentoLabel" aria-hidden="true">
    <div class="modal-dialog modal-lg modal-dialog-scrollable">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title d-flex align-items-center gap-2" id="modalRastreamentoLabel">
                    <img src="/images/track.png" alt="Rastreamento" style="width: 56px; height: auto;" />
                    Rastreamento de Encomenda
                </h5>
                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Fechar"></button>
            </div>
            <div class="modal-body" id="rastreamentoContent">
                <div class="text-center">
                    <div class="spinner-border text-primary" role="status">
                        <span class="visually-hidden">Carregando...</span>
                    </div>
                    <p class="mt-2">Carregando informações de rastreamento...</p>
                </div>
            </div>
        </div>
    </div>
</div>
```

### Passo 2: JavaScript para Gerar HTML

Crie a função JavaScript (pode ser em um arquivo `.js` separado ou inline):

```javascript
function abrirRastreamento(codigoRastreamento) {
    const modal = new bootstrap.Modal(document.getElementById('modalRastreamento'));
    const content = document.getElementById('rastreamentoContent');
    
    // Mostrar loading
    content.innerHTML = `
        <div class="text-center">
            <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Carregando...</span>
            </div>
            <p class="mt-2">Carregando informações de rastreamento...</p>
        </div>
    `;
    
    modal.show();
    
    // Buscar dados (ajuste a URL conforme seu endpoint)
    fetch('/api/rastreamento/' + encodeURIComponent(codigoRastreamento))
        .then(response => {
            if (!response.ok) {
                return response.json().then(err => { 
                    throw new Error(err.error || 'Erro ao buscar rastreamento'); 
                });
            }
            return response.json();
        })
        .then(data => {
            if (!data.objetos || data.objetos.length === 0) {
                content.innerHTML = '<div class="alert alert-warning">Nenhuma informação de rastreamento encontrada.</div>';
                return;
            }
            
            const objeto = data.objetos[0];
            const eventos = objeto.eventos || [];
            
            // Ordenar: mais recente primeiro
            eventos.sort((a, b) => new Date(b.dtHrCriado) - new Date(a.dtHrCriado));
            
            // Gerar HTML
            let html = `
                <div class="card mb-3">
                    <div class="card-body">
                        <h6 class="card-title mb-2">Informações do Objeto</h6>
                        <div class="row">
                            <div class="col-md-4">
                                <p class="mb-1"><strong>Código:</strong> ${objeto.codObjeto || 'N/A'}</p>
                            </div>
                            ${objeto.tipoPostal ? `<div class="col-md-4"><p class="mb-1"><strong>Tipo:</strong> ${objeto.tipoPostal.descricao || objeto.tipoPostal.sigla || 'N/A'}</p></div>` : ''}
                            ${objeto.dtPrevista ? `<div class="col-md-4"><p class="mb-1"><strong>Previsão:</strong> ${new Date(objeto.dtPrevista).toLocaleDateString('pt-BR')}</p></div>` : ''}
                        </div>
                    </div>
                </div>
                <h6 class="mb-3">Histórico de Rastreamento</h6>
                <div class="timeline-container" style="position: relative; padding-left: 55px;">
            `;
            
            eventos.forEach((evento, index) => {
                const isFirst = index === 0;
                const eventoDate = new Date(evento.dtHrCriado);
                const localizacao = evento.unidade?.endereco 
                    ? `${evento.unidade.endereco.cidade || ''}/${evento.unidade.endereco.uf || ''}`.replace(/^\/|\/$/g, '')
                    : '';
                
                html += `
                    <div class="timeline-item mb-4" style="position: relative;">
                        ${index < eventos.length - 1 ? '<div style="position: absolute; left: -20px; top: 30px; width: 2px; height: calc(100% + 1rem); background-color: #dee2e6; z-index: 1;"></div>' : ''}
                        <div class="d-flex align-items-start">
                            ${isFirst ? `<div style="position: absolute; left: -40px; top: 0; width: 48px; height: 48px; display: flex; align-items: center; justify-content: center; background-color: rgba(13, 110, 253, 0.1); border: 2px solid #0d6efd; border-radius: 50%; z-index: 10;">
                                <span style="font-size: 28px; line-height: 1;">🐟</span>
                            </div>` : ''}
                            <div class="flex-grow-1">
                                <div class="card ${isFirst ? 'border-primary' : ''}">
                                    <div class="card-body p-3">
                                        <h6 class="card-title mb-1 ${isFirst ? 'text-primary' : ''}">${evento.descricao || 'N/A'}</h6>
                                        <p class="card-text mb-1 text-muted small">
                                            <i class="bi bi-calendar3 me-1"></i>${eventoDate.toLocaleString('pt-BR')}
                                        </p>
                                        ${localizacao ? `<p class="card-text mb-1 text-muted small"><i class="bi bi-geo-alt me-1"></i>${localizacao}</p>` : ''}
                                        ${evento.detalhe ? `<p class="card-text mb-0 small">${evento.detalhe}</p>` : ''}
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                `;
            });
            
            html += `</div>`;
            content.innerHTML = html;
        })
        .catch(error => {
            content.innerHTML = `<div class="alert alert-danger">Erro ao carregar rastreamento: ${error.message}</div>`;
        });
}
```

### Passo 3: CSS (Opcional)

Adicione os estilos customizados:

```css
#modalRastreamento .modal-content {
    border-radius: 1rem;
}

#modalRastreamento .modal-header {
    border-top-left-radius: 1rem;
    border-top-right-radius: 1rem;
}

.airplane-tracking-btn {
    background: none;
    border: none;
}

.airplane-tracking-btn .track-icon {
    width: 40px;
    height: 40px;
    object-fit: contain;
}
```

### Passo 4: Chamar a Função

Para abrir o modal, chame a função com o código de rastreamento:

```html
<button onclick="abrirRastreamento('QN242960622BR')">
    Rastrear
</button>
```

Ou via JavaScript:

```javascript
document.getElementById('btnRastrear').addEventListener('click', function() {
    abrirRastreamento('QN242960622BR');
});
```

## Personalização

### Alterar Ícone do Evento Mais Recente

Substitua o emoji 🐟 por outro:

```javascript
<span style="font-size: 28px; line-height: 1;">📦</span>  // Pacote
<span style="font-size: 28px; line-height: 1;">🚚</span>  // Caminhão
<span style="font-size: 28px; line-height: 1;">✈️</span>  // Avião
```

Ou use um ícone do Bootstrap Icons:

```javascript
<i class="bi bi-box-seam" style="font-size: 24px;"></i>
```

### Alterar Cores

Modifique as cores do evento destacado:

```javascript
// Fundo azul claro → Verde claro
background-color: rgba(25, 135, 84, 0.1);  // Bootstrap success

// Borda azul → Verde
border: 2px solid #198754;  // Bootstrap success

// Texto azul → Verde
text-primary → text-success
border-primary → border-success
```

### Alterar Formato de Data

```javascript
// Formato padrão: dd/mm/yyyy hh:mm:ss
eventoDate.toLocaleString('pt-BR')

// Apenas data: dd/mm/yyyy
eventoDate.toLocaleDateString('pt-BR')

// Formato customizado
eventoDate.toLocaleString('pt-BR', { 
    day: '2-digit', 
    month: '2-digit', 
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
})
```

## Considerações Importantes

1. **Ordenação dos Eventos**: Os eventos devem ser ordenados do mais recente para o mais antigo (primeiro evento = mais recente)

2. **Ícone do Evento Mais Recente**: Apenas o primeiro evento (index 0) recebe o ícone destacado

3. **Linha Vertical**: Não é exibida no último evento da lista

4. **Localização**: Formatação `Cidade/UF`, removendo barras extras se algum campo estiver vazio

5. **Campos Opcionais**: 
   - `tipoPostal`: Pode ser null
   - `dtPrevista`: Pode ser null
   - `localizacao`: Pode estar vazio
   - `detalhe`: Pode estar vazio

6. **Responsividade**: O layout usa Bootstrap Grid (`col-md-4`) para responsividade

## Estrutura de Dados Esperada

A função espera receber um objeto JSON com a seguinte estrutura mínima:

```json
{
  "objetos": [
    {
      "codObjeto": "string",
      "tipoPostal": {
        "descricao": "string (opcional)",
        "sigla": "string (opcional)"
      },
      "dtPrevista": "datetime (opcional)",
      "eventos": [
        {
          "dtHrCriado": "datetime",
          "descricao": "string",
          "detalhe": "string (opcional)",
          "unidade": {
            "endereco": {
              "cidade": "string (opcional)",
              "uf": "string (opcional)"
            }
          }
        }
      ]
    }
  ]
}
```

## Conclusão

Esta documentação descreve completamente o formato HTML do modal de rastreamento, incluindo:
- Estrutura do modal Bootstrap
- Geração dinâmica do HTML via JavaScript
- Timeline visual com eventos
- Estilos e personalização
- Implementação passo a passo

Use esta documentação como referência para implementar o mesmo formato no projeto Tracker ou adaptá-lo conforme suas necessidades.

