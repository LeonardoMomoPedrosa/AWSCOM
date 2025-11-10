# Documentação: Adaptando build.yml para Projetos Sem SLCOMLIB

## Visão Geral

O projeto SLCOM utiliza uma dependência de projeto (ProjectReference) para o **SLCOMLIB**, que é um repositório separado. No GitHub Actions (`build.yml`), isso requer fazer checkout de dois repositórios. Esta documentação explica como adaptar o `build.yml` para diferentes cenários de dependência.

## Configuração Atual (Com SLCOMLIB)

### build.yml Atual

```yaml
build:
  name: Build (.NET 8)
  runs-on: ubuntu-latest
  needs: pre-actions
  steps:
    - name: Checkout do SLCOM
      uses: actions/checkout@v4
      with:
        repository: LeonardoMomoPedrosa/SLCOM
        token: ${{ secrets.GH_PAT }}
        path: SLCOM

    - name: Checkout do SLCOMLIB
      uses: actions/checkout@v4
      with:
        repository: LeonardoMomoPedrosa/SLCOMLIB
        token: ${{ secrets.GH_PAT }}
        path: SLCOMLIB

    - name: Instalar .NET 8 SDK
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'

    - name: Restaurar dependências
      working-directory: SLCOM
      run: dotnet restore SLCOM.sln

    - name: Compilar o projeto
      working-directory: SLCOM
      run: dotnet build SLCOM.sln --configuration Release --no-restore

    - name: Publicar (gera o executável pronto)
      working-directory: SLCOM
      run: dotnet publish SLCOM.csproj -c Release -r win-x64 -o ../publish --self-contained false
```

### Estrutura de Diretórios Após Checkout

```
workspace/
├── SLCOM/
│   ├── SLCOM.csproj
│   ├── SLCOM.sln
│   └── ...
└── SLCOMLIB/
    ├── SLCOMLIB.csproj
    └── ...
```

**Nota**: O `SLCOM.csproj` referencia `../SLCOMLIB/SLCOMLIB.csproj`, por isso ambos precisam estar no mesmo nível no diretório de trabalho.

## Cenários de Adaptação

### Cenário 1: Projeto Sem Dependência Externa (Sem SLCOMLIB)

Se o projeto não possui dependência do SLCOMLIB (tudo está no mesmo repositório), remova o passo de checkout do SLCOMLIB.

#### build.yml Adaptado

```yaml
build:
  name: Build (.NET 8)
  runs-on: ubuntu-latest
  needs: pre-actions
  steps:
    - name: Checkout do projeto
      uses: actions/checkout@v4
      with:
        repository: SeuUsuario/SeuProjeto
        token: ${{ secrets.GH_PAT }}

    - name: Instalar .NET 8 SDK
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'

    - name: Restaurar dependências
      run: dotnet restore SLCOM.sln

    - name: Compilar o projeto
      run: dotnet build SLCOM.sln --configuration Release --no-restore

    - name: Publicar (gera o executável pronto)
      run: dotnet publish SLCOM.csproj -c Release -r win-x64 -o ./publish --self-contained false

    - name: Remover appsettings de desenvolvimento
      run: |
        if [ -f "publish/appsettings.Development.json" ]; then
          rm -f publish/appsettings.Development.json
        fi

    - name: Compactar o resultado
      run: |
        cd publish
        zip -r ../slcom.zip .

    - name: Armazenar artefato no GitHub
      uses: actions/upload-artifact@v4
      with:
        name: SLCOM
        path: slcom.zip
```

#### Mudanças Principais

1. **Removido**: Passo de checkout do SLCOMLIB
2. **Removido**: Parâmetro `path: SLCOM` (checkout direto na raiz)
3. **Ajustado**: `working-directory: SLCOM` removido dos comandos
4. **Ajustado**: Caminho de publicação de `../publish` para `./publish`

