# 🔐 Configuração de SSH para GitHub Actions

## ℹ️ Nota: Chave SSH já configurada

Se você já tem outros pipelines (como SLCOM) funcionando com a mesma chave SSH, 
**NÃO precisa criar uma nova chave**. Use a mesma configuração.

## ❌ Problema: `ssh: unable to authenticate`

Este erro pode acontecer quando há diferença de versão ou configuração entre workflows.

---

## ✅ Solução se já tem chave funcionando

Se outros pipelines funcionam (ex: SLCOM), **use a mesma versão da action**:

```yaml
uses: appleboy/scp-action@v0.1.4  # Mesma versão que funciona
```

---

## ✅ Solução Completa (se precisar criar nova chave)

### **Passo 1: Criar par de chaves SSH no Bastion**

```bash
# Conectar no Bastion
ssh -i sua-chave.pem ec2-user@seu-bastion-ip

# Criar chave dedicada para GitHub Actions
cd ~/.ssh
ssh-keygen -t rsa -b 4096 -C "github-actions-deploy" -f github_actions_key -N ""

# Isso cria 2 arquivos:
# - github_actions_key      (chave PRIVADA - para o GitHub)
# - github_actions_key.pub  (chave PÚBLICA - fica no servidor)
```

---

### **Passo 2: Configurar chave no Bastion**

```bash
# Adicionar chave pública ao authorized_keys
cat ~/.ssh/github_actions_key.pub >> ~/.ssh/authorized_keys

# Ajustar permissões (IMPORTANTE!)
chmod 600 ~/.ssh/authorized_keys
chmod 700 ~/.ssh
chmod 600 ~/.ssh/github_actions_key
chmod 644 ~/.ssh/github_actions_key.pub

# Verificar se está OK
ls -la ~/.ssh/
```

Deve aparecer assim:
```
drwx------  2 ec2-user ec2-user   ...  .
-rw-------  1 ec2-user ec2-user   ...  authorized_keys
-rw-------  1 ec2-user ec2-user   ...  github_actions_key
-rw-r--r--  1 ec2-user ec2-user   ...  github_actions_key.pub
```

---

### **Passo 3: Copiar chave PRIVADA**

```bash
# Mostrar chave privada
cat ~/.ssh/github_actions_key
```

**Copie TODO o conteúdo**, incluindo:
```
-----BEGIN OPENSSH PRIVATE KEY-----
b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAACFwAAAA
... (várias linhas) ...
-----END OPENSSH PRIVATE KEY-----
```

⚠️ **ATENÇÃO:**
- Copiar TUDO desde `-----BEGIN` até `-----END`
- Incluir as linhas BEGIN e END
- Não adicionar espaços ou quebras extras

---

### **Passo 4: Adicionar Secret no GitHub**

**GitHub → Repositório → Settings → Secrets and variables → Actions**

Clique em **New repository secret**:

| Nome | Valor |
|------|-------|
| `BASTION_SSH_KEY` | Cole TODA a chave privada |
| `BASTION_HOST` | IP do Bastion (ex: `54.123.45.67`) |
| `BASTION_USERNAME` | `ec2-user` |
| `AWS_ACCESS_KEY_ID` | AKIA... |
| `AWS_SECRET_ACCESS_KEY` | ... |
| `AWS_REGION` | `us-east-1` |
| `BASTION_SG_ID` | `sg-xxxxx` |

---

### **Passo 5: Testar conexão localmente**

**No seu PC Windows:**

```powershell
# Salvar chave em arquivo temporário
$chavePrivada = @"
-----BEGIN OPENSSH PRIVATE KEY-----
(cole aqui)
-----END OPENSSH PRIVATE KEY-----
"@

$chavePrivada | Out-File -FilePath "temp_key" -Encoding ASCII -NoNewline

# Testar conexão
ssh -i temp_key ec2-user@seu-bastion-ip

# Se conectar, a chave está OK!
# Depois apagar o arquivo:
Remove-Item temp_key
```

---

### **Passo 6: Executar workflow novamente**

Com os secrets configurados:

```bash
git add .
git commit -m "Fix SSH authentication"
git push origin main
```

Ou executar manualmente:
**GitHub → Actions → Build AvisemeEmailer → Run workflow**

---

## 🔍 Troubleshooting

### **Erro: Permission denied (publickey)**

**Causa:** Chave pública não está no `authorized_keys`

**Solução:**
```bash
# No Bastion
cat ~/.ssh/github_actions_key.pub >> ~/.ssh/authorized_keys
chmod 600 ~/.ssh/authorized_keys
```

---

### **Erro: Bad permissions**

**Causa:** Permissões incorretas

**Solução:**
```bash
# No Bastion
chmod 700 ~/.ssh
chmod 600 ~/.ssh/authorized_keys
chmod 600 ~/.ssh/github_actions_key
```

---

### **Erro: Host key verification failed**

**Causa:** Primeira conexão, servidor desconhecido

**Solução:** Adicione `StrictHostKeyChecking=no` (já está no workflow)

---

### **Testar se o Bastion aceita a chave:**

```bash
# Do seu PC
ssh -i caminho/para/chave ec2-user@bastion-ip "echo 'Conexão OK!'"

# Se funcionar, o problema é na configuração do Secret
```

---

## 📝 Checklist Final

- [ ] Chave SSH criada no Bastion
- [ ] Chave pública em `~/.ssh/authorized_keys`
- [ ] Permissões corretas (700/600)
- [ ] Secret `BASTION_SSH_KEY` configurado no GitHub
- [ ] Chave privada COMPLETA (com BEGIN/END)
- [ ] Outros secrets configurados (HOST, USERNAME, etc)
- [ ] Porta 22 aberta no Security Group (workflow faz isso)
- [ ] Testado localmente (opcional)

---

## 🆘 Ainda não funciona?

Verifique:

1. **No GitHub Actions → Logs:**
   - Ver mensagem de erro completa
   - Procurar por "permission denied", "timeout", etc

2. **No Bastion:**
   ```bash
   # Ver logs de autenticação
   sudo tail -f /var/log/secure
   # (enquanto roda o workflow)
   ```

3. **Security Group:**
   - Porta 22 aberta para 0.0.0.0/0 (workflow abre/fecha)
   - Ou adicionar IP do GitHub Actions permanentemente

4. **Formato da chave:**
   - Deve ser OpenSSH format
   - Se for PEM, converter:
     ```bash
     ssh-keygen -p -m PEM -f sua-chave
     ```

---

## ✅ Após configurar

O workflow deve:
1. ✅ Abrir porta SSH
2. ✅ Build
3. ✅ Transfer ZIP → Bastion
4. ✅ Deploy em ~/AVISEME
5. ✅ Fechar porta SSH

Pronto! 🚀

