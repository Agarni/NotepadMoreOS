# Guia de desenvolvimento — NotepadMoreOS

Este manual é para desenvolvedores e agentes de IA que vão alterar o projeto. Ele assume que você já leu o [Guia do usuário](GUIA-USUARIO.md) e a [Arquitetura](ARQUITETURA.md).

## 1. Preparação

Requisitos: .NET 10 SDK.

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/NotepadMoreOS
```

Verificação visual (headless, grava em `docs/`):

```bash
dotnet run --project tools/NotepadMoreOS.UiSnapshot
```

Publicação (gera instaladores e pacotes em `artifacts/`, ignorado pelo git):

```bash
tools/release/build-installers.sh          # DMG (macOS) + setup.exe (Windows) + deb/tar.gz (Linux)
tools/release/build-macos.sh [arm64|x64]   # .app assinado + .dmg
tools/release/build-windows.sh             # publish + Inno Setup via Docker (zip como fallback)
tools/release/build-linux.sh [x64|arm64]   # .deb + .tar.gz portátil
tools/release/publish.sh                   # .app + zips portáteis, sem instalador
```

- `setup.iss` é o script do Inno Setup 6: instala por usuário (`{autopf}` → `%LOCALAPPDATA%\Programs`), com tarefas opcionais de atalho, associação de `.txt`/`.log`/`.md` e **Open with NotepadMoreOS**; registra `App Paths` para abrir pelo `Win+R`; desinstala removendo as chaves.
- `build-windows.sh` compila em `amake/innosetup` via Docker quando o daemon está ativo; sem Docker, gera um zip portátil.
- `build-windows.ps1` é a variante nativa para Windows/CI (requer Inno Setup 6 no PATH padrão).
- No macOS o `.app` recebe assinatura ad-hoc (necessária em Apple Silicon sem certificado Developer ID) e o DMG usa `create-dmg` quando possível, com fallback para `hdiutil` (o AppleScript do Finder pode falhar em automação).
- `.github/workflows/release.yml` gera os três instaladores em tags `v*` ou por execução manual.
- Linux: o `.deb` é montado com `dpkg-deb` (nativo no CI ou via Docker `debian:stable`); o binário é publicado com `InvariantGlobalization=true` para não depender de ICU, e o idioma padrão vem de `LANG`/`LC_ALL`.
- Assinatura/notarização reais ainda dependem de certificados; os avisos de Gatekeeper e SmartScreen são esperados.

## 2. Mapa mental do repositório

| Preciso mudar... | Vá para... |
|---|---|
| Aparência da janela, workspace bar/title bar, abas, status | `Views/MainWindow.axaml` + `Styles/` |
| Comportamento de arquivo, encoding, EOL | `Services/FileService.cs`, `ViewModels/DocumentViewModel.cs` |
| Busca no documento | `Services/SearchService.cs`, `ViewModels/FindReplaceViewModel.cs` |
| Busca em arquivos | `Services/FileSearchService.cs`, `ViewModels/FindInFilesViewModel.cs`, `Views/Dialogs/FindInFilesWindow.axaml` |
| Preferências | `Models/AppSettings.cs`, `ViewModels/SettingsViewModel.cs`, `Views/Dialogs/SettingsWindow.axaml` |
| Tema ou cores | `Services/ThemeService.cs`, `Styles/Palette.axaml`, `Styles/AppStyles.axaml` |
| Atalhos | `Views/MainWindow.axaml` + `Controls/ModExtension.cs` |
| Editor e ciclo TextMate | `Views/DocumentView.axaml(.cs)` |
| Sessão/recentes | `ViewModels/MainWindowViewModel.cs`, `Models/AppSettings.cs` |
| Recuperação após crash | `Services/SessionService.cs`, `Models/RecoveryModels.cs`, `ViewModels/MainWindowViewModel.cs` |
| Sessões nomeadas | `Models/NamedSession.cs`, `ViewModels/SessionItemViewModel.cs`, `MainWindowViewModel` commands |
| Ações de aba | `DocumentViewModel.RequestActionCommand`, `MainWindowViewModel.HandleDocumentActionAsync`, tab `ContextFlyout` |
| Alterações externas | `Services/FileWatchService.cs`, `ViewModels/MainWindowViewModel.cs` |
| Menu nativo do macOS | `Views/MainWindow.NativeMenu.cs`, `App.axaml.cs` |
| Testes | `tests/NotepadMoreOS.Tests/` |

## 3. Invariantes (não quebre)

1. **O `TextDocument` pertence ao `DocumentViewModel`.** Não faça binding de `TextEditor.Text`; atribua `TextEditor.Document` no `DocumentView` e escute mudanças de `Document`.
2. **Não coloque lógica de negócio no code-behind.** Code-behind só conecta eventos de UI, pickers, foco e fechamento de diálogo. Regras ficam em ViewModels/Services.
3. **Use bindings compilados.** Sempre declare `x:DataType` em janelas e DataTemplates. Evite `ReflectionBinding`; se precisar, justifique.
4. **Use `controls:Mod` para atalhos primários.** Não escreva `Ctrl+` em XAML para atalhos que devem ser `Cmd` no macOS. O markup resolve `Meta`/`Control`.
5. **Cuidado com atalhos reservados do macOS.** `Cmd+Q` sai do app, `Cmd+H` oculta, `Cmd+Tab` troca de app. Por isso comentar/substituir/quebra de linha usam `Cmd+Alt+...`.
6. **Um controle derivado precisa de `StyleKeyOverride`.** Veja `Controls/NotepadTextEditor.cs`; sem isso o `ControlTheme` do tipo base não é aplicado.
7. **TextMate é instalado por view e descartado no detach.** Não compartilhe uma `Installation` entre documentos.
8. **Toda gravação de arquivo passa por `FileService`.** Ele normaliza EOL, codifica, grava em temporário e troca atomicamente.
9. **Estado sujo vem do undo + encoding salvo.** Use `UndoStack.MarkAsOriginalFile()` após carregar/salvar.
10. **A busca em arquivos roda fora da UI e tem limites.** Use `Task.Run`, `CancellationToken`, `IProgress`, 10 MB e 5.000 resultados.
11. **Persistência é best-effort.** Falhas em `SettingsService.Save` não podem derrubar o app.
12. **UI em inglês e português.** Textos visíveis ficam em `Resources/Strings*.resx` (fonte: `tools/localization/strings.tsv`). Não hardcode strings de interface; nome de marca e atalhos não são traduzidos.
13. **Com preservação ligada, o fechamento normal mantém o snapshot.** Não pergunte por arquivo e não limpe `recovery/`; o original só muda com Save. Com preservação desligada, pergunte e limpe após o fechamento.
14. **Suprima o watcher ao salvar.** `FileWatchService.Suppress` evita que a própria gravação dispare reload/alerta.
15. **O menu nativo do macOS é instalado uma única vez, após `Loaded`.** Definir um segundo `NativeMenu` na mesma janela lança uma exceção nativa.
16. **Headless deve continuar mostrando o menu interno.** A ferramenta de screenshots depende disso.

## 4. Receitas

### 4.1 Adicionar um comando

1. No ViewModel, crie um método com `[RelayCommand]`:

```csharp
[RelayCommand]
private void ToggleSomething()
{
    // altera estado observável
}
```

O gerador cria `ToggleSomethingCommand`.

2. Adicione o item de menu em `MainWindow.axaml`:

```xml
<MenuItem Header="Something" Command="{Binding ToggleSomethingCommand}" />
```

3. Se houver atalho, adicione em `Window.KeyBindings` e no `HotKey` do menu:

```xml
<KeyBinding Gesture="{controls:Mod Key=K, Modifiers=Alt}"
            Command="{Binding ToggleSomethingCommand}" />