### Cenário 2: Projeto com SLCOMLIB no Mesmo Repositório

Se o SLCOMLIB está no mesmo repositório (como subpasta), não é necessário fazer checkout separado.

#### Estrutura do Repositório

```
SeuRepositorio/
├── SLCOM/
│   ├── SLCOM.csproj
│   ├── SLCOM.sln
│   └── ...
└── SLCOMLIB/
    ├── SLCOMLIB.csproj
    └── ...
```

#### build.yml Adaptado

```yaml
build:
  name: Build (.NET 8)
  runs-on: ubuntu-latest
  needs: pre-actions
  steps:
    - name: Checkout do projeto
      uses: actions/checkout@v4
      with:
        repository: SeuUsuario/SeuRepositorio
        token: ${{ secrets.GH_PAT }}

    - name: Instalar .NET 8 SDK
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'

    - name: Restaurar dependências
      working-directory: SLCOM
      run: dotnet restore SLCOM.sln

    - name: Compilar o projeto
      working-directory: SLCOM
      run: dotnet build SLCOM.sln --configuration Release --no-restore

    - name: Publicar (gera o executável pronto)
      working-directory: SLCOM
      run: dotnet publish SLCOM.csproj -c Release -r win-x64 -o ../publish --self-contained false
```

#### Mudanças Principais

1. **Removido**: Passo de checkout do SLCOMLIB (já está no repositório)
2. **Mantido**: `working-directory: SLCOM` (se o projeto está em subpasta)
3. **Ajustado**: Caminho de publicação `../publish` (relativo ao SLCOM)

### Cenário 3: Projeto com Git Submodule

Se o SLCOMLIB é um Git Submodule, use a opção `submodules: recursive` no checkout.

#### build.yml Adaptado

```yaml
build:
  name: Build (.NET 8)
  runs-on: ubuntu-latest
  needs: pre-actions
  steps:
    - name: Checkout com submodules
      uses: actions/checkout@v4
      with:
        repository: SeuUsuario/SeuProjeto
        token: ${{ secrets.GH_PAT }}
        submodules: recursive

    - name: Instalar .NET 8 SDK
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'

    - name: Restaurar dependências
      working-directory: SLCOM
      run: dotnet restore SLCOM.sln

    - name: Compilar o projeto
      working-directory: SLCOM
      run: dotnet build SLCOM.sln --configuration Release --no-restore

    - name: Publicar (gera o executável pronto)
      working-directory: SLCOM
      run: dotnet publish SLCOM.csproj -c Release -r win-x64 -o ../publish --self-contained false
```

#### Mudanças Principais

1. **Adicionado**: `submodules: recursive` no checkout
2. **Removido**: Passo de checkout separado do SLCOMLIB
3. **Mantido**: Estrutura de diretórios e caminhos relativos

### Cenário 4: Projeto com Dependência NuGet

Se o SLCOMLIB é um pacote NuGet, configure a fonte NuGet antes de restaurar.

#### build.yml Adaptado

```yaml
build:
  name: Build (.NET 8)
  runs-on: ubuntu-latest
  needs: pre-actions
  steps:
    - name: Checkout do projeto
      uses: actions/checkout@v4
      with:
        repository: SeuUsuario/SeuProjeto
        token: ${{ secrets.GH_PAT }}

    - name: Instalar .NET 8 SDK
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'

    - name: Configurar fonte NuGet (se feed privado)
      run: |
        # Para NuGet público (não é necessário, já está configurado por padrão)
        # dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
        
        # Para feed privado (exemplo)
        # dotnet nuget add source https://seu-feed.com/index.json -n privado \
        #   --username ${{ secrets.NUGET_USER }} \
        #   --password ${{ secrets.NUGET_PASS }}

    - name: Restaurar dependências
      working-directory: SLCOM
      run: dotnet restore SLCOM.sln

    - name: Compilar o projeto
      working-directory: SLCOM
      run: dotnet build SLCOM.sln --configuration Release --no-restore

    - name: Publicar (gera o executável pronto)
      working-directory: SLCOM
      run: dotnet publish SLCOM.csproj -c Release -r win-x64 -o ../publish --self-contained false
```

