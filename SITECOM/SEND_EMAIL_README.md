# 📧 Script de Envio de Email - Aquanimal

Script Python3 para enviar emails de boas-vindas via AWS SES para usuários com problemas no cadastro.

## 🚀 Uso Rápido

```bash
# Enviar email para um usuário
python3 send_welcome_email.py usuario@email.com

# Exemplos
python3 send_welcome_email.py joao.silva@gmail.com
python3 send_welcome_email.py maria@hotmail.com
```

## 📋 Pré-requisitos

1. **Python 3** instalado
2. **boto3** instalado:
   ```bash
   pip3 install boto3
   # ou
   python3 -m pip install boto3
   ```
3. **Credenciais AWS** configuradas (IAM role com permissão `ses:SendEmail`)

## 📝 O que o script faz?

- ✅ Envia email de **aquanimal@aquanimal.com.br**
- ✅ Assunto: **"Seu cadastro Aquanimal"**
- ✅ Corpo: Mensagem sobre problema técnico e cadastro ativo
- ✅ Formato HTML + texto plano
- ✅ **BCC automático para pedrosa.leonardo@gmail.com** (cópia oculta)
- ✅ Validação básica de email

## 🔧 Conteúdo do Email

**Assunto:** Seu cadastro Aquanimal

**Mensagem:**
> Olá!
> 
> Tivemos um problema técnico hoje e alguns cadastros não foram concluídos com sucesso.
> 
> Gostaríamos de informar que no momento o sistema já está normalizado.
> 
> Seu cadastro encontra-se ativo e pronto para uso!
> 
> Estamos felizes em ter você conosco! 🎉

## 📦 Uso no Bastion

```bash
# 1. Copiar script para o bastion
scp send_welcome_email.py ec2-user@bastion:~/

# 2. No bastion, dar permissão de execução
chmod +x send_welcome_email.py

# 3. Instalar boto3 (se necessário)
pip3 install boto3 --user

# 4. Enviar email
python3 send_welcome_email.py email@example.com
```

## 📊 Output Esperado

```
==================================================
  AQUANIMAL - Email de Boas-Vindas
==================================================
📧 Enviando email para: joao@gmail.com
📤 Remetente: aquanimal@aquanimal.com.br
📨 BCC: Habilitado (cópia oculta para administrador)
🌎 Região: us-east-1
--------------------------------------------------
✅ Email enviado com sucesso!
📬 MessageId: 010001234567890-abcdef...
🆔 RequestId: abc123...
==================================================
✅ Concluído com sucesso!
```

## ❌ Troubleshooting

### Erro: "AccessDenied"
```
IAM role sem permissão ses:SendEmail
```
**Solução:** Adicionar policy `AmazonSESFullAccess` ou inline policy com `ses:SendEmail`

### Erro: "MessageRejected"
```
Email de destino inválido ou não verificado
```
**Solução:** 
- Verificar se o email é válido
- Se SES estiver em sandbox mode, o email de destino precisa ser verificado

### Erro: "Module not found: boto3"
```
pip3 install boto3 --user
```

## 🎯 Casos de Uso

- ✅ Recuperar cadastros com falha
- ✅ Reenviar emails de boas-vindas
- ✅ Notificar usuários sobre correções
- ✅ Testes de envio de email

## 🔐 Permissões Necessárias (IAM)

```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "ses:SendEmail",
                "ses:SendRawEmail"
            ],
            "Resource": "*"
        }
    ]
}
```

## 📞 Suporte

Em caso de dúvidas, verificar:
1. Credenciais AWS configuradas corretamente
2. IAM role com permissão para SES
3. Email remetente verificado no SES (aquanimal@aquanimal.com.br)
4. Região correta (us-east-1)

