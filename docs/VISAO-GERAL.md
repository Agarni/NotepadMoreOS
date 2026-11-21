# Visão geral — NotepadMoreOS

**NotepadMoreOS é um editor de texto e código gratuito e multiplataforma, inspirado no Notepad++, para quem precisa abrir, editar e organizar arquivos de texto sem complicação.**

Sua identidade visual combina uma folha de código azul-escura e um sinal de “mais”, com contornos luminosos em ciano, azul e violeta.

Ele funciona em macOS, Windows e Linux e foi pensado para ser rápido, legível e previsível: abas, realce de sintaxe, busca, codificações e sessões — sem depender de um único sistema operacional.

## O que já funciona

- Abrir vários arquivos em abas, arrastar arquivos para a janela e reabrir recentes.
- Salvar, salvar como, salvar tudo, recarregar do disco e restaurar a sessão anterior.
- Realce de sintaxe para 63 linguagens, com detecção pela extensão.
- Buscar e substituir no arquivo atual, com regex, palavra inteira, maiúsculas/minúsculas e busca na seleção.
- Buscar em todos os arquivos de uma pasta, filtrando por máscara e abrindo o resultado na linha exata.
- Escolher codificação (UTF-8, UTF-16, ANSI etc.), reabrir com codificação forçada e fim de linha (CRLF, LF, CR).
- Recarregar automaticamente quando outro programa altera um arquivo limpo; pedir confirmação quando há edições locais.
- Preservar rascunhos e abas não salvas entre sessões, sem sobrescrever os arquivos originais.
- Criar sessões nomeadas de arquivos e abas não salvas, alternar entre elas sem prompts e gerenciá-las.
- Usar menu de contexto nas abas para fechar múltiplas, fixar, colorir, renomear, recarregar e outras ações.
- Desfazer/refazer, duplicar/apagar linha, comentar, ir para linha e inserir data/hora.
- Temas claros e escuros, barra de workspace, title bar próprio no macOS, menu nativo, zoom, quebra de linha e preferências persistentes.
- Pré-visualizar temas nas preferências e escolher a cor de destaque em uma paleta de 24 opções ou com um código hexadecimal personalizado.

## Em três passos

1. Abra o programa.
2. Use **New file** ou **Open file** (ou arraste um arquivo para a janela).
3. Edite e salve com `Cmd+S` no macOS ou `Ctrl+S` no Windows/Linux.

O ponto verde na aba e o asterisco no título indicam que há alterações não salvas. Ao fechar, o editor pergunta o que fazer.

## Para quem é

- **Pessoas não técnicas** que precisam de um bloco de notas melhor, com abas, busca e codificação correta.
- **Programadores** que querem um editor leve para arquivos isolados, logs, configurações e pequenos ajustes de código.
- **Equipes** que precisam de uma ferramenta gratuita, aberta e multiplataforma.
- **Desenvolvedores e IAs** que querem entender ou estender o projeto com documentação técnica e convenções explícitas.

## Como está organizado

Sem entrar em detalhes de implementação:

- **Interface:** uma janela com menu, barra de ferramentas, abas, editor e barra de status.
- **Idioma:** interface em inglês ou português do Brasil, seguindo o sistema por padrão.
- **Documentos:** cada aba tem seu próprio conteúdo, histórico de desfazer, codificação e posição de cursor.
- **Serviços:** leitura/gravação de arquivos, busca, temas, configurações e diálogos ficam separados da interface.
- **Testes:** os comportamentos principais são verificados automaticamente; uma ferramenta separada gera capturas de tela para revisão visual.
- **Persistência:** preferências e sessão ficam em um arquivo JSON na pasta de configuração do sistema.

Para os detalhes técnicos, consulte a [Arquitetura](ARQUITETURA.md); para mudanças no código, o [Guia de desenvolvimento](DESENVOLVIMENTO.md).

## O que ainda não existe

- Impressão, macros e plugins.
- Lista de funções, mapa de documento e explorador de pastas.
- Comparação de arquivos e edição de arquivos muito grandes com carregamento parcial.
- Instaladores assinados e localização da interface.

O [Roadmap](ROADMAP.md) organiza as prioridades.

## Documentos relacionados

| Documento | Público |
|---|---|
| [Guia do usuário](GUIA-USUARIO.md) | Qualquer pessoa |
| [Arquitetura](ARQUITETURA.md) | Técnico |
| [Desenvolvimento](DESENVOLVIMENTO.md) | Técnico / IA |
| [Roadmap](ROADMAP.md) | Produto e comunidade |
| [AGENTS.md](../AGENTS.md) | Agentes de IA |
| [Changelog](../CHANGELOG.md) | Todos |