#### Mudanças Principais

1. **Removido**: Passo de checkout do SLCOMLIB
2. **Adicionado**: Passo opcional para configurar fonte NuGet (apenas se necessário)
3. **Mantido**: Estrutura de diretórios e caminhos relativos

### Cenário 5: Múltiplos Repositórios com Paths Personalizados

Se você precisa fazer checkout de múltiplos repositórios, mas com estrutura diferente.

#### build.yml Adaptado

```yaml
build:
  name: Build (.NET 8)
  runs-on: ubuntu-latest
  needs: pre-actions
  steps:
    - name: Checkout do SLCOM
      uses: actions/checkout@v4
      with:
        repository: SeuUsuario/SLCOM
        token: ${{ secrets.GH_PAT }}
        path: src/SLCOM

    - name: Checkout do SLCOMLIB
      uses: actions/checkout@v4
      with:
        repository: SeuUsuario/SLCOMLIB
        token: ${{ secrets.GH_PAT }}
        path: src/SLCOMLIB

    - name: Instalar .NET 8 SDK
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'

    - name: Restaurar dependências
      working-directory: src/SLCOM
      run: dotnet restore SLCOM.sln

    - name: Compilar o projeto
      working-directory: src/SLCOM
      run: dotnet build SLCOM.sln --configuration Release --no-restore

    - name: Publicar (gera o executável pronto)
      working-directory: src/SLCOM
      run: dotnet publish SLCOM.csproj -c Release -r win-x64 -o ../../publish --self-contained false
```

#### Mudanças Principais

1. **Ajustado**: Paths personalizados (`src/SLCOM`, `src/SLCOMLIB`)
2. **Ajustado**: `working-directory` para refletir os novos paths
3. **Ajustado**: Caminho de publicação relativo aos novos paths

## Comparação de Cenários

| Cenário | Checkout SLCOMLIB | Submodules | NuGet Config | Path Customizado |
|---------|-------------------|------------|--------------|------------------|
| **Atual (2 repositórios)** | ✅ Sim | ❌ Não | ❌ Não | ❌ Não |
| **Sem SLCOMLIB** | ❌ Não | ❌ Não | ❌ Não | ❌ Não |
| **Mesmo repositório** | ❌ Não | ❌ Não | ❌ Não | ❌ Não |
| **Git Submodule** | ❌ Não | ✅ Sim | ❌ Não | ❌ Não |
| **NuGet** | ❌ Não | ❌ Não | ✅ Opcional | ❌ Não |
| **Paths personalizados** | ✅ Sim | ❌ Não | ❌ Não | ✅ Sim |

## Checklist de Migração

Ao adaptar o `build.yml` para um projeto sem SLCOMLIB:

- [ ] Remover passo de checkout do SLCOMLIB
- [ ] Ajustar paths dos comandos (`working-directory`)
- [ ] Ajustar caminho de publicação (se necessário)
- [ ] Verificar se `SLCOM.sln` referencia corretamente os projetos
- [ ] Verificar se `SLCOM.csproj` não tem `ProjectReference` para SLCOMLIB (ou ajustar path)
- [ ] Testar pipeline localmente (se possível) ou fazer commit e verificar execução
- [ ] Verificar se os artefatos são gerados corretamente
- [ ] Verificar se o deploy funciona com a nova estrutura

## Exemplo Completo: build.yml Sem SLCOMLIB

