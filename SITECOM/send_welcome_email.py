#!/usr/bin/env python3
"""
Script para enviar email de boas-vindas via AWS SES
Uso: python3 send_welcome_email.py email@example.com
"""

import sys
import boto3
from botocore.exceptions import ClientError


def send_welcome_email(recipient_email):
    """
    Envia email de boas-vindas para usuário com problemas no cadastro
    """
    # Configuração
    SENDER = "aquanimal@aquanimal.com.br"
    SUBJECT = "Seu cadastro Aquanimal"
    REGION = "us-east-1"
    
    # Corpo do email em HTML
    BODY_HTML = f"""
    <html>
    <head></head>
    <body>
        <h2>Olá!</h2>
        <p>Tivemos um problema técnico hoje e alguns cadastros não foram concluídos com sucesso.</p>
        <p>Gostaríamos de informar que <strong>no momento o sistema já está normalizado</strong>.</p>
        <p><strong>Seu cadastro encontra-se ativo e pronto para uso!</strong></p>
        <p>Estamos felizes em ter você conosco! 🎉</p>
        <br>
        <p>Atenciosamente,<br>
        <strong>Equipe Aquanimal</strong></p>
        <p><a href='https://aquanimal.com.br'>aquanimal.com.br</a></p>
        <hr>
        <p style='font-size: 12px; color: #666;'>
            Este é um email automático. Por favor, não responda.
        </p>
    </body>
    </html>
    """
    
    # Corpo do email em texto plano (fallback)
    BODY_TEXT = f"""
Olá!

Tivemos um problema técnico hoje e alguns cadastros não foram concluídos com sucesso.

Gostaríamos de informar que no momento o sistema já está normalizado.

Seu cadastro encontra-se ativo e pronto para uso!

Estamos felizes em ter você conosco!

Atenciosamente,
Equipe Aquanimal

aquanimal.com.br

---
Este é um email automático. Por favor, não responda.
    """
    
    # Criar cliente SES
    client = boto3.client('ses', region_name=REGION)
    
    try:
        print(f"📧 Enviando email para: {recipient_email}")
        print(f"📤 Remetente: {SENDER}")
        print(f"📨 BCC: Habilitado (cópia oculta para administrador)")
        print(f"🌎 Região: {REGION}")
        print("-" * 50)
        
        # Enviar email
        response = client.send_email(
            Source=SENDER,
            Destination={
                'ToAddresses': [recipient_email],
                'BccAddresses': ['pedrosa.leonardo@gmail.com']
            },
            Message={
                'Subject': {
                    'Data': SUBJECT,
                    'Charset': 'UTF-8'
                },
                'Body': {
                    'Text': {
                        'Data': BODY_TEXT,
                        'Charset': 'UTF-8'
                    },
                    'Html': {
                        'Data': BODY_HTML,
                        'Charset': 'UTF-8'
                    }
                }
            }
        )
        
        print(f"✅ Email enviado com sucesso!")
        print(f"📬 MessageId: {response['MessageId']}")
        print(f"🆔 RequestId: {response['ResponseMetadata']['RequestId']}")
        return True
        
    except ClientError as e:
        error_code = e.response['Error']['Code']
        error_message = e.response['Error']['Message']
        
        print(f"❌ ERRO ao enviar email:")
        print(f"   Código: {error_code}")
        print(f"   Mensagem: {error_message}")
        
        if error_code == 'MessageRejected':
            print("\n💡 Possíveis causas:")
            print("   - Email de destino inválido")
            print("   - Email não verificado no SES (sandbox mode)")
        elif error_code == 'AccessDenied':
            print("\n💡 Possíveis causas:")
            print("   - IAM role sem permissão ses:SendEmail")
            print("   - Credenciais AWS não configuradas")
        
        return False
        
    except Exception as e:
        print(f"❌ ERRO inesperado: {str(e)}")
        return False


def main():
    """
    Função principal
    """
    print("=" * 50)
    print("  AQUANIMAL - Email de Boas-Vindas")
    print("=" * 50)
    
    # Validar argumentos
    if len(sys.argv) != 2:
        print("\n❌ Uso incorreto!\n")
        print("Uso correto:")
        print(f"  python3 {sys.argv[0]} email@example.com\n")
        print("Exemplos:")
        print(f"  python3 {sys.argv[0]} joao@gmail.com")
        print(f"  python3 {sys.argv[0]} maria.silva@hotmail.com")
        print()
        sys.exit(1)
    
    recipient_email = sys.argv[1].strip()
    
    # Validação básica de email
    if '@' not in recipient_email or '.' not in recipient_email:
        print(f"\n❌ Email inválido: {recipient_email}")
        print("   Por favor, forneça um email válido.\n")
        sys.exit(1)
    
    # Enviar email
    success = send_welcome_email(recipient_email)
    
    print("=" * 50)
    
    if success:
        print("✅ Concluído com sucesso!")
        sys.exit(0)
    else:
        print("❌ Falha ao enviar email")
        sys.exit(1)


if __name__ == "__main__":
    main()

