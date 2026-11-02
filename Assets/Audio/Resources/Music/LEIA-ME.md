# Músicas

Solte aqui os arquivos de música (`.ogg`, `.mp3` ou `.wav`) com estes nomes, e o `MusicPlayer`
toca cada uma no lugar certo, em loop, com o volume de Configurações > Música:

| Arquivo        | Onde toca                                   |
|----------------|---------------------------------------------|
| `menu`         | menu principal                              |
| `arena`        | Arena                                       |
| `masmorra1`    | fases 1-1 a 1-9 (e assim por diante: `masmorra2`...) |
| `campanha`     | qualquer fase sem a música da sua masmorra  |
| `chefe`        | fases de chefe (1-9, 2-9...), se existir    |
| `ciclope`      | Arena, enquanto o Ciclope está vivo (`ArenaBossLoot`) |

Exemplo: `Assets/Audio/Resources/Music/menu.ogg`. Sem arquivo, aquele lugar fica em silêncio.
A troca entre músicas tem um fade curto, e a mesma música continua tocando quando a próxima cena usa o mesmo arquivo.

## Trilha Dark Fantasy (2026-11-02)

A direção de arte do jogo é Dark Fantasy, e a música segue: coral, cordas graves, tambores de
guerra, órgão, sinos e escalas menores/frígias. Referências de estilo (nada copiado): Dark Souls,
o Tristram do Diablo, Darkest Dungeon e Blasphemous. As três faixas são sintetizadas do zero por
`Tools/MusicGen/dark_fantasy.py`, então são nossas para usar, e todas fecham o loop sem emenda:

| Arquivo       | Faixa | Como é |
|---------------|-------|--------|
| `menu.ogg`    | O Despertar | Ré menor, 66 BPM, 87 s: violão sozinho sobre um bordão, depois violoncelo, depois o coral com o tema; sinos ao longe e vento |
| `arena.ogg`   | Arena de Sangue | Dó menor, 120 BPM, 96 s em 6 partes: galope de cordas graves, tambores de guerra, tema nos metais que o coral assume, uma parte frígia só com órgão e batida de coração, e o clímax |
| `ciclope.ogg` | O Olho do Abismo | Mi frígio, 140 BPM, 55 s: órgão, coral cantando sílabas curtas, taikos e metais, sino a cada dois compassos |

Para gerar de novo: `python3 Tools/MusicGen/dark_fantasy.py menu|arena|ciclope <saida.ogg>` (numpy + ffmpeg).
As faixas chiptune antigas (`arena_music.py`, `arena_music_2.py`) ficam no repositório só como histórico.
Para trocar uma música, é só substituir o arquivo mantendo o nome.