```yaml
name: Build SLCOM (.NET 8)

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:

  pre-actions:
    name: Pre Actions
    runs-on: ubuntu-latest
    steps:
      - name: Contexto
        run: |
          echo "🔧 Iniciando pipeline..."

  open-ssh-port:
    name: Abrir porta SSH AWS (22)
    runs-on: ubuntu-latest
    needs: pre-actions
    steps:
      - name: Configurar credenciais AWS
        uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: ${{ secrets.AWS_REGION }}

      - name: Abrir porta SSH temporariamente
        run: |
          aws ec2 authorize-security-group-ingress \
          --group-id ${{ secrets.BASTION_SG_ID }} \
          --protocol tcp --port 22 --cidr 0.0.0.0/0 || echo "⚠️ Regra já existente"

  build:
    name: Build (.NET 8)
    runs-on: ubuntu-latest
    needs: pre-actions
    steps:
      - name: Checkout do projeto
        uses: actions/checkout@v4
        with:
          repository: SeuUsuario/SeuProjeto
          token: ${{ secrets.GH_PAT }}

      - name: Instalar .NET 8 SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restaurar dependências
        run: dotnet restore SLCOM.sln

      - name: Compilar o projeto
        run: dotnet build SLCOM.sln --configuration Release --no-restore

      - name: Publicar (gera o executável pronto)
        run: dotnet publish SLCOM.csproj -c Release -r win-x64 -o ./publish --self-contained false

      - name: Remover appsettings de desenvolvimento
        run: |
          if [ -f "publish/appsettings.Development.json" ]; then
            rm -f publish/appsettings.Development.json
          fi

      - name: Compactar o resultado
        run: |
          cd publish
          zip -r ../slcom.zip .

      - name: Armazenar artefato no GitHub
        uses: actions/upload-artifact@v4
        with:
          name: SLCOM
          path: slcom.zip

  transfer-bastion:
    name: Transfer Bastion
    runs-on: ubuntu-latest
    needs: [ build, open-ssh-port ]
    steps:
      - name: Download artifact
        uses: actions/download-artifact@v4
        with:
          name: SLCOM

      - name: Enviar artefato para o Bastion
        uses: appleboy/scp-action@v0.1.4
        with:
          host: ${{ secrets.BASTION_HOST }}
          username: ${{ secrets.BASTION_USERNAME }}
          key: ${{ secrets.BASTION_SSH_KEY }}
          source: "slcom.zip"
          target: "/home/ec2-user/deploys/"

  deploy-servers:
    name: Deploy em Servidores
    runs-on: ubuntu-latest
    needs: transfer-bastion
    steps:
      - name: Executar deploy em múltiplos servidores (via Bastion)
        uses: appleboy/ssh-action@v1.1.0
        with:
          host: ${{ secrets.BASTION_HOST }}
          username: ${{ secrets.BASTION_USERNAME }}
          key: ${{ secrets.BASTION_SSH_KEY }}
          script_stop: false
          script: |
            set -e
            echo "Iniciando deploy no servidor de produção..."

            TMP_PS1="/tmp/deploy_slcom.ps1"
            cat > "$TMP_PS1" <<'PS1'
            Write-Host "== Deploy SLCOM =="

            $deployDir = 'C:\SLCOM_deploys'
            $targetDir = 'C:\SLCOM2'
            $zipPath   = Join-Path $deployDir 'slcom.zip'
            $service   = 'SLCOM'

            Write-Host "Parando serviço $service (se existir)..."
            Stop-Service -Name $service -ErrorAction SilentlyContinue

            Write-Host "Limpando $targetDir ..."
            Get-ChildItem $targetDir -Force |
              Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

            Write-Host "Extraindo pacote $zipPath para $targetDir ..."
            Expand-Archive -Path $zipPath -DestinationPath $targetDir -Force

            Write-Host "Removendo appsettings.Development.json..."
            $devConfigPath = Join-Path $targetDir 'appsettings.Development.json'
            if (Test-Path $devConfigPath) {
              Remove-Item $devConfigPath -Force
              Write-Host "appsettings.Development.json removido com sucesso."
            }

            Write-Host "Iniciando serviço $service..."
            Start-Service -Name $service -ErrorAction SilentlyContinue

            Write-Host "✅ Deploy concluído com sucesso."
            PS1

            for WINDOWS_IP in ${{ secrets.WIN_SERVER_1 }}; do
              [ -z "$WINDOWS_IP" ] && continue
              echo "Deploy em ${WINDOWS_IP}"

              scp -i ~/.ssh/bastion_to_win -o StrictHostKeyChecking=no \
                /home/ec2-user/deploys/slcom.zip \
                Administrator@${WINDOWS_IP}:C:/SLCOM_deploys/slcom.zip

              scp -i ~/.ssh/bastion_to_win -o StrictHostKeyChecking=no \
                "$TMP_PS1" \
                Administrator@${WINDOWS_IP}:C:/SLCOM_deploys/deploy_slcom.ps1

              ssh -i ~/.ssh/bastion_to_win -o StrictHostKeyChecking=no \
                Administrator@${WINDOWS_IP} \
                powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "C:\SLCOM_deploys\deploy_slcom.ps1"
            done

  close-ssh-port:
    name: Fechar porta SSH AWS (22)
    runs-on: ubuntu-latest
    needs: [ open-ssh-port, deploy-servers ]
    if: always()
    steps:
      - name: Configurar credenciais AWS
        uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: ${{ secrets.AWS_REGION }}

      - name: Fechar porta SSH
        run: |
          aws ec2 revoke-security-group-ingress \
            --group-id ${{ secrets.BASTION_SG_ID }} \
            --protocol tcp --port 22 --cidr 0.0.0.0/0 || echo "⚠️ Regra já removida"

  post-actions:
    name: Post Actions
    runs-on: ubuntu-latest
    needs: [ build, transfer-bastion, deploy-servers, close-ssh-port ]
    if: always()
    steps:
      - name: Resumo
        run: |
          echo "🏁 Pipeline finalizado."
          echo "build: ${{ needs.build.result }}"
          echo "transfer-bastion: ${{ needs['transfer-bastion'].result }}"
          echo "deploy-servers: ${{ needs['deploy-servers'].result }}"
          echo "close-ssh-port: ${{ needs['close-ssh-port'].result }}"
```

