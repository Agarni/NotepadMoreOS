# Changelog

Todas as mudanças relevantes do NotepadMoreOS são registradas aqui.

## [Não publicado]

## [1.7.5] — 2026-11-23

### Adicionado

- Destaque configurável de pares de parênteses, colchetes e chaves junto ao cursor.
- Quebra visual de linha e exibição de caracteres invisíveis temporárias por aba, com indicadores de diferença em relação às preferências.
- Prévia Markdown somente leitura em painel redimensionável abaixo do editor.
- Formatação local de JSON, JSONC e XML com aviso para conteúdo inválido.

### Corrigido

- Formatação de JSON e JSONC com indentação de estruturas aninhadas e validação do resultado. A formatação de XML preserva os espaços existentes no conteúdo.
- Ao fechar a prévia Markdown, o editor volta a ocupar a área usada pelo painel.

## [1.7.4] — 2026-11-23

### Adicionado

- **Pacote `.zip` para macOS**, publicado ao lado do `.dmg`. O macOS 15+ recusa montar discos não notarizados, então o `.dmg` pode ser bloqueado antes de abrir (mensagem *“A Apple não pôde verificar se o item … está livre de malware”*). O `.zip` extrai normalmente e o app instalado é liberado em **Ajustes do Sistema → Privacidade e Segurança → Abrir Mesmo Assim**.
- O `LEIA-ME.txt` também acompanha o `.zip`, com as instruções nas duas situações (disco bloqueado e app bloqueado).

### Corrigido

- Notas da release e documentação deixam de sugerir abrir o `.dmg` como primeira opção, já que ele pode nem montar sem notarização.

## [1.7.3] — 2026-11-23

### Adicionado

- **`LEIA-ME.txt` dentro do DMG** com o passo a passo para liberar o app no Gatekeeper, em português e inglês.

### Corrigido

- Documentação do macOS desatualizada: orientava usar **botão direito → Abrir**, atalho que a Apple removeu no macOS 15.

## [1.7.2] — 2026-11-21

### Adicionado

- Botão **GitHub project / Projeto no GitHub** na janela About, abrindo https://github.com/Agarni/NotepadMoreOS. O botão do Notepad++ continua ao lado, separando claramente o nosso projeto do editor que o inspirou.

## [1.7.1] — 2026-11-21

### Adicionado

- **Pacotes para Linux**: `.deb` (Debian/Ubuntu) com entrada de menu, ícone, `notepadmoreos` no `PATH`, dependências declaradas e desinstalação limpa; e `.tar.gz` portátil para qualquer distribuição.
- `tools/release/build-linux.sh` (x64 e arm64) e job Linux no workflow de release.

### Corrigido

- O executável Linux agora é publicado com globalização invariante, eliminando a exigência de `libicu` — causa de falha de inicialização em sistemas sem ICU.
- Nesse modo, `libicu` ausente impedia detectar o idioma; o padrão agora é lido de `LC_ALL`/`LC_MESSAGES`/`LANG` (por exemplo, `pt_BR.UTF-8` → português).

## [1.7.0] — 2026-11-21

### Adicionado

- **Instalador para Windows** (Inno Setup 6): assistente moderno, instalação por usuário, atalho opcional na área de trabalho, associação opcional de `.txt`, `.log` e `.md`, entrada **Open with NotepadMoreOS** no menu de contexto, `App Paths` para abrir pelo `Win+R` e desinstalador que remove as chaves criadas.
- **DMG para macOS** com arrastar-para-Applications, ícone do volume e bundle assinado em modo ad-hoc.
- Scripts de build: `build-macos.sh`, `build-windows.sh` (Inno Setup via Docker, com zip como fallback), `build-windows.ps1` e `build-installers.sh`.
- Workflow `release.yml` que gera os dois instaladores em tags `v*` ou por execução manual.

### Alterado

- Guia do usuário com o passo a passo de instalação em macOS e Windows, incluindo os avisos esperados de Gatekeeper e SmartScreen.
- Assinatura/notarização com certificados continua pendente e agora é o único item restante de distribuição no roadmap.

## [1.6.1] — 2026-11-20

### Alterado

