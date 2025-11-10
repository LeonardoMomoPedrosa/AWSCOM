# Documentação: Invalidação de Cache do E-commerce

## Visão Geral

O sistema de invalidação de cache permite que a aplicação gerencial (SLCOM) invalide caches específicos no e-commerce (SITECOM) quando dados são modificados. Isso garante que os usuários sempre visualizem informações atualizadas no site.

O sistema funciona através de chamadas HTTP para APIs do e-commerce, utilizando autenticação via Bearer Token (JWT) para segurança.

## Arquitetura

```
[SLCOM - Gerencial]  --HTTP POST-->  [SITECOM - E-commerce APIs]
     |                                        |
     |-- InvalidateAsync()                    |-- /apicom/cache/invalidate
     |-- GetTokenAsync()                      |-- /apicom/auth/loginger
     |                                        |
     |-- Redis Cache (Token)                  |-- Cache Regions
```

## Componentes Principais

### 1. SiteApiServices

Serviço responsável por comunicar-se com as APIs do e-commerce para invalidar cache.

**Localização**: `Services/SiteApiServices.cs`

**Interface**: `ISiteApiServices`

### 2. CacheInvalidateRequest

DTO que representa uma requisição de invalidação de cache.

```csharp
public class CacheInvalidateRequest
{
    public string Region { get; set; }        // Região do cache a ser invalidada
    public string Key { get; set; }           // Chave específica (opcional)
    public bool CleanRegionInd { get; set; }  // Se true, limpa toda a região
}
```

**Localização**: `DTO/Api/Request/CacheInvalidateRequest.cs`

### 3. SiteApiConfig

Configuração que contém as URLs dos servidores, credenciais e endpoints da API.

**Localização**: `Infrastructure/Config/SiteApiConfig.cs`

## Configuração

### 1. appsettings.json

```json
{
  "SiteApi": {
    "Servers": [
      {
        "BaseUrl": "http://localhost:5000/"
      },
      {
        "BaseUrl": "https://ecommerce-prod.example.com/"
      }
    ],
    "AuthPath": "apicom/auth/loginger",
    "InvalidateApi": "apicom/cache/invalidate",
    "ImageUploadApi": "apicom/imageupload/upload",
    "DestaqueImageUploadApi": "apicom/imageupload/uploaddestaque",
    "Username": "usuario_api",
    "Password": "senha_api",
    "TokenCacheMinutes": 50
  }
}
```

**Configurações**:
- `Servers`: Lista de servidores do e-commerce (permite múltiplos servidores para alta disponibilidade)
- `AuthPath`: Endpoint de autenticação
- `InvalidateApi`: Endpoint de invalidação de cache
- `Username/Password`: Credenciais para autenticação
- `TokenCacheMinutes`: Tempo de cache do token de autenticação (evita múltiplas chamadas de auth)

### 2. Program.cs

```csharp
// Configurar HttpClient para comunicação com APIs do e-commerce
builder.Services.AddHttpClient(Consts.SITE_CACHE_API, client =>
{
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Configurar SiteApiConfig
builder.Services.Configure<SiteApiConfig>(
    builder.Configuration.GetSection("SiteApi"));

// Registrar serviço
builder.Services.AddScoped<ISiteApiServices, SiteApiServices>();

// Configurar Redis para cache de token (opcional)
builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();
// Ou usar MemoryCache para desenvolvimento
builder.Services.AddSingleton<IRedisCacheService, MemoryCacheService>();
builder.Services.AddMemoryCache();
```

### 3. Consts.cs

```csharp
public class Consts
{
    public const string SITE_CACHE_API = "SITECACHEAPI";
}
```

## Como Funciona

### 1. Autenticação (GetTokenAsync)

O sistema utiliza autenticação Bearer Token para todas as chamadas à API:

1. **Verifica cache local**: Primeiro, verifica se existe um token válido em cache (Redis/MemoryCache)
2. **Autentica se necessário**: Se não houver token válido, faz POST para `/apicom/auth/loginger` com credenciais
3. **Armazena token**: Salva o token em cache pelo tempo especificado em `TokenCacheMinutes`
4. **Retorna token**: Retorna o token para uso nas requisições