## Troubleshooting

### Erro: "Could not find a part of the path '../SLCOMLIB'"

**Causa**: O passo de checkout do SLCOMLIB foi removido, mas o projeto ainda referencia.

**Solução**:
1. Verifique se o `SLCOM.csproj` não tem `ProjectReference` para SLCOMLIB
2. Ou ajuste o caminho da referência no `.csproj`
3. Ou mantenha o passo de checkout (se necessário)

### Erro: "Project reference '../SLCOMLIB/SLCOMLIB.csproj' not found"

**Causa**: Caminho do projeto SLCOMLIB está incorreto após mudança de estrutura.

**Solução**:
1. Verifique a estrutura de diretórios após o checkout
2. Ajuste o caminho relativo no `SLCOM.csproj`
3. Ou ajuste os paths no `build.yml`

### Erro: "The working-directory does not exist"

**Causa**: O `working-directory` está apontando para um path que não existe.

**Solução**:
1. Verifique a estrutura de diretórios após o checkout
2. Ajuste o `working-directory` para o path correto
3. Ou remova o `working-directory` se o projeto está na raiz

### Erro no NuGet: "Unable to find package"

**Causa**: Pacote NuGet não encontrado ou fonte não configurada.

**Solução**:
1. Verifique se o pacote existe e está acessível
2. Configure a fonte NuGet no passo anterior ao restore
3. Verifique se as credenciais estão corretas (se feed privado)

## Referências

- **build.yml original**: `.github/workflows/build.yml`
- **GitHub Actions Checkout**: https://github.com/actions/checkout
- **GitHub Actions Setup .NET**: https://github.com/actions/setup-dotnet