- O seletor de linguagem ficou apenas na barra superior; a barra de status não duplica mais a opção.
- O botão de novo arquivo foi removido da barra superior; criar arquivos fica no **+** da faixa de abas (além de `Cmd+N` e **File → New**).
- "Lines" e "Length" da barra de status agora seguem o idioma da interface ("Linhas" e "Comprimento" em pt-BR).

### Corrigido

- Muitas abas abertas deixaram de mostrar uma barra de rolagem grossa: a faixa agora tem setas de navegação nas extremidades, que aparecem apenas quando há overflow, e a roda do mouse rola horizontalmente. Ao ativar uma aba fora da área visível, a faixa rola até ela.
- Snapshot ganhou uma verificação de overflow das abas (com 10 abas) para evitar regressão.

## [1.6.0] — 2026-11-20

### Adicionado

- **Interface em português do Brasil**, além do inglês. O idioma segue o sistema por padrão e pode ser forçado em **Settings → Appearance → Language** (aplicado ao reiniciar).
- Todos os textos de interface — menus internos e nativos, barra de ferramentas, abas, busca, diálogos, mensagens de status e preferências — passaram a vir de recursos (`Strings.resx` / `Strings.pt-BR.resx`).
- Acessor tipado `Strings` e gerador `tools/localization` para manter traduções e chaves em sincronia.

### Alterado

- `AppSettings.Language` adicionado (`system`, `en`, `pt-BR`).
- Testes de localização: paridade de chaves entre idiomas, valores de amostra e normalização do idioma (138 no total).
- A ferramenta de snapshot fixa inglês para manter as capturas determinísticas; `TestCulture` faz o mesmo nos testes.

## [1.5.1] — 2026-11-20

### Alterado

- As abas deixaram de ter preenchimento no hover: passar o mouse agora apenas revela o botão de fechar, sem bloco de destaque competindo com a aba ativa.
- Removida a transição de fundo das abas, que deixava um rastro ao deslizar o mouse entre elas.
- Removido o brush `TabHoverBrush`, que ficou sem uso.

## [1.5.0] — 2026-11-20

### Adicionado

- **Estilos de aba selecionáveis** em **Settings → Appearance**: *Rounded* (padrão, pílula com sublinhado de acento), *Floating* (estilo Safari/opencode, sem sublinhado) e *Connected* (abas clássicas encostadas ao editor).
- Pré-visualização imediata do estilo de aba; **Cancel** restaura o estilo salvo.
- O estilo é persistido em `settings.json`.

### Corrigido

- Abas voltaram ao formato de pílula arredondada, sem o corte reto introduzido na 1.4.9.
- O sublinhado da aba ativa agora é um indicador interno, que não contorna nem escapa dos cantos arredondados.
- O hover das abas ficou mais discreto que a aba ativa, sem o bloco claro que se sobressaía.

### Alterado

- `TabStyle` adicionado a `AppSettings`; `MainWindowViewModel.PreviewTabStyle` permite testar o estilo enquanto as preferências estão abertas.
- Testes para o estilo de aba, validação do valor salvo e prévia (128 no total).

## [1.4.9] — 2026-11-20

### Corrigido

- O sublinhado da aba ativa não contorna mais os cantos arredondados nem "escapa" da silhueta da aba: as abas passaram a ter cantos superiores arredondados e base reta, encostada na linha da faixa.
- O hover deixou de ficar mais claro que a aba ativa no tema escuro, corrigindo a hierarquia visual entre aba ativa, aba em hover e barra.
- Ativar uma aba não desloca mais o conteúdo em 2px; a borda do acento ocupa o espaço já reservado pelo padding.
- O botão de fechar da aba usa um destaque suave em vez do bloco cinza forte.

### Alterado

- Novos brushes `TabHoverBrush` e `TabCloseHoverBrush` nos temas claro e escuro, separando o hover das abas dos demais controles.
- Faixa de abas mais compacta, com as abas encostadas na linha inferior.

## [1.4.8] — 2026-11-20

### Adicionado