```

4. Se o comando depende do editor, use `ActiveDocument?.Host` (`IEditorHost`) em vez de acessar o `TextEditor` diretamente.
5. Atualize a tabela de atalhos do README e do guia do usuário.
6. Adicione teste se a lógica não for puramente visual.

### 4.2 Adicionar uma preferência

1. Adicione a propriedade com valor padrão em `Models/AppSettings.cs`.
2. Se afeta o editor, adicione em `EditorSettingsViewModel`, escreva no `AppSettings` e notifique `Changed`:

```csharp
partial void OnMyOptionChanged(bool value) => Persist(s => s.MyOption = value);
```

3. Se afeta a UI global, adicione em `SettingsViewModel` e na janela `SettingsWindow.axaml`.
4. Aplique em `SettingsViewModel.ApplyTo`.
5. O salvamento é agendado automaticamente por `Changed`; não chame `Save` a cada tecla.
6. Se a opção impacta documentos abertos, notifique a propriedade derivada correspondente (ex.: `EffectiveFontSize`).
7. Teste o round-trip em `SettingsServiceTests` e o efeito em `EditorSettingsViewModel`.

### 4.3 Adicionar um diálogo

1. Crie `Views/Dialogs/MyDialog.axaml(.cs)` com `Window`, `ShowInTaskbar="False"` e `WindowStartupLocation="CenterOwner"`.
2. Use as classes de estilo existentes (`accent`, `ghost`, `find`, `label`, `section-title`).
3. Exponha um resultado tipado via `Close(result)` e `ShowDialog<T>`.
4. Adicione um método em `DialogService`, por exemplo:

```csharp
public async Task<MyResult?> ShowMyDialogAsync(...)
{
    var window = new MyDialog(...);
    return await window.ShowDialog<MyResult?>(Owner);
}
```

5. Injete dependências no ViewModel do diálogo, não use service locator.
6. Cancele operações assíncronas no `OnClosed`/`Dispose`.
7. Adicione teste de ViewModel, se houver lógica.
8. Capture a tela com a ferramenta de snapshot.

### 4.4 Adicionar um serviço

1. Crie a classe em `Services/`, com dependências explícitas no construtor.
2. Evite estado global; se precisar de cache, documente a validade.
3. Registre em `App.axaml.cs`:

```csharp
services.AddSingleton<MyService>();
```

4. Se houver IO, mantenha a UI responsiva (`Task.Run` ou `*Async`).
5. Escreva testes isolados, com diretórios temporários; nunca escreva na configuração real do usuário.

### 4.5 Adicionar uma linguagem

A maior parte é automática:

1. `LanguageService` lista as linguagens do `TextMateSharp.Grammars` e mapeia extensões.
2. Escolha o nome de exibição pelo primeiro alias.
3. Se a linguagem precisar de regra de comentário, ajuste `DocumentViewModel.CommentPrefix`.
4. Se precisar de indentação específica, ajuste `DocumentView.ApplyIndentation`.
5. Adicione um teste para detecção e prefixo, quando aplicável.

### Prévia e formatação de documentos

A prévia Markdown é vinculada pelo `DocumentView` e acompanha o texto enquanto estiver aberta. O divisor redimensiona as linhas do editor e da prévia. `UpdatePreviewLayout` restaura as proporções ao abrir e zera a linha da prévia ao fechar. A ferramenta de snapshot verifica a recuperação da área do editor após mudar a proporção e fechar o painel. Ao alterar `DocumentFormatter`, preserve comentários do JSONC, respeite a indentação configurada e só substitua o documento após validar e formatar todo o conteúdo.

### 4.6 Alterar a busca

**No documento:** `SearchService` concentra regex, opções, preview e replace. A UI chama `FindReplaceViewModel`, que mantém escopo e estado. Evite duplicar a construção de regex na UI.

Modos de padrão (mutuamente exclusivos): literal, expressão regular (`UseRegex`) e estendido (`UseExtendedEscapes`). O modo estendido passa por `SearchService.UnescapeExtended` antes de escapar o literal; ao adicionar um escape novo, atualize a tabela do guia do usuário e os testes de `UnescapeExtended`. Substituições usam `PrepareReplacement` para respeitar o modo ativo.

**Em arquivos:** `FileSearchService` já cobre máscaras, subpastas, codificação, binários, limite e cancelamento. Para uma nova opção:

1. adicione em `FileSearchOptions`;
2. use em `SearchCore`;
3. exponha em `FindInFilesViewModel`;
4. adicione o controle na janela;
5. teste o comportamento no serviço (mais importante que testar a janela).

### 4.7 Adicionar um teste

Padrão da suíte:

```csharp
[Fact]
public async Task MyFeature_DoesWhatItSays()
{
    var root = TestEnvironment.CreateTempDirectory();
    try
    {
        // arrange
        // act
        // assert
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}
```

- Use `TestEnvironment.CreateDocument()` para um `DocumentViewModel` sem UI.
- Use `TestEditorHost` para simular o editor em testes de ViewModel.
- Use `SettingsService(customPath)` para não tocar no `settings.json` real.
- Teste limites (vazio, inválido, cancelado, truncado), não só o caminho feliz.
- Se o teste acessa um `TextDocument` depois de um `await`, envolva o corpo com `AsyncTest.Run(async () => { ... });`. O `TextDocument` do AvaloniaEdit tem afinidade de thread e o contexto padrão do xUnit pode trocar de thread entre continuações.
- `AsyncTest` possui timeout de 60 s e repropaga a exceção original.

### 4.8 Alterar a recuperação após crash

- `SessionService` grava metadados e conteúdo; não coloque lógica de documento nele.
- `MainWindowViewModel.SaveRecoverySnapshotAsync` monta o snapshot a partir das abas.
- Documentos sujos/não salvos guardam conteúdo; documentos limpos guardam apenas caminho.
- Restauração usa `DocumentViewModel.RestoreRecoveryAsync`.
- Com `RecoveryEnabled` ligado, o fechamento normal salva o snapshot, mantém `recovery/` e não grava os arquivos originais.
- Com `RecoveryEnabled` desligado, o fechamento pergunta e limpa a recuperação.
- Teste com `SessionService(directory)` e `AsyncTest.Run`.

### 4.9 Alterar a detecção de alterações externas

- `FileWatchService` é por arquivo, com debounce e supressão.
- Rastreie em `AddDocument` e após Save As; remova em `RemoveDocument`.
- Chame `Suppress` antes de salvar (origem e destino).
- `HandleExternalChangeAsync` decide entre reload automático e confirmação.
- Testes de watcher usam debounce curto e timeout generoso; evite depender de timing exato.

### 4.10 Menu nativo do macOS

- Menu da aplicação: `App.Initialize`, antes da janela.
- Menu da janela: `MainWindow.NativeMenu.cs`, uma única instância, após `Loaded`.
- Não adicione `Gesture` a Undo/Cut/Copy/Paste/Select All; isso quebraria edição de texto em diálogos.
- Submenus dinâmicos usam `NativeMenu.NeedsUpdate`.
- No macOS real, limpe `Window.KeyBindings` para evitar execução dupla pelo menu + binding e recrie `Esc` se necessário. Limpe também os `HotKey` do menu interno oculto.
- `IsHeadlessPlatform` mantém o menu interno para snapshots.

### 4.11 Cores por linguagem e badges

- `DocumentViewModel.LanguageAccentBrush` e `LanguageSoftBrush` são derivados do `Language.Id`.
- Ao adicionar uma linguagem que mereça cor própria, ajuste `GetLanguageColor`; linguagens sem cor usam um cinza neutro.
- As abas e o seletor da barra de workspace usam esses brushes.
- `MainWindowViewModel.ActiveLanguageAccentBrush` expõe a cor da aba ativa para o topo.

### 4.11.1 Aparência das abas

Há três estilos, escolhidos em **Settings → Appearance → Tab style** (`AppSettings.TabStyle`, ids em `Models/TabStyles.cs`). O estilo vira uma classe no `Border` da faixa de abas (`tabs-rounded`, `tabs-floating`, `tabs-connected`) e as variações são estilos descendentes em `AppStyles.axaml`.

- **Rounded** (padrão): pílula com `CornerRadius=8`. O acento é um `Border.tab-indicator` dentro do conteúdo da aba, com margem lateral para não tocar os cantos; nunca use `BorderThickness` no fundo arredondado, pois a borda contorna os cantos e escapa da silhueta.
- **Floating**: abas mais baixas e espaçadas, sem sublinhado; a aba ativa se destaca pelo preenchimento.
- **Connected**: base reta, encostada na linha da faixa, com o acento como borda inferior; o padding reserva os 2px para não deslocar o conteúdo ao ativar.

Regras:

- O conteúdo da aba usa `VerticalContentAlignment=Stretch` e um `Grid` centralizado, para o indicador ficar no rodapé real da aba.
- Abas **não têm preenchimento em hover**: apenas o botão de fechar aparece (`Opacity`) e o ponteiro vira mão. Evite reintroduzir preenchimento sem antes validar com o snapshot, pois ele compete com a aba ativa.
- O botão de fechar usa `TabCloseHoverBrush` como destaque próprio.
- `PreviewTabStyle` aplica o estilo durante a prévia e `null` restaura o valor salvo.
- A faixa de abas não usa barra de rolagem: `TabScroll` (ScrollViewer) fica com `HorizontalScrollBarVisibility="Hidden"` e botões `TabScrollLeft/Right` aparecem só quando há overflow; `BringActiveTabIntoView` rola até a aba ativa e `UpdateTabScrollButtons` recalcula a visibilidade. O snapshot valida esse fluxo com 10 abas.

### 4.12 Chrome próprio no macOS

- `WindowDecorations.BorderOnly` + `ExtendClientAreaToDecorationsHint` remove a title bar do SO e preserva resize.
- Os traffic lights são controles próprios; não dependa dos botões nativos.
- A `WorkspaceBar` trata arraste e duplo clique; não intercepte cliques de botões (`IsInsideButton`).
- No headless, mostre os traffic lights, mas não altere o chrome nativo e oculte o menu interno para o screenshot refletir o macOS.
- No Windows/Linux, mantenha o chrome nativo e esconda os traffic lights.

### 4.13 Adicionar uma ação ao menu de contexto da aba

1. Adicione um `MenuItem` no `ContextFlyout` da aba com `Command="{Binding RequestActionCommand}"` e um `CommandParameter` novo.
2. Trate a string em `MainWindowViewModel.HandleDocumentActionAsync`.
3. Opere no `document` recebido, não em `ActiveDocument`.
4. Se a ação alterar estado persistente (pino/cor/read-only/caret), atualize a recuperação e as sessões nomeadas.
5. Adicione teste em `SessionAndTabActionTests` quando possível.

### 4.14 Sessões nomeadas

- Modelo em `NamedSession`/`NamedSessionFile`; persistência em `AppSettings.Sessions`.
- Salvar/abrir/renomear/excluir usam os métodos de `MainWindowViewModel`.
- Ao trocar de sessão, grave o workspace atual nos rascunhos da sessão (sem prompts) antes de fechar as abas; se não houver sessão nomeada, crie `Unsaved workspace`.
- Abas não salvas ou sujas geram rascunhos em `session-drafts/<sessionId>/` ao lado de `settings.json`; o conteúdo nunca entra no JSON de preferências nem altera arquivos originais.
- `DocumentViewModel.RestoreSessionDraft` recria a aba em memória com codificação, EOL, linguagem, caret, pino, cor, read-only e estado sujo.
- Abas sem caminho voltam limpas e recebem `SessionDraftPath`; `SaveDocumentAsync` grava nesse rascunho sem pedir nome, e `Save As...` exporta e limpa o vínculo.
- `SaveDocumentAsync` não faz nada quando o documento não está sujo e o comando não é Save As.
- Ao excluir uma sessão, remova também a pasta de rascunhos; ao salvar novamente, limpe rascunhos obsoletos.
- `CreateEmptySessionAsync` cria uma sessão sem arquivos e esvazia o workspace, usando o snapshot automático descrito acima.

### 4.15 Flyouts com botões de ação

Um `Flyout` com botões comuns não fecha sozinho ao clicar em uma ação. Ligue `Click` a um handler que agenda `Flyout.Hide()` com `Dispatcher.UIThread.Post(..., DispatcherPriority.Background)`; o menu de sessões usa `OnSessionFlyoutItemClick`. Não chame `Hide()` de forma síncrona: o evento `Click` ocorre antes do comando, e fechar o flyout pode limpar o `CommandParameter` e impedir a seleção. `MenuFlyout`/`MenuItem` fecham o submenu automaticamente, mas ainda é preciso fechar o flyout pai quando a ação deve encerrar o menu. A ferramenta de snapshot clica em uma sessão e valida a abertura e o fechamento.

### 4.16 Atualizar a identidade visual

- Use a referência da marca (folha com canto dobrado, linhas de código e sinal de “mais”) como ponto de partida, não como algo a copiar pixel por pixel.
- Na identidade escolhida, preserve fundo e folha azul-escuros, contorno luminoso ciano/azul/violeta e símbolo de “mais”; mantenha formas e contraste legíveis em ícones pequenos.
- Separe o símbolo do wordmark. Ícones de janela e de plataforma usam somente o símbolo; documentos de apresentação podem usar a composição com o nome.
- Atualize `Assets/appicon.svg` como fonte vetorial e regenere `appicon.png`/`appicon.ico`; mantenha About, Welcome e a title bar coerentes usando o PNG compartilhado, sem duplicar vetores incompatíveis.
- Execute o snapshot, inspecione as capturas e verifique visualmente o About, Welcome e o ícone da janela.

### 4.17 Alterar temas e cor de destaque

- `ThemeInfo` fornece as cores da miniatura de pré-visualização; mantenha a amostra coerente com a identidade do tema TextMate.
- Alterações feitas em Settings devem ser aplicadas temporariamente por `SettingsViewModel.PreviewChanged`; Cancel precisa restaurar tema e cor persistidos, Save persiste ambos.
- A cor de destaque é salva como `#RRGGBB` em `AppSettings.AccentColor`; aplique as cores derivadas aos dicionários Light e Dark em `ThemeService`, sem alterar a cor da sintaxe TextMate.
- A paleta vive em `ThemeService.AccentPalette` e é consumida por Settings e pelos menus View; não duplique a lista.
- `AccentColorPreset.IsSelected` marca a cor ativa na UI; ao adicionar ou remover cores, mantenha o estado sincronizado com `AppSettings.AccentColor`.
- Evite cores de acento fixas no código de UI: use `AccentBrush`, `AccentForegroundBrush` ou `EditorSelectionBrush` para acompanhar a escolha do usuário.
- Preserve as predefinições e a validação de hex de seis dígitos; execute testes e snapshot para validar o preview e os bindings.

### 4.18 Contagem de ocorrências na busca

- A barra de busca mostra o total enquanto o usuário digita, antes de navegar. `FindReplaceViewModel.RefreshAfterCriteriaChange` limpa resultados antigos e chama `UpdateMatchSummary`.
- Use `SearchService.CountMatches` para a contagem: ela ignora previews e para no limite de 10.000, ao contrário de `FindAll`.
- Não use `FindAll` para a contagem ao vivo; ele monta preview e registros de cada ocorrência.
- Ao navegar, o status passa a ser `Match N of M`; ao alterar qualquer critério, volta a ser o total.
- Regex inválido é reportado durante a digitação (`Invalid regular expression`), como ao executar a busca.

### 4.20 Adicionar um texto de interface

1. Acrescente a chave em `tools/localization/strings.tsv` (`Chave|English|Português`).
2. Rode `python3 tools/localization/generate.py` para regerar os dois `.resx` e `Strings.g.cs`.
3. No XAML use `{x:Static loc:Strings.Chave}` com `xmlns:loc="using:NotepadMoreOS.Resources"`; no código use `Strings.Chave` ou `string.Format(Strings.Chave, args)`.
4. Não hardcode texto visível ao usuário, e não localize nome de marca, atalhos nem nomes de produtos.
5. Textos que exigem reinício devem ser aplicados em `App.Initialize`, antes de carregar o XAML.
6. Rode os testes: `LocalizationTests` valida paridade de chaves entre os idiomas.

### 4.21 Verificar a UI sem abrir uma janela

```bash
dotnet run --project tools/NotepadMoreOS.UiSnapshot
```

A ferramenta:

- inicia Avalonia.Headless com Skia;
- ativa `LogToTrace` e um `ConsoleTraceListener`;
- monta a janela principal com dados de exemplo;
- renderiza o editor escuro/claro, barra de busca, busca em arquivos, preferências e sobre;
- grava em `docs/`.

Se aparecer uma linha `[Binding]...`, corrija antes de considerar a tarefa concluída. Bindings compilados ainda podem falhar em runtime quando um caminho intermediário é nulo.

A ferramenta registra essas linhas e encerra com código de saída `1` quando alguma ocorre, listando os erros no final da execução. Um `exit 0` significa UI renderizada sem erro de binding.

## 5. Armadilhas conhecidas

### NativeMenu duplicado

`NativeMenu.SetMenu` duas vezes na mesma janela pode lançar `The menu being updated does not match`. Construa uma única instância e atualize itens/submenus.

### Watcher e gravações próprias

Sem `FileWatchService.Suppress` antes de salvar, o editor pode recarregar o próprio arquivo e exibir avisos indevidos.

### Recuperação e fechamento

Um teste que chama `SaveRecoverySnapshotAsync` e depois `RequestCloseApplicationAsync` deve terminar com `HasRecovery == false`.

### Binding em `Document`/`TextEditor`

Não use `Text="{Binding Text}"`. O documento é o modelo e é atribuído no attach. Se trocar `TextDocument`, o `DocumentView` precisa escutar a propriedade `Document`.

### Tema de controle derivado

`NotepadTextEditor` sobrescreve `StyleKeyOverride`. Qualquer novo controle que herde de um controle Avalonia deve fazer o mesmo ou receberá tema vazio.

### `MenuItem.HotKey` x `KeyBinding`

`HotKey` exibe o gesto; `Window.KeyBindings` executa. Cadastre os dois quando o atalho deve funcionar globalmente e aparecer no menu.

### `KeyGesture.ToString()` para teclas OEM

`Key.OemQuestion` aparece como `OemQuestion` no menu. Prefira teclas alfabéticas em atalhos visíveis, ou aceite o nome técnico.

### Fechamento assíncrono

`Window.Closing` precisa de `e.Cancel = true`, aguardar `RequestCloseApplicationAsync` e só então chamar `Close()` via dispatcher. Use `IsCloseApproved` para não perguntar duas vezes em `ShutdownRequested`.

### Status bar e `ActiveDocument` nulo

Bindings em `ActiveDocument.CaretLine` falham e geram log quando não há documento. A barra usa proxies (`StatusCaretLine`, `StatusEncoding`, ...) no `MainWindowViewModel` para não emitir erros.

### Regex de comprimento zero

`FindAll` ignora matches vazios para evitar loops. `ReplaceAll` usa a semântica do `Regex.Replace`; ao alterar isso, cubra os dois casos em teste.

### Heurística de binário

`FileService.IsProbablyBinary` aceita UTF-16 (com/sem BOM) e rejeita NUL em outras codificações. Ajustes nessa heurística exigem testes com UTF-8, UTF-16 e bytes binários.

### Limites da busca em arquivos

10 MB por arquivo e 5.000 resultados são intencionais. Se aumentar, meça tempo, memória e responsividade da UI; prefira permitir refinar a busca.

## 6. Fluxo recomendado para IA

1. Leia `AGENTS.md`.
2. Leia `docs/ARQUITETURA.md` e as seções relevantes deste guia.
3. Localize os arquivos com `search`/`grep` antes de editar.
4. Faça a menor mudança coerente com os padrões existentes.
5. Execute `dotnet build` e `dotnet test`.
6. Execute a ferramenta de snapshot e procure erros `[Binding]`.
7. Atualize README, guia do usuário, arquitetura e changelog quando o comportamento mudar.
8. Não apague screenshots ou documentação existentes sem substituí-las por equivalentes atualizados.

### Definição de pronto

- build sem erros e sem avisos;
- todos os testes passando (138 no estado atual);
- nenhum `[Binding]` inesperado na execução headless;
- screenshots atualizados quando a UI mudou;
- docs e changelog atualizados;
- atalhos e limitações documentados.

## 7. Comandos úteis

```bash
# build e testes
dotnet build
dotnet test

# executar
dotnet run --project src/NotepadMoreOS

# screenshots
dotnet run --project tools/NotepadMoreOS.UiSnapshot

# filtros de teste
dotnet test --filter "FullyQualifiedName~FileSearchServiceTests"

# publicar macOS ARM
dotnet publish src/NotepadMoreOS -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true
```

## 8. Estilo de código

- nullable habilitado;
- `ImplicitUsings` habilitado;
- nomes de tipos e membros em inglês;
- textos de UI em recursos localizados (inglês e pt-BR);
- comentários apenas para intenção/decisão, não para repetir o código;
- `async` de ponta a ponta em IO;
- ViewModels observáveis com `[ObservableProperty]`/`[RelayCommand]`;
- sem ReactiveUI;
- sem `Avalonia.Diagnostics` (use `LogToTrace` e a ferramenta de snapshot);
- sem dependências novas sem necessidade e sem atualizar versões fixadas por acaso.

## 9. Documentação a manter

Ao mudar comportamento:

| Mudança | Atualize |
|---|---|
| Recurso visível ao usuário | `README.md`, `docs/GUIA-USUARIO.md` |
| Arquitetura, serviço ou fluxo | `docs/ARQUITETURA.md` |
| Convenção/recipe | `docs/DESENVOLVIMENTO.md`, `AGENTS.md` |
| Prioridade futura | `docs/ROADMAP.md` |
| Versão | `Directory.Build.props`, `CHANGELOG.md` |
| UI | screenshots em `docs/` |
