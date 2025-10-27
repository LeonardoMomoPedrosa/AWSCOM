# Problema de Timeout com Secrets Manager

## 🔴 Problema Identificado

A Lambda não consegue acessar o Secrets Manager (timeout de 15 segundos).

## 🔍 Causas Prováveis

### 1. Lambda em VPC SEM acesso à internet

Se a Lambda está em VPC (necessário para acessar o banco RDS), ela **perde acesso à internet pública**. Isso causa timeout ao tentar acessar o Secrets Manager.

## ✅ Soluções

### **Opção 1: VPC Endpoint para Secrets Manager** (Recomendado)

1. Acesse o VPC Console
2. **Endpoints** → **Create Endpoint**
3. Configure:
   - **Service category**: AWS services
   - **Service name**: `com.amazonaws.us-east-1.secretsmanager` (para região us-east-1)
   - **VPC**: Mesma VPC da Lambda
   - **Subnets**: Selecione as subnets onde a Lambda está
   - **Security groups**: Use o mesmo Security Group da Lambda ou crie um novo
4. Clique em **Create endpoint**
5. Aguarde a criação

### **Opção 2: NAT Gateway** (Mais caro)

1. Acesse o VPC Console
2. **NAT Gateways** → **Create NAT Gateway**
3. Configure com uma Elastic IP
4. Configure a Route Table para rotear tráfego de saída pelo NAT Gateway

## 🚀 Como Verificar

Após configurar:
1. Faça upload do novo ZIP
2. Teste a função
3. Verifique os logs para ver se consegue acessar o Secrets Manager

## 📚 Mais Informações

- VPC Endpoints são gratuitos para Secrets Manager (com algumas limitações de taxa)
- NAT Gateway custa ~$32/mês + custos de tráfego
- VPC Endpoint é a solução mais simples e econômica