**Fluxo de Autenticação**:
```
1. Verifica Redis: "SITE_AUTH_KEY"
2. Se não existe ou expirado:
   - POST /apicom/auth/loginger
   - Body: { "username": "...", "password": "..." }
   - Response: { "token": "...", "tokenType": "Bearer", "expiresIn": 3600 }
   - Salva em cache por TokenCacheMinutes
3. Retorna token
```

### 2. Invalidação de Cache (InvalidateAsync)

O método `InvalidateAsync` permite invalidar uma ou múltiplas regiões de cache:

**Assinatura**:
```csharp
Task<bool> InvalidateAsync(CacheInvalidateRequest request)
Task<bool> InvalidateAsync(IEnumerable<CacheInvalidateRequest> requests)
```

**Fluxo de Execução**:
1. Obtém token de autenticação (via `GetTokenAsync`)
2. Para cada servidor configurado em `SiteApiConfig.Servers`:
   - Cria HttpClient com BaseAddress do servidor
   - Adiciona header Authorization: `Bearer {token}`
   - Serializa lista de `CacheInvalidateRequest` para JSON
   - Faz POST para `{BaseUrl}/{InvalidateApi}`
   - Verifica status da resposta
3. Retorna `true` se todas as chamadas foram bem-sucedidas, `false` caso contrário

**Características**:
- **Múltiplos servidores**: Envia invalidação para todos os servidores configurados em paralelo
- **Paralelismo**: Usa `Task.WhenAll` para executar todas as requisições simultaneamente
- **Tolerância a falhas**: Se um servidor falhar, continua tentando os outros, mas retorna `false` no final

## Regiões de Cache

As regiões de cache são constantes definidas no projeto SITECOM (geralmente em `SiteCacheKeyUtil`). Algumas regiões comuns:

- `REGION_CATALOGPRODUCTLIST`: Lista de produtos do catálogo
- `REGION_PRODUCTDETAILS`: Detalhes de um produto específico
- `REGION_DRYCATEGORYMODELS`: Modelos de categorias (produtos secos)
- `REGION_FISHCATALOGDESC`: Descrição de catálogo (peixes)
- `REGION_DRYCATALOGDESC`: Descrição de catálogo (produtos secos)
- `REGION_MENU`: Menu de navegação
- `REGION_DESTAQUE`: Destaques/carrossel da homepage

## Exemplos de Uso

### Exemplo 1: Invalidar Cache de um Produto

Quando um produto é atualizado, é necessário invalidar:
- A lista de produtos do catálogo (para diferentes tipos)
- Os detalhes do produto específico

```csharp
public class ProductController : Controller
{
    private readonly ISiteApiServices _siteApi;

    [HttpPost]
    public async Task<IActionResult> UpdateProduct(ProductViewModel viewModel)
    {
        // ... atualiza produto no banco ...
        
        var cacheType = viewModel.SubSubTipo > 0 
            ? viewModel.SubSubTipo 
            : viewModel.SubTipo;

        var cacheInfo = new List<CacheInvalidateRequest>
        {
            // Invalidar lista de produtos (com e sem promoção)
            new() 
            { 
                Region = SiteCacheKeyUtil.REGION_CATALOGPRODUCTLIST, 
                Key = $"{cacheType}pTrue", 
                CleanRegionInd = false 
            },
            new() 
            { 
                Region = SiteCacheKeyUtil.REGION_CATALOGPRODUCTLIST, 
                Key = $"{cacheType}pFalse", 
                CleanRegionInd = false 
            },
            // Invalidar detalhes do produto
            new() 
            { 
                Region = SiteCacheKeyUtil.REGION_PRODUCTDETAILS, 
                Key = $"{viewModel.PKId}", 
                CleanRegionInd = false 
            }
        };

        // Se for produto seco, também invalidar categorias
        if (viewModel.SubSubTipo <= 0)
        {
            cacheInfo.Add(new CacheInvalidateRequest
            {
                Region = SiteCacheKeyUtil.REGION_DRYCATEGORYMODELS,
                Key = "",
                CleanRegionInd = true  // Limpa toda a região
            });
        }

        bool cacheSuccess = await _siteApi.InvalidateAsync(cacheInfo);
        
        return Json(new { 
            success = resModel.IsSuccess && cacheSuccess, 
            message = cacheSuccess ? "Produto atualizado" : "Erro ao atualizar cache" 
        });
    }
}
```

