#!/usr/bin/env python3
"""
Script para enviar dados do buslog.json para a API de webhook
Uso: python3 send_buslog.py [caminho_do_buslog.json]
"""

import sys
import os
import json
import requests
from pathlib import Path


def read_json_file(file_path):
    """
    Lê o arquivo JSON e retorna o conteúdo
    """
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            data = json.load(f)
        print(f"✅ Arquivo JSON lido com sucesso: {file_path}")
        return data
    except FileNotFoundError:
        print(f"❌ ERRO: Arquivo não encontrado: {file_path}")
        sys.exit(1)
    except json.JSONDecodeError as e:
        print(f"❌ ERRO: Arquivo JSON inválido: {e}")
        sys.exit(1)
    except Exception as e:
        print(f"❌ ERRO ao ler arquivo: {str(e)}")
        sys.exit(1)


def get_token_from_env():
    """
    Lê o token da variável de ambiente buslog_token
    """
    token = os.getenv('buslog_token')
    if not token:
        print("❌ ERRO: Variável de ambiente 'buslog_token' não encontrada")
        print("\n💡 Como configurar:")
        print("   Linux/Mac: export buslog_token='seu_token_aqui'")
        print("   Windows: set buslog_token=seu_token_aqui")
        print("   Windows PowerShell: $env:buslog_token='seu_token_aqui'")
        sys.exit(1)
    
    print("✅ Token obtido da variável de ambiente")
    return token


def send_to_api(json_data, token):
    """
    Envia o JSON para a API via POST
    Tenta diferentes formatos de header com token
    """
    url = "https://aquanimal.com.br/apicom/webhook/track3rc"
    
    # Lista de formatos de header para tentar (em ordem de prioridade)
    header_formats = [
        {'token': token},  # Formato mais simples: header 'token'
        {'Authorization': f'Bearer {token}'},  # Bearer token
        {'X-API-Token': token},  # Header customizado
        {'Authorization': token},  # Token direto no Authorization
    ]
    
    try:
        print(f"📡 Enviando dados para: {url}")
        print(f"📦 Tamanho do JSON: {len(json.dumps(json_data))} bytes")
        print(f"🔑 Token: {token[:10]}...{token[-5:] if len(token) > 15 else token}")
        
        response = None
        last_error = None
        successful_response = None
        
        # Tentar cada formato de header
        for i, header_format in enumerate(header_formats, 1):
            headers = {
                'Content-Type': 'application/json',
                **header_format
            }
            
            header_name = list(header_format.keys())[0]
            print(f"🔄 Tentativa {i}/{len(header_formats)}: Header '{header_name}'")
            
            try:
                response = requests.post(
                    url,
                    json=json_data,
                    headers=headers,
                    timeout=30
                )
                
                # Se sucesso (2xx), parar de tentar
                if 200 <= response.status_code < 300:
                    print(f"✅ Requisição enviada com sucesso usando header '{header_name}'!")
                    successful_response = response
                    break
                elif response.status_code == 401:
                    print(f"   ⚠️  Não autorizado (401) com header '{header_name}'")
                    last_error = response
                    continue
                else:
                    # Outro erro, salvar mas continuar tentando
                    print(f"   ⚠️  Status {response.status_code} com header '{header_name}'")
                    last_error = response
                    continue
                    
            except requests.exceptions.RequestException as e:
                print(f"   ⚠️  Erro na tentativa {i}: {str(e)}")
                last_error = e
                continue
        
        # Se tivemos sucesso, processar resposta
        if successful_response is not None:
            response = successful_response
            print(f"\n📊 Status Code: {response.status_code}")
            
            # Tentar exibir resposta se for JSON
            try:
                response_json = response.json()
                print(f"📄 Resposta da API:")
                print(json.dumps(response_json, indent=2, ensure_ascii=False))
            except:
                response_text = response.text[:500]
                if response_text:
                    print(f"📄 Resposta da API (texto): {response_text}")
                else:
                    print(f"📄 Resposta da API: (vazia)")
            
            return True
        
        # Se não teve sucesso, tratar erro
        if isinstance(last_error, requests.Response):
            # Última resposta foi um erro HTTP
            error_response = last_error
            status_code = error_response.status_code
            error_text = error_response.text[:500] if error_response.text else "(vazia)"
            
            print(f"\n❌ ERRO HTTP: Status {status_code}")
            print(f"📄 Resposta: {error_text}")
            
            if status_code == 401:
                print("\n💡 Possíveis causas:")
                print("   - Token inválido ou expirado")
                print("   - Formato do header incorreto")
                print("   - Todos os formatos de header foram tentados sem sucesso")
            elif status_code == 403:
                print("\n💡 Possíveis causas:")
                print("   - Token sem permissão para acessar a API")
            elif status_code == 400:
                print("\n💡 Possíveis causas:")
                print("   - JSON inválido ou formato incorreto")
                print("   - Dados obrigatórios faltando")
            elif status_code == 404:
                print("\n💡 Possíveis causas:")
                print("   - URL da API incorreta")
                print("   - Endpoint não encontrado")
            elif status_code == 500:
                print("\n💡 Possíveis causas:")
                print("   - Erro interno do servidor")
                print("   - Problema temporário na API")
            
            return False
            
        elif last_error is not None:
            # Foi uma exceção de conexão/timeout
            if isinstance(last_error, requests.exceptions.Timeout):
                print("\n❌ ERRO: Timeout ao conectar com a API")
                print("   A requisição demorou mais de 30 segundos")
                return False
            elif isinstance(last_error, requests.exceptions.ConnectionError):
                print("\n❌ ERRO: Não foi possível conectar com a API")
                print("   Verifique sua conexão com a internet")
                return False
            else:
                # Outra exceção
                raise last_error
        else:
            print("\n❌ ERRO: Todas as tentativas falharam sem resposta")
            return False
        
    except requests.exceptions.Timeout:
        print("\n❌ ERRO: Timeout ao conectar com a API")
        print("   A requisição demorou mais de 30 segundos")
        return False
    except requests.exceptions.ConnectionError:
        print("\n❌ ERRO: Não foi possível conectar com a API")
        print("   Verifique sua conexão com a internet")
        return False
    except Exception as e:
        print(f"\n❌ ERRO inesperado: {str(e)}")
        import traceback
        traceback.print_exc()
        return False