- **Pesquisa estendida** no Find, no estilo do Notepad++: o botão `\t` interpreta `\t`, `\r`, `\n`, `\\`, `\0`, `\a`, `\b`, `\f`, `\v`, `\xNN`, `\uNNNN` e `\oNNN` como caracteres reais.
- As mesmas sequências são interpretadas em **Replace with**, permitindo inserir tabulações e quebras de linha.
- Modo estendido e expressão regular são alternativos: ativar um desativa o outro.

### Alterado

- `SearchService.UnescapeExtended` e `PrepareReplacement` centralizam a expansão de escapes e o tratamento de `$` conforme o modo ativo.
- Escapes desconhecidos, como `\q`, são preservados literalmente.
- Testes ampliados para escapes, busca literal em modo estendido, substituição e exclusividade dos modos (125 no total).

## [1.4.7] — 2026-11-20

### Adicionado

- A barra de busca mostra a contagem de ocorrências **enquanto você digita** (`3 matches`, `1 match`, `No matches`), sem precisar navegar.
- Expressão regular inválida é indicada durante a digitação, e não apenas ao executar a busca.
- A contagem respeita maiúsculas/minúsculas, palavra inteira, regex e o escopo da seleção, atualizando ao alternar cada opção.

### Alterado

- `SearchService.CountMatches` conta ocorrências sem montar previews, mantendo o limite de 10.000 e o timeout de regex.
- Ao navegar, o status continua exibindo `Match N of M`; ao alterar um critério, volta ao total.
- A captura de tela da busca passou a usar o arquivo de código, mostrando o contador e os resultados.

## [1.4.6] — 2026-11-20

### Adicionado

- Menu **View → Accent Color** (e equivalente no menu nativo do macOS) para trocar a cor de destaque sem abrir as preferências, com marcação da cor ativa.
- Botão **Reset to default** nas preferências e no menu para voltar ao verde original.
- A cor ativa agora aparece com um anel de seleção na paleta.

### Corrigido

- A cor de seleção do editor e o badge de linguagens sem cor própria não ficam mais verdes fixos quando outra cor de destaque está ativa.
- A ferramenta de snapshot passa a **falhar a execução** quando Avalonia registra erro de binding, em vez de apenas imprimir a linha `[Binding]`.

### Alterado

- A paleta de cores passou a ter uma fonte única em `ThemeService`, compartilhada por preferências e menus.
- Testes ampliados para seleção da paleta, reset, cor personalizada e comando de acento (106 no total).

## [1.4.5] — 2026-11-20

### Adicionado

- Pré-visualização de sintaxe para o tema selecionado nas preferências, com aplicação temporária imediata e restauração ao cancelar.
- Cor de destaque configurável por predefinições ou código hexadecimal de seis dígitos; a escolha atualiza a interface e é persistida nas configurações.
- Paleta ampliada para 24 opções nomeadas, com código hexadecimal disponível ao passar o mouse.
- Testes para o preview de preferências, validação da cor, paleta e persistência (102 testes no total).

## [1.4.4] — 2026-11-20

### Alterado

- Proposta 3 escolhida: folha de código azul-escura, contorno luminoso ciano/azul/violeta e sinal de “mais”.
- O ícone vetorial é compartilhado pelo ícone da aplicação, title bar, Welcome e About; o wordmark destaca “More” em ciano.
- Documentação atualizada com a direção da marca e os pontos de integração.

## [1.4.3] — 2026-11-20

### Alterado

- Trocar de sessão não pede mais para salvar. O workspace atual é gravado automaticamente como rascunhos da sessão atual antes de abrir a próxima, com o mesmo comportamento sem prompts da preservação ao fechar o aplicativo.
- Se o workspace atual ainda não tem sessão nomeada, o editor cria automaticamente uma sessão **Unsaved workspace** para não descartar edições.
- A confirmação ao abrir outra sessão e ao criar uma sessão vazia foi removida; a troca é direta e preserva o conteúdo anterior.

### Corrigido

- Edições não salvas não são mais perdidas ao alternar entre sessões.
- Selecionar uma sessão no menu voltou a abrir a sessão corretamente; o fechamento do flyout agora acontece depois da execução do comando.
- O menu de sessões agora fecha ao selecionar uma sessão ou executar uma ação.
- Testes ampliados para troca de sessão com edições pendentes e para o fallback de workspace sem sessão nomeada (96 testes no total).

## [1.4.2] — 2026-11-20