### Exemplo 2: Invalidar Cache de Categoria/Grupo

Quando uma categoria é atualizada:

```csharp
[HttpPatch]
public async Task<IActionResult> PatchGroup(ProductTypeModel model)
{
    // ... atualiza grupo no banco ...
    
    var cacheInfo = new List<CacheInvalidateRequest>
    {
        // Limpa todas as categorias
        new() 
        { 
            Region = SiteCacheKeyUtil.REGION_DRYCATEGORYMODELS, 
            Key = "", 
            CleanRegionInd = true 
        },
        // Invalida descrição específica do catálogo
        new() 
        { 
            Region = SiteCacheKeyUtil.REGION_DRYCATALOGDESC, 
            Key = $"{model.PKId}", 
            CleanRegionInd = false 
        }
    };

    bool cacheSuccess = await _siteCache.InvalidateAsync(cacheInfo);
    
    return Json(new { 
        success = resModel.IsSuccess && cacheSuccess, 
        message = cacheSuccess ? resModel.Message : "Erro ao atualizar cache" 
    });
}
```

### Exemplo 3: Invalidar Cache de Destaque/Carrossel

```csharp
private async Task InvalidateDestaqueCacheAsync()
{
    var cacheInfo = new List<CacheInvalidateRequest>
    {
        new() 
        { 
            Region = SiteCacheKeyUtil.REGION_DESTAQUE, 
            Key = "", 
            CleanRegionInd = true  // Limpa toda a região de destaques
        }
    };
    
    await _siteApi.InvalidateAsync(cacheInfo);
}

[HttpPost]
public async Task<IActionResult> UploadCarouselImage(int? id, IFormFile file, string? link)
{
    // ... upload de imagem e atualização no banco ...
    
    if (resModel.IsSuccess)
    {
        await InvalidateDestaqueCacheAsync();
    }
    
    return Json(new { success = resModel.IsSuccess, message = resModel.Message });
}
```

### Exemplo 4: Invalidar Cache Simples (Menu)

```csharp
[HttpDelete]
public async Task<IActionResult> DeleteGroup(int id)
{
    // ... deleta grupo no banco ...
    
    var cacheInfo = new CacheInvalidateRequest() 
    { 
        Region = SiteCacheKeyUtil.REGION_MENU, 
        Key = "", 
        CleanRegionInd = true 
    };
    
    bool cacheSuccess = await _siteCache.InvalidateAsync(cacheInfo);
    
    return Json(new { 
        success = resModel.IsSuccess, 
        message = resModel.Message 
    });
}
```

## Parâmetros de CacheInvalidateRequest

### Region (string)
- **Obrigatório**: Nome da região de cache a ser invalidada
- **Exemplos**: `REGION_CATALOGPRODUCTLIST`, `REGION_PRODUCTDETAILS`, `REGION_MENU`
- **Uso**: Define qual tipo de cache será invalidado

### Key (string)
- **Opcional**: Chave específica dentro da região
- **Exemplos**: ID do produto (`"123"`), tipo de cache (`"5pTrue"`)
- **Uso**: 
  - Se preenchido: invalida apenas essa chave específica
  - Se vazio (`""`) e `CleanRegionInd = true`: limpa toda a região
  - Se vazio e `CleanRegionInd = false`: comportamento depende da implementação no SITECOM

### CleanRegionInd (bool)
- **Padrão**: `false`
- **Uso**:
  - `true`: Limpa toda a região de cache (ignora `Key`)
  - `false`: Invalida apenas a chave específica (`Key`)

## Tratamento de Erros

### Validação de Resposta

