# Tests - Scripts de Teste

## send_buslog.py

Script para enviar dados do `buslog.json` para a API de webhook da Aquanimal.

### Requisitos

- Python 3.6+
- Biblioteca `requests`: `pip install requests`

### Configuração

1. **Configurar variável de ambiente:**
   
   Linux/Mac:
   ```bash
   export buslog_token='seu_token_aqui'
   ```
   
   Windows (CMD):
   ```cmd
   set buslog_token=seu_token_aqui
   ```
   
   Windows (PowerShell):
   ```powershell
   $env:buslog_token='seu_token_aqui'
   ```

2. **Preparar arquivo JSON:**
   - Coloque o arquivo `buslog.json` no mesmo diretório do script
   - Ou forneça o caminho completo como argumento

### Uso

```bash
# Usar buslog.json no diretório atual
python3 send_buslog.py

# Especificar caminho do arquivo
python3 send_buslog.py /caminho/para/buslog.json
python3 send_buslog.py buslog.json
```

### Funcionamento

1. Lê o arquivo `buslog.json`
2. Obtém o token da variável de ambiente `buslog_token`
3. Envia POST para `https://aquanimal.com.br/apicom/webhook/track3rc`
4. Inclui o token no header da requisição
5. Envia o JSON no body da requisição

### Headers

O script tenta automaticamente múltiplos formatos de header (em ordem de prioridade):

1. **Header 'token'** (tentativa inicial):
   ```
   token: {token}
   ```

2. **Bearer Token**:
   ```
   Authorization: Bearer {token}
   ```

3. **Header customizado 'X-API-Token'**:
   ```
   X-API-Token: {token}
   ```

4. **Token direto no Authorization**:
   ```
   Authorization: {token}
   ```

O script para na primeira tentativa bem-sucedida (status 2xx). Se todos os formatos falharem, exibe uma mensagem de erro detalhada.

Se a API usar outro formato de header específico, edite o script na função `send_to_api()` na lista `header_formats`.

### Exemplo de Saída

```
============================================================
  AQUANIMAL - Enviar Buslog para API Webhook
============================================================

[STEP 1] Lendo arquivo JSON...
✅ Arquivo JSON lido com sucesso: buslog.json

[STEP 2] Obtendo token da variável de ambiente...
✅ Token obtido da variável de ambiente

[STEP 3] Enviando dados para a API...
📡 Enviando dados para: https://aquanimal.com.br/apicom/webhook/track3rc
📦 Tamanho do JSON: 1234 bytes
✅ Requisição enviada com sucesso!
📊 Status Code: 200
📄 Resposta da API:
{
  "status": "success",
  "message": "Dados recebidos com sucesso"
}
============================================================
✅ Concluído com sucesso!
```

### Tratamento de Erros

O script trata os seguintes erros:

- ❌ Arquivo não encontrado
- ❌ JSON inválido
- ❌ Token não configurado
- ❌ Erro de conexão
- ❌ Timeout
- ❌ Erro HTTP (401, 403, 400, etc.)

### Notas

- O script usa timeout de 30 segundos
- Tenta primeiro com Bearer token, depois com header 'token' se receber 401
- Se a API usar outro formato de header, modifique a função `send_to_api()`