### Corrigido

- Abas não salvas restauradas de uma sessão nomeada não aparecem mais como alteradas sem motivo; o conteúdo da sessão é a nova base e o indicador só volta ao editar.
- Salvar uma aba não salva restaurada de uma sessão grava no rascunho da própria sessão, sem abrir o diálogo de **Save As**. Use **Save As...** para exportar para um arquivo real.
- Salvar um documento sem alterações não abre mais o seletor de arquivo.
- Testes ampliados para restauração limpa, edição, salvamento de rascunhos e múltiplas sessões independentes (94 testes no total).

## [1.4.1] — 2026-11-20

### Adicionado

- Sessões nomeadas agora preservam abas não salvas e arquivos com edições pendentes. O conteúdo é gravado em rascunhos próprios da sessão, em `session-drafts/`, sem alterar os arquivos originais.
- Ao reabrir uma sessão, abas novas e abas com alterações voltam exatamente como estavam, marcadas como não salvas.
- Opção **New empty session...** no chip de sessão para criar e ativar uma sessão sem arquivos, fechando as abas atuais pelo fluxo normal de confirmação.

### Alterado

- Removido o chip de pasta/título duplicado da barra superior. A sessão fica somente no chip da direita.
- Rascunhos de sessões são removidos ao excluir a sessão e arquivos obsoletos são limpos ao salvar novamente.
- Testes ampliados para sessões com abas não salvas e abas somente novas (92 testes no total).

### Corrigido

- Não é mais necessário ter pelo menos um arquivo salvo no disco para criar uma sessão.

## [1.4.0] — 2026-11-19

### Adicionado

- **Sessões nomeadas**: salvar as abas atuais com um nome, abrir, renomear e excluir sessões. Cada sessão guarda caminhos, caret, aba fixada, cor da aba e estado somente leitura.
- **Chip de sessão** na barra de workspace, com lista de sessões, ações de salvar/renomear/excluir e menu de gerenciamento por sessão (⋯).
- **Menu de contexto das abas** com:
  - Fechar, fechar outras, fechar à esquerda/direita e fechar todas;
  - Fixar/desafixar aba (abas fixadas não mostram o X);
  - Salvar e Salvar como;
  - Abrir em → pasta ou terminal;
  - Renomear arquivo;
  - Mover para a lixeira;
  - Recarregar;
  - Alternar somente leitura;
  - Copiar caminho, nome ou conteúdo;
  - Mover aba para esquerda/direita/início/fim;
  - Aplicar cor na aba.
- Serviço de lixeira multiplataforma em `FileService` (macOS/Linux/Windows Recycle Bin).
- Testes para sessões, ações de aba, pino, cor, somente leitura, fechamento múltiplo e lixeira (89 testes no total).

### Alterado

- O fechamento de abas fixadas fica protegido na faixa de abas.
- Sessões nomeadas complementam a recuperação automática: sessões guardam arquivos; a recuperação guarda rascunhos não salvos.
- Versão do projeto elevada para 1.4.0.

## [1.3.0] — 2026-11-19

### Adicionado

- **Barra de workspace integrada**: marca do app, ações de arquivo, desfazer/refazer, chip da pasta de trabalho, seletor de linguagem com indicador colorido e menu de ações rápidas.
- **Title bar próprio no macOS**: sem a barra de título do sistema, com traffic lights desenhados na barra, arraste da janela e redimensionamento preservado.
- Cores por linguagem nos badges das abas.
- Toolbar em grupos compactos e visual de “IDE” inspirado em barras de workspace modernas.

### Alterado

- Layout principal reorganizado em title bar/barra de workspace, abas, editor e status.
- Menu de linguagem movido também para o topo, mantendo o seletor da barra de status.
- Screenshots e documentação atualizados.
- Versão do projeto elevada para 1.3.0.

## [1.2.1] — 2026-11-19

### Corrigido

- Abas novas podiam compartilhar o conteúdo da aba anterior; o `DocumentView` agora desanexa o documento antigo e instala o novo ao trocar o `DataContext` (inclui teste de regressão na ferramenta de screenshots).
- O conteúdo de novas abas é sempre independente.

### Alterado