def main():
    """
    Função principal
    """
    print("=" * 60)
    print("  AQUANIMAL - Enviar Buslog para API Webhook")
    print("=" * 60)
    
    # Determinar caminho do arquivo JSON
    if len(sys.argv) > 1:
        json_file_path = sys.argv[1]
    else:
        # Tentar encontrar buslog.json no diretório atual
        json_file_path = "buslog.json"
        if not os.path.exists(json_file_path):
            # Tentar no diretório do script
            script_dir = Path(__file__).parent
            json_file_path = script_dir / "buslog.json"
            if not json_file_path.exists():
                print("\n❌ Arquivo buslog.json não encontrado!")
                print("\nUso correto:")
                print(f"  python3 {sys.argv[0]} [caminho_do_buslog.json]")
                print("\nExemplos:")
                print(f"  python3 {sys.argv[0]} buslog.json")
                print(f"  python3 {sys.argv[0]} /caminho/para/buslog.json")
                print("\nOu coloque o arquivo buslog.json no mesmo diretório do script")
                sys.exit(1)
    
    json_file_path = str(json_file_path)
    
    # 1. Ler arquivo JSON
    print(f"\n[STEP 1] Lendo arquivo JSON...")
    json_data = read_json_file(json_file_path)
    
    # 2. Obter token da variável de ambiente
    print(f"\n[STEP 2] Obtendo token da variável de ambiente...")
    token = get_token_from_env()
    
    # 3. Enviar para a API
    print(f"\n[STEP 3] Enviando dados para a API...")
    success = send_to_api(json_data, token)
    
    print("=" * 60)
    
    if success:
        print("✅ Concluído com sucesso!")
        sys.exit(0)
    else:
        print("❌ Falha ao enviar dados para a API")
        sys.exit(1)


if __name__ == "__main__":
    main()

