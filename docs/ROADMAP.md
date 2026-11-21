# Roadmap — NotepadMoreOS

O roadmap é uma prioridade, não uma promessa. Itens podem ser reordenados conforme feedback.

## Estado atual (1.7.2)

- Editor multiaba com TextMate e 63 linguagens
- Busca e substituição com regex, palavra inteira, match case, escopo de seleção e painel de resultados
- Busca em arquivos com máscaras, subpastas, ignorar pastas de build, detecção de binário, codificação e cancelamento
- Codificação e fim de linha com detecção, conversão e reabertura forçada
- Alteração externa detectada com reload automático ou confirmação
- Recuperação automática de abas e conteúdo não salvo após crash
- Menu nativo do macOS e menu interno nas demais plataformas
- Salvamento atômico, recentes e restauração de sessão
- Temas claro/escuro + 21 temas TextMate, pré-visualização nas preferências, paleta de 24 cores de destaque, barra de workspace/title bar própria no macOS e badges coloridos por linguagem
- Sessões nomeadas com gerenciamento, criação de sessão vazia, troca sem prompts, rascunhos de abas não salvas e menu de contexto completo nas abas
- Preferências, zoom, quebra de linha, indentação, comentários, ir para linha
- Interface em inglês e português do Brasil (recursos `Strings.resx`/`Strings.pt-BR.resx`, escolha em Preferências ou idioma do sistema)
- Instaladores: DMG com arrastar-para-Aplicativos no macOS, assistente Inno Setup no Windows e pacotes `.deb` + `.tar.gz` no Linux
- Testes automatizados e ferramenta de screenshots headless

## Próximo

| Prioridade | Item | Por quê |
|---|---|---|
| Alta | **Assinatura e notarização** | Remover os avisos de Gatekeeper/SmartScreen (depende de certificados) |
| Alta | **Mais idiomas** | A estrutura de recursos já existe; falta traduzir e validar |
| Média | **Filtros de exclusão na busca em arquivos** | Pastas/arquivos personalizados além dos padrões |
| Média | **Histórico e persistência da busca em arquivos** | Reaproveitar pasta, máscara e últimas buscas |
| Média | **Recuperação: diálogo de revisão** | Mostrar quais abas foram recuperadas e permitir descartar antes de abrir |
| Média | **Sessões: gerenciador dedicado** | Janela com lista, detalhes, atalhos e reordenação de sessões |
| Média | **Menu nativo em mais plataformas** | Avaliar menu global no Linux/Windows |

## Depois

- Impressão e visualização de impressão
- Macros graváveis e editáveis
- Sistema de plugins com API documentada
- Lista de funções (function list)
- Mapa de documento (minimap/document map)
- Explorador de pastas / “folder as workspace”
- Comparação de arquivos (diff) integrada
- Visualização dividida (split view) do mesmo ou de dois documentos
- Marcação e navegação entre marcadores
- Modo coluna/edição em bloco mais completo
- Suporte a arquivos muito grandes (carregamento virtualizado)
- UDL (User Defined Languages)
- Atualização automática
- Múltiplas janelas
- Wayland/nativo e polimento por plataforma

## Ideias de comunidade

- Temas adicionais e importação de temas TextMate do usuário
- Paletas de comandos (command palette)
- Integração com Git (status/blame/diff)
- Terminal integrado
- Sincronização opcional de preferências
- Traduções comunitárias

## Como contribuir

Leia [DESENVOLVIMENTO.md](DESENVOLVIMENTO.md), escolha um item, abra uma issue descrevendo o escopo e envie uma mudança pequena e testada. Mudanças de UI devem incluir screenshots atualizados via `tools/NotepadMoreOS.UiSnapshot`.