O método `InvalidateAsync` retorna `bool`:
- `true`: Todas as requisições foram bem-sucedidas
- `false`: Pelo menos uma requisição falhou

### Exemplo de Tratamento

```csharp
bool cacheSuccess = await _siteApi.InvalidateAsync(cacheInfo);

if (!cacheSuccess)
{
    _logger.LogWarning("Falha na invalidação de cache. Produto ID: {ProductId}", productId);
    // Opcional: retry, notificação, etc.
}

bool isSuccess = dbOperationSuccess && cacheSuccess;
var message = cacheSuccess 
    ? "Operação realizada com sucesso" 
    : "Operação realizada, mas erro ao atualizar cache";
```

### Erros de Autenticação

Se a autenticação falhar, uma exceção será lançada:

```csharp
try
{
    await _siteApi.InvalidateAsync(cacheInfo);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Erro ao invalidar cache: {Message}", ex.Message);
    // Exceção pode ser: "Auth failed: 401 {response}"
}
```

## Múltiplos Servidores

O sistema suporta múltiplos servidores para alta disponibilidade:

```json
{
  "SiteApi": {
    "Servers": [
      { "BaseUrl": "https://ecommerce-server1.example.com/" },
      { "BaseUrl": "https://ecommerce-server2.example.com/" },
      { "BaseUrl": "https://ecommerce-server3.example.com/" }
    ]
  }
}
```

**Comportamento**:
- Todas as requisições são enviadas em paralelo para todos os servidores
- Se um servidor falhar, os outros continuam sendo processados
- O resultado final é `false` se qualquer servidor falhar

## Implementação no SITECOM/Personalize

Para implementar no projeto SITECOM/Personalize, você precisará:

### 1. Criar a Interface

```csharp
public interface ISiteApiServices
{
    Task<bool> InvalidateAsync(IEnumerable<CacheInvalidateRequest> requests);
    Task<bool> InvalidateAsync(CacheInvalidateRequest request);
}
```

### 2. Implementar o Serviço

```csharp
public class SiteApiServices : ISiteApiServices
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IRedisCacheService _redis;
    private readonly SiteApiConfig _siteConfig;

    public SiteApiServices(
        IHttpClientFactory httpFactory,
        IRedisCacheService redisCacheService,
        IOptions<SiteApiConfig> siteConfig)
    {
        _httpFactory = httpFactory;
        _redis = redisCacheService;
        _siteConfig = siteConfig.Value;
    }

    public async Task<bool> InvalidateAsync(CacheInvalidateRequest request)
    {
        return await InvalidateAsync(new[] { request });
    }

    public async Task<bool> InvalidateAsync(IEnumerable<CacheInvalidateRequest> requests)
    {
        var token = await GetTokenAsync();
        var retVal = true;

        var tasks = _siteConfig.Servers.Select(async server =>
        {
            var httpClient = _httpFactory.CreateClient(Consts.SITE_CACHE_API);
            httpClient.BaseAddress = new Uri(server.BaseUrl);
            httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);

            var body = JsonConvert.SerializeObject(requests);
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(_siteConfig.InvalidateApi, content);

            if (!response.IsSuccessStatusCode)
            {
                retVal = false;
            }
        });

        await Task.WhenAll(tasks);
        return retVal;
    }

    private async Task<string> GetTokenAsync()
    {
        var key = BaseCacheServices.GetSiteAuthKey();
        var cached = await _redis.GetCacheValueAsync<CacheAuthResponse>(key);
        
        if (cached != null && !string.IsNullOrEmpty(cached.Token))
        {
            return cached.Token;
        }

        var server = _siteConfig.Servers.First();
        var httpClient = _httpFactory.CreateClient(Consts.SITE_CACHE_API);
        httpClient.BaseAddress = new Uri(server.BaseUrl);

        var credentials = new 
        { 
            username = _siteConfig.Username, 
            password = _siteConfig.Password 
        };
        var payload = JsonConvert.SerializeObject(credentials);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(_siteConfig.AuthPath, content);
        var respStr = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Auth failed: {(int)response.StatusCode} {respStr}");
        }

        var auth = JsonConvert.DeserializeObject<CacheAuthResponse>(respStr);
        var ttl = _siteConfig.TokenCacheMinutes;
        await _redis.SetCacheValueAsync(key, auth, TimeSpan.FromMinutes(ttl));
        
        return auth.Token;
    }
}
```

