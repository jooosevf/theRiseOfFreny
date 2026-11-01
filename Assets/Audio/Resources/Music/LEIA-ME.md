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

`arena.ogg` é uma faixa provisória (chiptune em Mi menor, 132 BPM, 87 s em loop, em seis partes),
gerada por `Tools/MusicGen/arena_music_2.py`. A primeira faixa (Ré menor, 150 BPM, 51 s) ainda pode
ser gerada de novo com `Tools/MusicGen/arena_music.py`. Para trocar, é só substituir o arquivo mantendo o nome.

`ciclope.ogg` (Mi frígio, 168 BPM, 46 s em loop, bumbo duplo) é gerada por `Tools/MusicGen/boss_music.py`.
