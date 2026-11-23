# Publicar uma release — NotepadMoreOS

Este guia cobre os dois caminhos: **automático** (tag no GitHub, o CI monta tudo) e **manual** (você gera os pacotes e anexa na release).

## 1. Antes de publicar

```bash
dotnet build -c Release          # 0 erros, 0 avisos
dotnet test -c Release           # todos os testes verdes
dotnet run -c Release --project tools/NotepadMoreOS.UiSnapshot   # exit 0, sem [Binding]
```

Confira também se `CHANGELOG.md` tem a entrada da versão e se os screenshots em `docs/` estão atualizados.

## 2. Subir a versão

A versão vem de `Directory.Build.props` e é usada nos nomes dos pacotes.

1. `Directory.Build.props` → `<Version>`.
2. `CHANGELOG.md` → nova seção no topo.
3. `README.md` e `docs/ROADMAP.md` → “Versão atual” / “Estado atual”.
4. `AGENTS.md` → linha `Current version` (contexto para agentes).
5. Regenerar recursos se mudou texto de UI: `python3 tools/localization/generate.py`.

```bash
git add -A
git commit -m "Release 1.7.3"
git push origin main
```

## 3. Caminho automático (recomendado)

O workflow `.github/workflows/release.yml` roda em tags `v*`:

```bash
git tag -a v1.7.3 -m "NotepadMoreOS 1.7.3"
git push origin v1.7.3
```

O que acontece:

1. **macOS** (`macos-latest`): publica `osx-arm64`, monta o `.app`, assina em modo ad-hoc e gera o `.dmg`.
2. **Windows** (`windows-latest`): instala o Inno Setup via Chocolatey, publica `win-x64` e compila o `setup.exe`.
3. **Linux** (`ubuntu-latest`): publica `linux-x64` e monta `.deb` + `.tar.gz` com `dpkg-deb`.
4. **publish**: baixa os três artefatos, cria a Release e anexa os pacotes com notas geradas automaticamente.

Acompanhe em **Actions → Release packages**. Se um job falhar, abra o run, leia o passo que quebrou, corrija e rode **Re-run all jobs** — não é preciso apagar a tag.

Para uma release só manual (sem tag), use **Actions → Release packages → Run workflow** (o job `publish` é ignorado porque ele exige tag).

## 4. Caminho manual (sem CI)

### 4.1 Gerar os pacotes localmente

```bash
tools/release/build-installers.sh      # macOS + Windows + Linux
```

Ou por plataforma:

```bash
tools/release/build-macos.sh arm64     # NotepadMoreOS.app + .dmg
tools/release/build-windows.sh         # setup.exe (Docker + Inno Setup) ou zip
tools/release/build-linux.sh x64       # .deb + .tar.gz
```

Saída em `artifacts/` (ignorado pelo git — os binários não entram no repositório):

| Arquivo | Origem |
|---|---|
| `NotepadMoreOS-<v>-macOS-arm64.dmg` | `build-macos.sh` |
| `NotepadMoreOS-<v>-macOS-arm64.zip` | `build-macos.sh` (app + `LEIA-ME.txt`) |
| `NotepadMoreOS-<v>-windows-x64-setup.exe` | `build-windows.sh` (Inno Setup) |
| `NotepadMoreOS-<v>-windows-x64.zip` | fallback sem Docker |
| `NotepadMoreOS-<v>-linux-amd64.deb` | `build-linux.sh x64` |
| `NotepadMoreOS-<v>-linux-x64.tar.gz` | idem |

Requisitos: .NET 10 SDK, Docker (para o `setup.exe` e para o `.deb` fora do Linux), `create-dmg` opcional (há fallback para `hdiutil`).

### 4.2 Conferir antes de anexar

```bash
# macOS: abre o DMG, confere versão e o app
hdiutil attach artifacts/NotepadMoreOS-1.7.3-macOS-arm64.dmg
/usr/libexec/PlistBuddy -c "Print :CFBundleShortVersionString" \
  /Volumes/NotepadMoreOS/NotepadMoreOS.app/Contents/Info.plist
hdiutil detach /Volumes/NotepadMoreOS

# Linux: instala em container limpo e testa com Xvfb
docker run --rm -v "$PWD/artifacts:/art:ro" debian:stable bash -c \
  "apt-get update -qq && apt-get install -y xvfb /art/NotepadMoreOS-1.7.3-linux-amd64.deb && timeout 8 xvfb-run -a notepadmoreos"

# Integridade dos pacotes
shasum -a 256 artifacts/*
```

### 4.3 Criar a release no GitHub

**Pela interface web:** *Releases → Draft a new release* → escolha/crie a tag `v1.7.3` → título `v1.7.3` → descreva as mudanças → anexe os pacotes de `artifacts/` → **Publish release**.

**Ou pela CLI** (`brew install gh`, depois `gh auth login`):

```bash
gh release create v1.7.3 \
  artifacts/NotepadMoreOS-1.7.3-macOS-arm64.dmg \
  artifacts/NotepadMoreOS-1.7.3-windows-x64-setup.exe \
  artifacts/NotepadMoreOS-1.7.3-linux-amd64.deb \
  artifacts/NotepadMoreOS-1.7.3-linux-x64.tar.gz \
  --title "v1.7.3" \
  --notes-file CHANGELOG.md \
  --generate-notes
```

Anexe sempre a versão da tag: o nome do arquivo precisa casar com a versão instalada, senão o usuário baixa um pacote diferente do anunciado.

### 4.4 Verificar depois de publicar

1. Baixe ao menos um pacote pela página da release (não do disco) e confira o tamanho.
2. No macOS, abra o DMG baixado; no Windows, rode o `setup.exe`; no Linux, `apt install ./…deb`.
3. Confirme a versão em **Help → About NotepadMoreOS**.

## 5. Problemas comuns

| Sintoma | Causa / solução |
|---|---|
| `create-dmg` falha com erro do Finder (-1712) | O AppleScript do Finder expira em automação; o script cai para `hdiutil` automaticamente |
| `dpkg-deb: not found` | Instale `dpkg-dev` ou tenha o Docker ativo (o script usa `debian:stable`) |
| `.deb` abre e fecha no Linux | Build sem `InvariantGlobalization=true` (falta ICU); use `build-linux.sh` |
| `setup.exe` não é gerado | Docker desligado ou imagem `amake/innosetup` indisponível; o script gera zip como fallback |
| Release sem anexos | Algum job de plataforma falhou; o job `publish` depende dos três |

## 6. Assinatura (pendente)

Os pacotes são assinados apenas em modo ad-hoc (macOS) e não têm assinatura Authenticode (Windows). Por isso:

- macOS: o Gatekeeper bloqueia o **próprio `.dmg`** antes de montar, além da primeira abertura do app. Não existe solução pelo lado do pacote sem notarização: o fluxo é remover a quarentena (`xattr -d com.apple.quarantine …dmg`) ou usar o `.zip` e liberar o app em **Ajustes do Sistema → Privacidade e Segurança → Abrir Mesmo Assim**. Desde o macOS 15 o atalho **botão direito → Abrir** não existe.
- Windows: SmartScreen pede **Mais informações → Executar assim mesmo**.

Para eliminar os avisos é necessário um certificado Apple Developer ID + notarização e um certificado de code signing na Windows; os scripts já isolam o passo de assinatura para facilitar essa troca.