- Fechar o aplicativo não pergunta mais por arquivo quando a preservação está ativa. Os rascunhos não salvos são mantidos no snapshot de recuperação e restaurados na próxima sessão, **sem gravar o arquivo original**.
- **Keep unsaved changes between sessions (no prompt on exit)** substitui o rótulo antigo de crash recovery nas preferências. Desmarcar volta ao fluxo Save / Don’t Save / Cancel.
- Mensagem de restauração passou a indicar “from the previous session”.
- Testes atualizados para 86 casos.

## [1.2.0] — 2026-11-19

### Adicionado

- **Recuperação após fechamento inesperado**: snapshot automático do workspace (metadados + conteúdo de documentos sujos/não salvos) a cada poucos segundos; ao reabrir, abas e alterações são restauradas e marcadas como não salvas. O fechamento normal limpa a recuperação.
- **Reabrir com codificação**: menu **Encoding** com detecção automática e reabertura forçada em UTF-8, UTF-8 BOM, UTF-16 LE/BE, ANSI e ISO-8859-1.
- **Save with Encoding** explícito no menu Encoding, mantendo a barra de status como indicador da codificação de gravação.
- **Menu nativo do macOS** com About, Preferences, File, Edit, Search, Encoding, View e Help; o menu interno continua nas demais plataformas e no modo headless.
- **Detecção de alteração externa**: arquivos limpos são recarregados automaticamente; documentos com alterações pedem confirmação antes de descartar; exclusão no disco é informada sem fechar a aba.
- **Visual modernizado**: nova paleta escura/clara, toolbar em grupos arredondados, identidade visual no topo, abas com destaque inferior, barra de status com chips, tela de boas-vindas com brilho suave, seleção de texto e checkboxes na cor de destaque do app.
- Serviços `SessionService` (recuperação) e `FileWatchService` (observação de arquivos).
- Suíte com 85 testes, incluindo recuperação, watcher, codificação forçada e alterações externas.

### Alterado

- Tema escuro e claro refinados; acento verde aplicado aos controles nativos do Fluent.
- Documentação e screenshots atualizados.
- Versão do projeto elevada para 1.2.0.

## [1.1.0] — 2026-11-19

### Adicionado

- **Find in Files**: busca recursiva em pastas com máscaras, expressões regulares, match case, palavra inteira, inclusão de subpastas e opção de ignorar pastas ocultas/de build.
- Detecção de arquivos binários e suporte a codificações na busca em arquivos.
- Cancelamento, progresso e limite de 5.000 resultados na busca em arquivos.
- Abertura do resultado na linha correspondente, com seleção do trecho.
- Documentação em três níveis: guia do usuário, arquitetura e guia de desenvolvimento/IA; `AGENTS.md` e roadmap.
- Ferramenta `NotepadMoreOS.UiSnapshot` para renderizar a UI com Avalonia Headless + Skia e detectar erros de binding.
- Screenshots versionados de tema escuro/claro, busca, busca em arquivos, boas-vindas, preferências e sobre.
- Estilos visuais para botões desabilitados.

### Alterado

- Menu **Search** reorganizado; `Cmd+Shift+F` agora abre **Find in Files**.
- README, atalhos e limitações atualizados.
- Versão do projeto elevada para 1.1.0.

### Corrigido

- Proxies da barra de status evitam erros de binding quando não há documento ativo.
- Fonte do editor é convertida para `FontFamily` no ViewModel, eliminando falha de conversão.
- Documento inicial do editor e tema de controle derivado evitam erros internos do AvaloniaEdit.
- Heurística de UTF-16 sem BOM mais segura para não classificar binários pequenos como texto.
- Pluralização do resumo da busca em arquivos.
- Botões customizados agora indicam visualmente o estado desabilitado.

## [1.0.0] — 2026-11-19

### Adicionado

- Editor multiaba em Avalonia 12 / .NET 10 com AvaloniaEdit e realce TextMate.
- Operações de arquivo, recentes, sessão, codificações e fins de linha.
- Busca/substituição com regex, palavra inteira, match case, seleção e painel de resultados.
- Temas, preferências, zoom, quebra de linha, indentação e atalhos multiplataforma.
- Testes automatizados de serviços e ViewModels.