### 3. Criar os DTOs

```csharp
// DTO/Api/Request/CacheInvalidateRequest.cs
public class CacheInvalidateRequest
{
    public string Region { get; set; }
    public string Key { get; set; }
    public bool CleanRegionInd { get; set; }
}

// DTO/Api/Response/CacheAuthResponse.cs
public class CacheAuthResponse
{
    public string Token { get; set; }
    public string TokenType { get; set; }
    public int ExpiresIn { get; set; }
}
```

### 4. Configurar no Program.cs

```csharp
// Configurar HttpClient
builder.Services.AddHttpClient(Consts.SITE_CACHE_API, client =>
{
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Configurar SiteApiConfig
builder.Services.Configure<SiteApiConfig>(
    builder.Configuration.GetSection("SiteApi"));

// Registrar serviço
builder.Services.AddScoped<ISiteApiServices, SiteApiServices>();

// Configurar cache (Redis ou MemoryCache)
builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();
// ou
builder.Services.AddSingleton<IRedisCacheService, MemoryCacheService>();
builder.Services.AddMemoryCache();
```

### 5. Configurar appsettings.json

```json
{
  "SiteApi": {
    "Servers": [
      {
        "BaseUrl": "https://ecommerce.example.com/"
      }
    ],
    "AuthPath": "apicom/auth/loginger",
    "InvalidateApi": "apicom/cache/invalidate",
    "Username": "usuario_api",
    "Password": "senha_api",
    "TokenCacheMinutes": 50
  }
}
```

## Boas Práticas

1. **Sempre invalidar cache após operações de escrita**: Create, Update, Delete
2. **Invalidar múltiplas regiões relacionadas**: Se atualizar um produto, invalidar lista e detalhes
3. **Usar CleanRegionInd com cuidado**: Limpar toda a região pode impactar performance
4. **Tratar erros adequadamente**: Logar falhas e considerar retry em casos críticos
5. **Validar sucesso da invalidação**: Verificar retorno `bool` e informar ao usuário se necessário
6. **Cache de token**: Evita múltiplas autenticações, melhora performance
7. **Múltiplos servidores**: Use para alta disponibilidade em produção
8. **Logging**: Adicione logs para debug e monitoramento

## Troubleshooting

### Problema: Token sempre expira
**Solução**: Verifique `TokenCacheMinutes` na configuração e o TTL do cache (Redis/MemoryCache)

### Problema: Invalidação não funciona
**Soluções**:
- Verifique se as URLs dos servidores estão corretas
- Verifique credenciais de autenticação
- Verifique se o endpoint `/apicom/cache/invalidate` existe no SITECOM
- Verifique logs de erro no SITECOM

### Problema: Performance lenta
**Soluções**:
- Verifique se está usando cache de token (Redis/MemoryCache)
- Considere aumentar `TokenCacheMinutes`
- Verifique latência de rede para servidores do e-commerce
- Considere processar invalidações de forma assíncrona (background jobs)

### Problema: Alguns servidores falham
**Soluções**:
- Verifique conectividade de rede
- Verifique se todos os servidores estão online
- Considere implementar retry logic para servidores que falharam
- Monitore logs para identificar padrões de falha

## Referências

- **Serviço**: `Services/SiteApiServices.cs`
- **Interface**: `Services/interfaces/ISiteApiServices.cs`
- **DTO Request**: `DTO/Api/Request/CacheInvalidateRequest.cs`
- **DTO Response**: `DTO/Api/Response/CacheAuthResponse.cs`
- **Configuração**: `Infrastructure/Config/SiteApiConfig.cs`
- **Exemplos de uso**: 
  - `Controllers/ProductController.cs`
  - `Controllers/GroupController.cs`
  - `Controllers/ContentController.cs`

