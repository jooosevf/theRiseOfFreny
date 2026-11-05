# Art Bible: The Rise of Freny

> **Status**: Draft
> **Owned By**: art-director
> **Last Updated**: 2026-11-05
> **Art Director Sign-Off (AD-ART-BIBLE)**: SKIPPED 2026-11-04 — lean mode
> **Aprovação**: o José respondeu às decisões em 2026-11-05 e segue lendo o documento. As respostas estão em "Decisões do José".
> **Foco atual**: só a arena. Campanha e cosméticos vêm depois, e o que este documento diz sobre eles é direção futura, não trabalho de agora.

## 1. Visual Identity Statement

**Regra visual: um homem de osso e ferrugem contra um mundo azul-negro, onde o fundo nunca disputa atenção e tudo que acende significa alguma coisa.**

Este documento é um retrofit. A arte a 4x, a paleta azul-marinho dos inimigos, o elmo de osso e a rampa ameixa e ferrugem do Freny são a identidade existente e ficam. As regras novas valem para arte nova; a arte antiga só muda nos pontos que cada seção nomeia. O gênero é Dark Fantasy em tudo, por regra do projeto. O brief do jogo não tem pilares formais, então os princípios se ancoram na linha "quem joga e o que sente": mundo sombrio com recursos escassos, e ver o número crescer.

**Princípio 1: o escuro é o fundo, não o inimigo.** O clima vem de um mundo quase preto, mas o jogador nunca pode perder quem o ataca. O corpo do inimigo continua escuro, e a borda e os olhos carregam a leitura. Teste: quando clima e leitura brigarem, escureça o fundo ou acenda a borda; não clareie o corpo inteiro. Serve a "mundo sombrio".

**Princípio 2: cor acesa é informação.** A escassez também é visual, e uma cor saturada sem significado é ruído. Teste: quando uma peça nova pedir uma cor forte, pergunte o que ela diz ao jogador; se a tabela semântica não cobrir, baixe a saturação. Serve a "recursos escassos".

**Princípio 3: o retorno cresce no primeiro plano.** O hype e o peso dos golpes vão para Freny, para os inimigos atingidos e para os números, e o cenário recua. Teste: na dúvida entre um efeito no cenário e um no primeiro plano, escolha o primeiro plano. Serve a "ver o número crescer" e à direção de combate que cresce e recompensa.

## 2. Mood & Atmosphere

Os dois modos têm clima próprio e a mesma identidade. A campanha é contida e escassa: pouca luz, pouca cor, poucos efeitos. A arena começa sóbria e sobe em hype. Nenhuma luz abaixo foi vista rodando; são alvos para testar na Unity.

**Exploração da campanha.** Emoção: inquietação paciente, de quem quer ver a próxima sala e teme o que gasta para chegar lá. Luz: azul frio, baixa intensidade global, contraste local alto em poucas fontes; interior ou noite. Descritores: úmido, silencioso, pesado, abandonado. Energia: contemplativa. Carrega o clima: o tile quase preto e uma única fonte de luz por trecho de tela, sempre em lugar que importa.

**Área segura e checkpoint.** Emoção: alívio breve, o da sala segura de Resident Evil. Luz: a única luz quente e estável da campanha, em ferrugem e âmbar, intensidade média, baixo contraste, sem pulso. Descritores: abrigado, morno, parado, íntimo. Energia: repouso. Carrega o clima: `Savepoint.png`, a banca do Mercador e o calor em volta deles. Na campanha, calor significa segurança.

**Perigo e combate da campanha.** Emoção: tensão curta, com medo de gastar o que resta. Luz: a mesma luz fria da exploração; o que muda é o que acende (olhos, telegrafia, impacto). Descritores: seco, brusco, abafado, punitivo. Energia: picos curtos entre pausas. Carrega o clima: o golpe escasso e pesado, com efeito pequeno e flash branco, sem banners nem números enormes.

**Chefe.** Emoção: assombro seguido de medo; o tamanho avisa antes da barra. Luz: a sala escurece um passo e uma fonte fria recorta o chefe por trás, com contraste alto. Descritores: monumental, solene, opressivo, definitivo. Energia: medida, com explosões telegrafadas. Carrega o clima: `Boss Door.png`, a escala de 60 a 70 pixels de arte contra os 32 do Freny, e `Boss Health.png` larga na tela.

**Arena no início.** Emoção: prontidão de quem entra para bater recorde. Luz: herda a hora do ciclo, com contraste médio e cenário bem visível. Descritores: aberto, limpo, sóbrio, à espera. Energia: medida. Carrega o clima: a tela quieta e o HUD pequeno, e a voz do narrador quando a chave `Narrator` está ligada.

**Arena em hype alto.** Emoção: euforia de domínio; mais um abate para ver o número subir. Luz: o fundo escurece e o primeiro plano acende, com o maior contraste do jogo. Descritores: ruidoso, cintilante, vertiginoso, triunfal. Energia: frenética. Carrega o clima: o combo que cresce, muda de cor e treme (`ComboHype`) e os banners de abate múltiplo (`MultiKill`).

**Lua de Sangue e Ciclope.** Emoção: alarme e oportunidade; a corrida virou outra coisa. Luz: noite com ambiente vermelho frio, lua grande e vermelha, paleta puxada para vinho. Descritores: agourento, febril, cerimonial, sufocante. Energia: frenética com peso. Carrega o clima: a lua e a entrada do Ciclope. A Lua precisa funcionar sozinha, porque continua existindo com a chave `Cyclops` desligada. Sob luz vermelha o vermelho deixa de avisar, então o perigo se lê por forma, pulso branco e som. O vinho `#600820` do Ciclope tem 1,48 sobre o tile e some; seus olhos e telegrafias seguem o ponto focal da regra de recorte. O verde do vômito é o que mais salta aqui, e isso é bom.

**Morte.** Emoção: perda seca; na campanha pesa, na arena pede outra tentativa. Luz: a cor sai do mundo, Freny fica com a dele por um instante, e a tela desce para `#05030D`. Descritores: silencioso, frio, final, seco. Energia: queda súbita até zero. Carrega o clima: a animação de 7 quadros e `Game Over.png`. Na arena a pontuação aparece logo, porque ver o número é parte do prazer.

**Vitória.** Emoção: alívio com orgulho, sem festa colorida. Luz: a primeira luz quente depois do combate, com ouro só na recompensa. Descritores: dourado, sóbrio, merecido, breve. Energia: descendente, com um pico curto no prêmio. Carrega o clima: o baú prateado ou dourado e as faíscas de ouro.

**Menus e Armeira.** Emoção: o silêncio antes de voltar; na Armeira, o prazer de escolher com o que lutar. Luz: o fundo roxo escuro de hoje, com Freny em destaque; na Armeira, luz neutra e estável para julgar o elmo e as armas pela cor real. Descritores: noturno, arcano, contido, cuidadoso. Energia: repouso. Carrega o clima: o roxo-magenta, que o José vê como parte de Dark Fantasy, e a arte atual do menu, que fica.

**Poço de Ácido (desligado).** A chave `AcidPit` está desligada desde 2026-11-02, mas a arte existe. O clima é náusea e calor doentio: luz verde vinda de baixo sobre o tile de sempre. É o único lugar em que o verde ácido é o ambiente.

### Escada de hype

Os limiares em números são do design de jogo; aqui fica só a aparência. A cor sobe por valor e calor, e o tamanho e o som reforçam. Com `ComboHype` desligada, o jogo volta à linha simples e isso continua correto. Nenhum efeito cobre a tela com flash repetido. A opção "Reduzir tremor e flash" ainda não existe e precisa ser criada em Configurações; `Features.cs` não serve para isso, porque é chave de release e não do jogador. O que ela cobre: `ScreenKick.Kick`, o crescimento do combo em `CombatHud`, o piscar de `ComboManager`, o flash de `ArenaPowerUps` e `HitFlash`.

| Degrau | Cor do combo | Fundo | Primeiro plano |
|---|---|---|---|
| Base | Osso `#B0A088` | Normal | Faíscas pequenas |
| Aquecendo | Ouro `#D8A028` | Levemente escurecido | Número maior, faíscas mais longas |
| Alto | Ferrugem `#C87038` | Escurecido | Tremor de tela, rastro nos golpes |
| Furor | Vermelho `#D02828` com pico branco | No mais escuro | Banners, voz, partículas no máximo |

### Dia e noite da arena

O ciclo fica: é pedido do José (2026-10-28) e dá ritmo à corrida, com 1 minuto de dia e 2 de noite. A noite é o estado de identidade. O dia é a maior fuga de Dark Fantasy do jogo: céu azul limpo, de `#4A88D4` a `#B0D4EC`. José já disse que ficou claro demais e que os inimigos ficam azuis.

Recomendo trocar a cor do dia, não o ciclo: um **dia de cinza**, sob céu coberto e frio, de `#7C8884` a `#A8B0A8`, com sol velado. A tabela mostra que o problema atual é de clima mais que de leitura (razões por cor de arquivo, sem luz).

| Céu | Navy `#283050` | Calça `#402838` | Túnica `#806868` | Elmo `#B0A088` |
|---|---|---|---|---|
| Atual, topo `#4A88D4` | 3,54 | 3,65 | 1,41 | 1,43 |
| Atual, base `#B0D4EC` | 8,27 | 8,54 | 3,29 | 1,64 |
| Proposto, topo `#7C8884` | 3,51 | 3,62 | 1,40 | 1,44 |
| Proposto, base `#A8B0A8` | 5,80 | 5,98 | 2,31 | 1,15 |

A luminância máxima do céu cai de 0,62 para 0,42, e o matiz deixa de ser o do inimigo. O ponto fraco é o elmo perto do horizonte, pior que hoje; ali a separação depende do matiz quente sobre cinza frio e precisa de teste em tela. De dia os inimigos se leem como silhueta escura sobre céu claro, então o aro da regra de recorte só é exigido à noite. As torchas azuis de dia ficam como chama espectral, só na arena, e o crepúsculo laranja também fica.

## 3. Shape Language

**Silhueta.** A forma precisa se ler sem cor, porque o corpo do inimigo é escuro. Teste: a peça preenchida de uma cor só, em escala real, ainda diz quem é. Cada arquétipo tem um único traço de assinatura (tabela na seção 5).

**Hostil e humano.** Freny é vertical, estreito (14 x 32) e tem cabeça grande. Os inimigos são proporcionalmente mais largos que ele (do Golem, 22 x 31, ao Ciclope, 56 x 65), ou têm massa desigual, pontas ou apêndices. Nenhum inimigo novo pode ter o perfil humano estreito com cabeça clara.

**Ambiente.** Domina a pedra pesada e retangular, com cantos chanfrados, arcos redondos e cortes diagonais, como em `Tileset.png`. A ameaça rompe esse retângulo com pontas (`Falling spike`, espinhos do tileset). Curvas orgânicas são do que invade a pedra: hera, teias, tentáculos e líquidos.

**UI.** A UI usa placas retangulares na grade do jogo, em roxo escuro, sem cantos arredondados, cápsulas ou brilho vítreo. Bordas retas são estáveis; moldura e barra marcam seleção.

**Protagonistas e coadjuvantes.** Atraem o olho: o elmo de Freny, olhos de inimigo, telegrafia, itens e números. Recuam: tile, céu, props e o corpo escuro dos inimigos.

## 4. Color System

### Paleta primária

Todos os valores vêm dos arquivos. O contraste é sobre o tile `#080810`, sem luz.

| Cor | Hex | Papel no mundo | Contraste |
|---|---|---|---|
| Abismo | `#080810` | O mundo: pedra, noite, o que engole | 1,00 |
| Azul-marinho | `#283050` | A carne de tudo que é hostil | 1,55 |
| Ameixa | `#402838` | A sombra de Freny; o humano que resiste no escuro | 1,50 |
| Osso | `#B0A088` | O elmo de Freny; o claro que é dele | 7,82 |
| Ferrugem | `#C87038` | Luvas, gola e fogo que abriga; calor e segurança | 5,53 |
| Sangue | `#D02828` | Dano, perigo, urgência | 3,82 |
| Ouro | `#D8A028` | Recompensa, moeda, baú, conquista | 8,53 |

Cores de apoio: violeta `#484098` e `#303078`, túnica `#806868`, vinho `#600820` e o branco puro do flash de golpe.

### Semântica

| Cor | Significa | Onde vale | Não pode |
|---|---|---|---|
| Vermelho vivo `#D02828`, `#FF4033` | Dano e perigo agora | Golpes, avisos, números, frasco de vida. Exceção aprovada pelo José: texto de combo e banner no HUD, com contorno preto | Decorar cenário ou cosmético comum; no mundo, marcar o combo |
| Vinho `#600820` | Elite, sangue velho, Fúria | Corpo de elite, marcas | Ser o único aviso de um ataque |
| Ferrugem `#C87038` | Calor, abrigo, jogador | Freny, checkpoint, torchas | Marcar inimigo comum |
| Ouro `#D8A028` | Recompensa | Moeda, baú, conquista, combo | Fazer parte de inimigo ou ameaça |
| Violeta `#484098` | Magia e sobrenatural hostil, no mundo | Inimigos, projéteis | Marcar item ou recompensa no mundo. O roxo-magenta dos menus é outra família e fica |
| Verde ácido `#44FF00` (aprox.) | Corrói e envenena | Poço, vômito do Ciclope | Decorar, ou estar em cosmético |
| Azul-claro do diamante | Valor raro | Diamante | Efeito de inimigo |
| Branco puro | Impacto, instante | Flash de golpe, pico de combo | Cor fixa de peça |

Conflitos registrados:

- **Verde ácido.** A convenção das folhas v2 era "verde ácido só no Poço", mas o vômito do Ciclope já é verde na arena clássica. Mudo a regra para "verde ácido só no que corrói", porque o significado é o que importa e o Ciclope já o cumpre. O olho verde do Rato da v1 sai, se o Rato não corroer.
- **Verde de cura e de missão.** `#59FF66` e `#4DBF59` são vizinhos do ácido. Ficam, porque um é número com sinal de mais e o outro é interface, e nenhum aparece como poça no chão.
- **Roxo do piloto automático.** `#9E47FF` colide com a magia inimiga. Proponho osso com ícone próprio quando a UI for refeita.
- **Mercador em vermelho.** Disputa com o perigo; a leitura é salva pela forma, pela banca e pelo ícone de interação.

### Regra de recorte (inimigo sobre tile)

**Teste de 2026-11-05, na arena:** as paredes de tijolo ficam iluminadas pelas torchas e pelo ciclo do dia, e os inimigos sem aro já se leem bem nas fotos. Os números abaixo são de arquivo, sem luz, e superestimam o problema nesse cenário. O aro testado (`#5868A0`) deixou o inimigo com cara de contorno neon. A regra fica para reavaliar nas masmorras escuras da campanha; na arena, a recomendação passa a ser não aplicar. O José decide vendo as fotos.

O inimigo tem 1,55:1 sobre `#080810` e 1,40:1 sobre `#101818`: clima bom, leitura ruim. O acento violeta `#484098` chega a 2,34, e a 1,99 sobre `#201830`. A regra abaixo é verificável e não mexe no corpo.

1. **Teto do fundo.** Nenhum tile de área jogável passa de 1,2:1 sobre `#080810`. O mais claro medido, `#201830`, tem 1,17.
2. **Corpo livre.** O corpo pode ficar na rampa navy, sem mínimo de contraste.
3. **Borda.** Em cada quadro de idle e de andar, pelo menos 40% dos pixels da borda externa (pixel opaco vizinho de transparente) têm 3:1 ou mais sobre `#201830`. Cor de referência: `#5868A0`, azul-aço da mesma família, com 3,16 sobre `#201830`, 3,35 sobre `#101818`, 3,71 sobre `#080810` e 3,09 sobre um tile exatamente no teto.
4. **Ponto focal.** Cada quadro tem ao menos um ponto de 4,5:1 ou mais sobre `#201830`, normalmente os olhos. Referências: lilás `#A8A0E8` (7,17) em inimigo comum e `#FF4033` (4,89) em elite.
5. **Verificação em tela.** Uma captura em 1080p, no ponto mais escuro e iluminado da fase, mantém 3:1 entre o aro e o tile vizinho. Quadros de flash ficam de fora.

A borda não precisa fechar: é luz vinda de cima e dos lados, não um contorno. **Isso muda em parte a convenção "sem contorno preto" das folhas v2.** O contorno preto continua proibido, com bom motivo: preto sobre `#080810` dá 1,05:1 e não recorta nada. O que muda é que "sem contorno" não pode mais significar "sem borda clara", porque o corpo navy sozinho não passa em leitura sobre o tile real.

**Arte antiga:** os itens 3 e 4 valem por arquivo só para arte nova. Pelas cores dominantes medidas, os inimigos atuais não devem passar. Proponho aro assado por script em `Tools/ArtGen`, com os mesmos GUIDs e fatias, verificado pelo item 5, e olhos por objeto filho sem luz, como já faz `Enemy.cs`. O script roda antes de `enemy_skins.py` e não pinta os quadros de flash branco. Isso altera os PNGs e pode estourar orçamentos em bytes. Material com shader próprio fica só para o caso de o assado falhar no ponto mais escuro: nem Sprite Lit nem `SpriteFlash.shader` têm aro, e seriam preciso um shader novo, ajustes em 7 prefabs e cuidado com a luz, que escurece o aro. Decisão do José, com teste na Unity.

### Temperatura por área

A campanha tem 18 fases em duas masmorras, e o código cita 9. Nomes e temas das futuras não existem, e este documento não os inventa. A regra fixa só um eixo: **quanto mais avança a campanha, mais a pedra esquenta, sem nunca ficar clara.** Os inimigos navy se destacam cada vez mais. Toda masmorra cumpre o teto de 1,2:1 e não usa vermelho vivo, ouro ou verde ácido como ambiente. Cada uma pode ter um único acento próprio, se não colidir com a semântica.

| Faixa | Masmorras | Matiz do tile | Referência |
|---|---|---|---|
| Fria | 1 a 3 | Azul e azul-esverdeado | `#080810`, `#081010`, `#101818` (existem) |
| Neutra | 4 a 6 | Cinza, ameixa, hematoma | `#201830`, `#101020` (existem) |
| Quente | 7 a 9 | Brasa apagada, vinho seco | `#201410` (proposta; 1,11) |

### Paleta de UI

**O roxo-magenta fica.** O José vê nele muito de Dark Fantasy e não quer trocar tudo de uma vez (2026-11-05). A base é a de `MenuKit.cs` hoje. As cores de combate também ficam (Hit `#FF4033`, Hurt `#D91A26`, Healed `#59FF66`, Energy `#FFDB33`, Rage `#BF2626`), porque são semânticas e o hype pede saturação. Ferro, osso e ferrugem deixam de ser a proposta; só voltam se o José pedir.

| Papel | Hoje, e fica | Observação |
|---|---|---|
| Painel | `#120A21` | Base |
| Botão | `#331F54` | |
| Selecionado | `#AD3DD1` | Foco ganha reforço fora da cor, abaixo |
| Pressionado | `#E68CFF` | |
| Desativado | `#241A33` a 60% | O texto segue claro em botão inativo; escurecer o texto é ajuste à parte |
| Texto | `#F7EDFF` | 17,07 sobre o painel |
| Texto fraco | `#B399D1` | 7,69 sobre o painel |
| Título | `#ED8FFC` | |
| Sombra | `#05030D` | |
| Ouro de conquista | `#FFD94A` | |

**Foco de menu.** Segue a recomendação: moldura de 3 px e barra sólida à esquerda em `#AD3DD1`, com o texto sem mudar. A espessura é o sinal que não depende de cor. O marcador não pode ser ">", já usado em `MenuKit.Opens`. Borda pede objeto novo em botão e linha.

### Daltonismo

Nenhuma cor semântica trabalha sozinha. Os pares de maior risco são vermelho e verde, ferrugem e vermelho, violeta e navy.

| Significado | Risco | Reforço de forma ou ícone | Reforço de som |
|---|---|---|---|
| Dano ao jogador | Vermelho some em protanopia | Ícone de vida esvaziado e número grande; tremor e flash são extras | Som de dano |
| Telegrafia de ataque | Vermelho e fundo | Pose de preparo e pulso branco local, mantido na opção reduzida | Som de aviso próprio |
| Ácido | Verde parece ocre | Poça irregular, bolhas animadas | Chiado |
| Cura | Verde confunde com dano | Sinal de mais, partículas que sobem | Som próprio |
| Recompensa | Ouro e verde se aproximam | Brilho em cruz, giro da moeda, forma do baú | Tilintar |
| Elite | Vinho parece marrom | Olhos acesos e marca fixa no corpo | Som na entrada |
| Magia | Violeta e navy se fundem | Anel ou losango, projétil com rastro | Som de carga |
| Segurança | Ferrugem e vermelho se aproximam | Luz estável sem pulso, forma do checkpoint | Música muda |
| Energia e Fúria | Amarelo e vermelho | Barras em posições fixas; ícones a criar, porque `DrawBars` não desenha ícone | Som ao encher |
| Foco de menu | Depende de cor | Moldura mais grossa e barra lateral | `ui_move` já existe |
| Mina-aranha armada | Lâmpada vermelha em corpo escuro | Pulso que acelera, perna erguida | Bipe que acelera |

## 5. Character Design Direction

**Freny.** É um combatente humano, comum no corpo e estranho na cabeça: elmo de osso com viseira em cruz, túnica cinza-rosa, calça ameixa, luvas e gola ferrugem. Ele luta com lâmina e arma de fogo. Não é herói de brilho: é um sobrevivente que ganha estatura pela forma como luta. Comentários em `Tools/ArtGen` ainda o tratam no feminino, mas é erro antigo e não direção. A leitura dele é em três cores: elmo (7,82), luvas (5,53) e túnica (3,89). A calça tem 1,50 sobre o tile e pode sumir no escuro, levando a posição das pernas; a verificação em tela decide se ele precisa de luz pessoal fraca ou de uma coluna `#605050` na arte.

### Leitura num relance

| Tipo | Como se reconhece | Cor | Tamanho |
|---|---|---|---|
| Freny | Cabeça clara grande, perfil estreito | Osso, ferrugem, ameixa | 14 x 32 |
| Inimigo comum | Corpo escuro e aro frio, com um traço de forma | Navy e violeta | Altura de 16 a 36 |
| Elite | Silhueta da base, corpo vinho ou marca vermelha, olhos `#FF4033` | Vinho sobre navy | Igual à base |
| Chefe | Domina a tela e tem barra própria | Navy com marca própria | Altura de 60 a 70 |
| NPC | Parado, sem aro, cor quente, ícone de interação | Quente, sem navy | Perto do tamanho de Freny |

### Escala

José cobrou escala certa: o Arauto da v1 ficou menor que o Golem. A tabela, medida nos sprites de hoje em pixels de arte (`conceitos_v2.md`), é a referência obrigatória.

| Sprite | Largura x altura | Altura em relação a Freny |
|---|---|---|
| Freny | 14 x 32 | 1,00 |
| Inseto | 17 x 16 | 0,50 |
| Golem | 22 x 31 | 0,97 |
| Feiticeiro | 23 x 33 | 1,03 |
| Escudeiro | 28 x 36 | 1,13 |
| Ciclope | 56 x 65 | 2,03 |

- **Comum e elite:** altura de 16 a 36 pixels de arte.
- **Chefe:** altura de 60 a 70 pixels de arte.
- **Vão de 37 a 59:** fica vazio de propósito, para que um chefe nunca se confunda com um inimigo grande. Exceção registrada: o Tentáculo do Poço tem cerca de 45 de altura visível (estimado em `Tentacle Idle.png`), mas é fixo no chão.
- **Largura:** acima de 56, a do Ciclope, só com decisão registrada. O Boitatá da v2 (70 x 20) cai nesse caso.

Freny ocupa 8% da altura da tela na arena e 12,8% na campanha, cerca de 86 e 138 pixels de tela em 1080p. Por isso a arena exige silhueta mais forte.

### Silhueta por arquétipo

| Inimigo | Pasta | Assinatura | Regra |
|---|---|---|---|
| Inseto | `Bug` | Baixo, horizontal, orelhas pontudas, patas violeta | Nunca passa da cintura de Freny |
| Morcego | `Flying` | V de asas abertas | O único de asas; a forma muda ao bater |
| Golem | `Golem` | Bloco sem pescoço, ombros maiores que a cabeça | Sempre retangular e sem pontas |
| Golem Vermelho | `EnemySkins/GolemRed` | Mesmo bloco, corpo vinho, linha acesa no peito e olhos | Elite por cor e marca, não por tamanho |
| Escudeiro | `Shielded` | Escudo alto com espinhos à frente, elmo com chifres | O lado protegido é óbvio |
| Feiticeiro | `Sorcerer` | Corpo fino, capuz, cajado alto com anel | O único com vara vertical; osso só no cajado, nunca na cabeça |
| Feiticeiro Supremo | `EnemySkins/SorcererSupreme` | Mesmo corpo, vermelho na roupa, flash violeta | Elite por cor; o flash violeta aparece nele na folha de contato |
| Mina-aranha | `SpiderMine` | Cúpula baixa, pernas finas violeta, lâmpada vermelha no topo | A lâmpada é a telegrafia; sempre visível |
| Mina | `Mine` | Bola de pontas | Sem pernas; o estado se lê pelo pulso |
| Licker | `Licker` | Corpo fino pendente, cabeça em gancho e dentes | Em uso na campanha e em cenas do Build Settings (`Licker_Enemy.prefab`), mesmo fora de `ArenaDirector.cs` |
| Ciclope | `Cyclops` | Massa curvada, boca e olho único no alto, braços enfaixados | Chefe; nenhum outro tem olho único grande |
| Crawler (Poço) | `Crawler` | Olho grande verde com pernas pequenas | Só no Poço; o olho é a peça inteira |
| Tentáculo (Poço) | `Tentacle` | Coluna torcida com veios violeta, base fixa | Não anda; base visível para medir o alcance |

**NPC.** O Mercador tem cabelo e roupa vermelhos, um risco de leitura. Fica por ora, porque tem banca, não ameaça e tem rosto. NPC novo não usa navy, aro frio nem olhos acesos, e tem sempre `Interact.png` por perto. Arte nova do Mercador puxa a roupa para vinho e ferrugem e mantém o cabelo como assinatura.

**Pose e expressão.** Rígida no repouso, exagerada no golpe, com antecipação clara. Rosto só existe em retratos (`Player Face`, `Merchant Face`); no jogo a expressão vive no corpo. Nenhum ataque de inimigo começa sem um quadro de preparo legível.

**Detalhe na distância de jogo.** Não há LOD em 2D. O equivalente é que o detalhe interno sobrevive à escala real ou sai: um traço de um pixel num corpo navy só existe se mudar a leitura.

**Cosméticos.** Freny pode trocar de elmo, mas não pode virar inimigo. Qualquer cabeça precisa de área clara ou quente de ao menos 3:1 sobre `#201830`, visível em escala real, e não pode combinar corpo escuro, capuz e olho violeta.

### Folhas de conceito v2 (2026-11-01)

Nada delas está no jogo; são rascunho, não cânone. O José achou os animais do folclore feios: eles ficam para depois, e serão redesenhados, provavelmente por um artista contratado. O Arauto e os cosméticos também ficam para depois. A análise abaixo fica como briefing para essa hora.

**Seguem a direção:**

- A grade a 4x, a paleta atual e a prática de testar em faixa 1x e em folha de mesmo chão, que viram critério de aceitação (seção 8).
- A tabela de escala e as faixas de altura.
- Lobisomem: silhueta curvada, navy, garras claras, papel limpo na Lua de Sangue.
- Mula sem cabeça: fogo no pescoço é uma ideia de forma forte, embora o corpo ainda pareça um cachorro retangular.
- Armas: ícones em 25 x 25 como os Items, formas claras. Não opino sobre o desenho de jogo delas.
- Elmos de leitura clara: emplumado, peste, troféu do Ciclope, caveira, galhada, ferro.

**Fogem da direção:**

- **Arauto do Poço.** A escala está certa (52 x 64), mas o corpo simétrico, reto, em bandas de violeta saturado com viseira parece robô de ficção científica. É a maior fuga da folha.
- **Sino dourado.** Ouro significa recompensa, e o sino está solto do corpo. O próprio `conceitos_v2.md` se contradiz: diz "sem o sino" na revisão e usa o sino na fase 1.
- **Corpo-seco.** O marrom saturado foge dos "inimigos frios" que a própria folha declara, o aproxima da semântica de NPC, e o corpo de palito não tem massa.
- **Boitatá.** Anéis e losangos soltos parecem colar, não serpente, e o fogo violeta perde o que o torna reconhecível.
- **Necromante.** Repete o Feiticeiro e o Cultista: capuz escuro com vara. Precisa de outro traço, ou de sumir.
- **Capuz do feiticeiro, elmo de pedra, capuz do carrasco.** Fazem Freny ler como inimigo.
- **Capas saturadas.** Manto do Poço e capa de musgo em verde vivo, brasão em vermelho vivo e cruz branca dura disputam com a semântica.
- **Fundo de teste.** Os inimigos aparecem sobre placas ardósia mais claras que o tile real. Isso esconde o problema: os navy que parecem bons ali têm 1,55 no jogo.

O folclore brasileiro e o sino e a máscara do Arauto ficam para depois da arena; ver "Decisões do José".

## 6. Environment Design Language

**Arquitetura.** O mundo é um castelo-masmorra de pedra, velho e abandonado: blocos com cantos reforçados, arcos e portas gradeadas, correntes, teias, estandartes rasgados e estátuas de guardas com alabarda. Tudo isso existe em `Tileset.png` e fica. Sugere uma ordem militar antiga caída, sem fechar a história. Por fora, a arena mostra céu noturno, montanhas em silhueta e um castelo flutuante, que ficam como estão. Os céus de dia são o único ponto a mudar.

**Textura.** Pixel art a 1x ampliada 4x, cor chapada e rampas curtas. Sem pintura suave, gradiente de alta resolução ou PBR: a luz vem das luzes 2D, não da textura. O bloco é pouco contrastado para os personagens ficarem à frente. Os gradientes e o CRT de `fundo.png`, `Scanlines.png` e `Monitor.png` estão fora do padrão.

**Densidade de props.** Exploração: esparsa, em grupos pequenos, porque cada objeto pode ser recurso ou pista. Área segura: média, com objetos de uso; é o único lugar que parece habitado. Sala de chefe: mínima, porque nada disputa com o chefe. Arena: mínima no piso, para a leitura sob hype. Poço: mínima, com perigos marcados.

**Interativos.** Props não usam valores mais claros que o aro de um inimigo, salvo se forem interativos. O que se pode usar tem brilho ou borda; o que não se pode usar não tem. Num jogo de recursos e puzzles, confundir um vaso quebrável com um de cenário custa confiança. Os vasos e barris do tileset precisam dessa revisão.

**Narrativa do ambiente.** O cenário conta sem texto, nesta ordem: quem esteve aqui, o que deu errado, o que resta. Exemplos no estilo do que existe: pontas com sangue antigo, guardas de pedra diante de portas gradeadas, hera entrando pela pedra. Nas áreas seguras, sinais de quem passou antes, como uma tocha ainda acesa. A arena conta pouco: é um palco.

**O que fica e o que muda.** Fica: tileset, céu noturno, torchas, portas, armadilhas. Muda: céus de dia, se José aprovar, e marcação de interativos. Só para arte nova: faixas de temperatura. O audit trata `Tileset 1`, `Brick 200x200` e os duplicados do Runner.

## 7. UI/HUD Visual Direction

Direção de arte revisada pelo papel de UX, lendo o código de `MenuKit`, `CombatHud` e telas de menu. Nada disso foi testado na Unity nem com controle na mão.

**HUD.** Fica em tela, não diegético, porque ranking e combo pedem números exatos. Na campanha é mínimo, nos cantos. Na arena cresce com o hype, em combo e banners, não em molduras. `HealthFull`, `HealthEmpty`, `HealthArmor` e `Boss Health.png` ficam, sujeitos ao teste de leitura. O pavio da granada junto de Freny é quase diegético, já existe e é bom. Rótulos de HUD têm mínimo de 16 px finais em 1080p: hoje `CombatHud` usa 12 x largura/1920, ou 12 px em 1080p, metade do menu. O encaixe do layout com 16 não foi verificado.

**Roxo-magenta: fica nos menus.** Decisão do José em 2026-11-05. `fundo.png`, `Loading BG.png`, `Scanlines.png`, `Monitor.png` e `Menu player.png` também ficam: a arte do menu não precisa mexer agora. Se um dia as cores mudarem, estão centralizadas só em parte: são 9 no `MenuKit`, mais literais em `MenuKit`, `TouchControls`, `CombatHud` e `InventoryScreens`, e `CombatHud.ReadyColor` herda `TitleColor`. As scanlines nas fases vêm de `Campaign/VhsOverlay.cs`, assunto da campanha.

**Armeira.** Mantém a paleta dos menus: placas, ícones em 25 x 25, Freny com a arma ou o elmo escolhido, luz neutra. Preço em ouro; indisponível com cadeado ou texto, não só com `#A89C8C`. Ouro é sempre dinheiro e não destaca seleção.

**PressStart2P: em teste.** O José aprovou separar as fontes e testar outra (2026-11-05); nada muda sem foto aprovada. A proposta é tirá-la dos menus e dos textos e mantê-la nos números de combate e no ranking até haver substituta. É fonte de fliperama, larga e cansativa em pt-BR acentuado e em frase longa. Mas números de combate pedem peso, e o tom de fliperama combina com pontuação de arena. Trocar tudo de uma vez afetaria todas as telas, então a mudança é em etapas. Até a troca, PressStart2P vale para strings inteiras do HUD de combate, porque a mesma string mistura palavra e número ("Combo: x3"), e os banners de `MultiKill` ficam nela até passarem no teste de leitura em movimento. `MenuKit.Font` é única e alimenta também o HUD, então a etapa exige separar `BodyFont` e `NumberFont` antes.

| Uso | Direção | Candidatas |
|---|---|---|
| Títulos e banners | Pixel com serifa ou gótica leve | Alagard (licença e acentos não verificados) |
| Texto e menus | Pixel estreita, boa em frase longa | m5x7, Pixel Operator (licença e acentos não verificados). ChiKareGo2 está reprovada: não tem ú, ü, … e — (tabela de glifos conferida) |
| Números de combate, ranking | PressStart2P por ora | Mantida |

Candidata a fonte só entra com áàâãéêíóôõúüç, maiúsculas e … —. PressStart2P tem todos os acentos do pt-BR.

**Tamanhos.** O `MenuKit` usa 12, 16 e 28 em referência 1280x720, e `InventoryScreens` tem um 7 literal. PressStart2P é desenhada numa grade de 8, então 16 cai certo, e 12 e 28 não caem. Regra: cada fonte pixel só nos múltiplos da sua grade, medidos em pixels finais (tamanho x escala do Canvas). `CreateCanvas` dá 1,5x em 1080p, e em 1280x800, 1366x768 e 900p nem 16 cai na grade. Por isso a escala do Canvas passa a ser em degraus: 1x até 900p, 1,5x em 1080p, 2x em 1440p, 3x em 4K. Corpo de texto tem mínimo de 14 px de altura de maiúscula na referência; título fica perto de 32. O padrão de `Label` passa a corpo, e 12 só com fonte cuja grade aceite. PressStart a 16 não cabe nos botões de Controles ("Analógico" pede 144 px em 110 úteis), então esse mínimo só vale depois da troca de fonte. Números e banners de combate podem sair da grade só durante o pop (até 0,18 s, o valor do combo hoje) e o throb; em repouso voltam ao múltiplo. Essa exceção está nas decisões pendentes.

**Iconografia.** Silhuetas chapadas na arte do jogo, com no máximo quatro cores da paleta. Conquistas continuam em 40 x 40 texels com PPU 40, e itens em 25 x 25 pixels de arte, o tamanho de `Items 100x100`. Cada ícone precisa se ler como silhueta de cor única. Teclas no HUD seguem o último dispositivo usado: hoje `CombatHud.Key` mostra sempre o teclado, até no prompt de Fúria com controle.

**Animação.** Menu é curto e seco: deslocamento de um pixel de arte, sem elasticidade nem fade longo. Combate é o oposto: crescer, tremer, mudar de cor, conforme a escada de hype. O contraste entre os dois é proposital.

**Menu player.** `Menu player.png` fica como está, por decisão do José (2026-11-05), mesmo fora do pixel do jogo.

**Conflito entre arte e legibilidade.** Uma fonte gótica de verdade serviria ao tom e falharia na leitura; fica só para títulos e, se não passar no teste, sai também deles. Em qualquer conflito de menu, a legibilidade ganha.

## 8. Asset Standards

Esta seção é a entrada do `/asset-audit`. Caminhos relativos a `Assets/`. Revisada pelo papel de Unity, lendo arquivos e `.meta`, sem abrir o editor.

### Arte nova e arte antiga

**Arte antiga** é todo PNG que já existia no commit `a2340c2`: 297 arquivos, 231 deles com espaço no nome. **Nada se renomeia em massa**: há `Resources.Load` por nome, e o GUID do `.meta` nunca pode mudar. A lista oficial é `design/art/legacy-assets.txt`, um caminho por linha, com os 297 PNGs de `a2340c2`, gerada por `git ls-tree`. Se ela faltar, a checagem de nomes do audit é `NOT ASSESSED`.

**Arte nova** é qualquer outro PNG, e o nome dele precisa passar nesta regex:

```
^(hero|enm|npc|env|sky|prop|item|fx|ui|ico)_[a-z0-9]+(_[a-z0-9]+)*(_[0-9]+x[0-9]+)?\.png$
```

Nenhum dos 297 nomes atuais passa nela, o que confirma a separação. O sufixo opcional é o tamanho do quadro em texels. As categorias visuais são: `hero` (Freny e cosméticos), `enm` (inimigo e chefe), `npc`, `env` (tile e cenário), `sky` (céu e camadas de fundo), `prop` (objeto e armadilha), `item` (item, arma, coletável), `fx` (efeito), `ui` (painel, botão, tela) e `ico` (ícone). Exemplos: `enm_lobisomem_walk_120x120.png`, `sky_masmorra3_fundo.png`, `ico_conquista_combo100_40x40.png`.

**A regex vale para o arquivo, não para todo sprite.** Parte do código lê o nome do sprite. Tileset que passa por `StageTerrain` ou `Breakable` mantém o prefixo `Tileset_` nos sprites até o código mudar, porque `Tileset_N`, `Acid` e `Water` definem beirada, espinho e escada, e outro nome vira `Kind.None`. Também dependem de nome `ArenaDayNight` (sufixo " Day"), `FrenyLook`, `FrenyMotion`, `FrameAnimator` (sufixo `_N`), `Achievements`, `ArenaChest`, `ArenaPowerUps`, `BossKey`, `GoldenChest`, `MenuArt`, `MenuPortrait` e `CombatHud`.

**Exceção:** uma skin nova em `Sprites/Characters/Mobs/<Base>/Resources/EnemySkins/<Nome>/` copia nomes de arquivo e de sprite da família base. O `SpriteSkin` troca por `Sprite.name` e, sem par, mostra o original sem aviso; o audit compara os nomes de sprite. O Runner fica fora de todas as regras (minijogo não decidido) e só é listado no relatório.

### Orçamentos por categoria

Bytes vêm de `stat`, dimensões em texels do cabeçalho do PNG, e os limites derivam do inventário. A primeira linha que combinar vale.

| Cat. | Caminho | Limite em bytes | Largura x altura máx. | Maior atual em bytes |
|---|---|---|---|---|
| C1 chefe | `Sprites/Characters/Mobs/Cyclops/` | 64.000 | 2048 x 2688 | `Cyclops Smash` 56.145 |
| C2 personagem | restante de `Sprites/Characters/` | 20.000 | 4096 x 1024 | `traje` 51.107 (fora); depois `blades` 16.309 |
| C3 céu | `Sprites/Environment/Sky/` | 25.000 | 2048 x 2048 | `Night sky Moon` 17.898 |
| C4 ambiente | restante de `Sprites/Environment/` | 50.000 | 2048 x 2048 | `Tileset` 40.640 |
| C5 itens | `Sprites/Items/` e `Sprites/Liquids/` | 8.000 | 1024 x 1024 | `Net Open` 5.255 |
| C6 efeitos | `Sprites/Effects/` | 4.000 | 1280 x 512 | `Scanlines` 345.391 (fora); depois `Heal` 1.656 |
| C7 conquistas | `Sprites/Interface/Resources/Achievements/` | 1.500 | 40 x 40 exato | `oops` 1.198 |
| C8 tela cheia | `Sprites/Interface/`, largura de 1280 ou mais | 200.000 | 1920 x 1200 | `Menu player` 2.496.805 (fora) |
| C9 interface | restante de `Sprites/Interface/` | 30.000 | 1440 x 1440 | `Merchant Face` 26.063 |

Chefe novo entra na C1 quando sua pasta for declarada aqui. Fora hoje, de propósito, ficam 7 dos 228 arquivos fora do Runner:

| Arquivo | Excesso | Destino proposto |
|---|---|---|
| `Sprites/Interface/Main Menu/Menu player.png` | Bytes e largura (5190) | Fica, por decisão do José; dívida aceita |
| `Sprites/Interface/Main Menu/fundo.png` | 1.101.207 bytes | Fica, por decisão do José; dívida aceita |
| `Sprites/Interface/Loading Screen/Loading BG.png` | 677.707 bytes | Fica, por decisão do José; dívida aceita |
| `Sprites/Effects/Scanlines.png` | Bytes e dimensão | Em uso nas fases por `VhsOverlay`; depende do teste da decisão 17, na campanha |
| `Sprites/Effects/Monitor.png` | 17.559 bytes e altura | Fica, por decisão do José; dívida aceita |
| `Sprites/Characters/Player/Resources/FrenyLook/traje.png` | 51.107 bytes e altura (2048) | Aceitar ou reorganizar a folha |
| `Sprites/Characters/Mobs/Licker/Monster attack.png` | Largura 4160 | Ver abaixo |

Como o audit marca NON-COMPLIANT em qualquer violação de tamanho, esse será o veredito até a lista cair. É dívida declarada, não falha nova. `Monster attack.png` (4160, limite 4096 em Standalone) e `FrenyPoses/blades.png` (2160, limite 2048 em Standalone) passam do limite do importador e são reduzidos, o que deve desfazer a grade; o efeito não foi visto. A solução sem mexer na arte é subir `maxTextureSize` na Unity, sem tocar o GUID (`Menu player` já usa 8192). O Licker é inimigo ativo, então isso aparece em jogo.

### Formato e importação

| Campo | Regra para arte nova | Estado da arte antiga |
|---|---|---|
| Formato | PNG 8 bits por canal com alfa | Todos PNG |
| Grade | 1 pixel de arte = 4 texels; largura e altura múltiplas de 4 | Fora: `Continue.png` e outros; só informativo |
| PPU | 100 (25 pixels de arte por unidade); conquistas 40 | 278 com 100; 40 (13); 32 (5, `Crawler` e `Vomit Puddle`); 128 (1, `Environment/Sky.png`) |
| Filtro | Point (`filterMode: 0`) | 226 Point; fora do Runner só 3 não são: `Scanlines`, `Monitor`, `Player jump kick` (todos -1) |
| Compressão | `textureCompression: 1` | 253 com 1; 44 com 0 |
| Modo do sprite | Múltiplo (2) em folhas; único (1) em imagens soltas | Há folhas em modo 1 sem fatias, como `Key 60x60` e `Flying attack` |
| Pivô | Centro (`alignment: 0`); pés na mesma linha em todos os quadros | Centro nas 3 amostras lidas; demais não medidos |
| Tamanho máx. no importador | Maior ou igual ao maior lado, definido no `.meta`; `sheetmeta.py` não define esse campo | Medido nos 297: 5 estouram (2 fora do Runner: `Monster attack`, `blades`; 3 no Runner). O padrão é 2048 em 289 deles, então C1 e C2 exigem subir o campo |
| Geração | Via `Tools/ArtGen` quando couber; `sheetmeta.py` escreve o `.meta` | Existe |

**POT não se aplica.** Folhas de sprite são tiras e grades cujo tamanho vem do quadro, e exigir potência de dois adicionaria vazio e quebraria a grade. A regra padrão de POT do audit fica desligada em todas as categorias acima.

**Compressão.** Com arte a 4x alinhada à origem da textura, cada bloco 4 x 4 tem uma cor só e a perda simulada é mínima (erro máximo de 3 em 255). Múltiplo de 4 não basta: com a grade deslocada em 2 texels o erro chega a 125. A simulação é uma aproximação de BC1, não o codificador da Unity, e o formato real em Standalone é `NOT SOURCEABLE` nos documentos do motor. Validar um Ciclope na Unity antes de mudar os 44 sem compressão. `Loading BG` é pintura e fica de fora. `Cyclops Smash.png`, por exemplo, ocupa 18,9 MB em memória sem compressão, assumindo RGBA32.

**Polígonos, LOD e material.** Orçamentos de polígono e níveis de LOD **não se aplicam**: o jogo é 2D, com sprites. Slots de material não têm limite declarado. O limite superior sem compressão (largura x altura x 4) é de 413,9 MiB nos 297 PNGs, 313,6 MiB sem o Runner e 120,2 MiB nas pastas `Resources`; os 44 sem compressão somam 61,6 MiB. É soma de arquivos, não pico em jogo: o pico fica `NOT ASSESSED` até medir no Memory Profiler. Mipmap, crunch e readable estão desligados nos 297, e não há `.spriteatlas`. WebGL é `NOT ASSESSED`, porque a Steam é o alvo e este orçamento não trata formato nem pico da Web.

**Orphaned** é `NOT ASSESSED` em Unity, como o audit já diz. `Player alt`, `Player_Teste`, `Tileset 1`, `Brick 200x200` e os duplicados do Runner devem ser revistos à mão.

### Câmera e escala de pixel: conflito aberto

**Ideal de arte:** escala inteira, em que cada pixel de arte tem a mesma largura na tela. **Restrição:** a câmera define o que o jogador vê, e mudá-la muda o jogo e o balanceamento. Nenhuma cena ou prefab usa Pixel Perfect Camera, e 1080p dá escala não inteira nos dois modos. Na Campanha o tamanho vem da lente Cinemachine, e o Pixel Perfect pede `CinemachinePixelPerfect`. Na Arena o tamanho está na Camera e alimenta `ArenaBounds`, lido em 23 arquivos: largura que varia com a resolução muda spawn e wrap, e isso afeta o ranking.

| Opção | Arena | Campanha | Ganha | Perde |
|---|---|---|---|---|
| Como está | Tamanho 8; 400 px de arte; 2,7x em 1080p | Tamanho 5; 250 px; 4,32x | Nada muda | Pixels desiguais; o aro de 1 pixel varia |
| Câmera ajustada | Tamanho 7,2; 360 px; 3x | Tamanho 5,4; 270 px; 4x | Pixel exato em 1080p | Arena vê 10% menos de altura; campanha, 8% mais; rebalancear |
| URP `PixelPerfectCamera`, Assets PPU 25 | Sem Crop, o tamanho muda com a resolução. Com Crop X e Y, fica fixo (referência 360 = 7,2), com barras fora dos múltiplos | Igual, referência 270 = 5,4 | Pixel exato | Campo de visão fixo ou tela cheia; luzes, UI e pós não verificados. O 2D Renderer lê o componente de URP, não o de `com.unity.2d.pixel-perfect` que está no manifesto |

Outras telas contam: 400 de altura dá 2x exato em 800p, 360 dá 4x em 1440p, e 270 não é inteiro em 1440p. **Este documento não resolve o conflito.** A arte nova a 4x serve às três opções. O José pediu teste com foto antes de aprovar (2026-11-05).

### Critério de aceitação de arte nova

Uma peça só entra no jogo se cumprir todos os itens. As folhas v2 já faziam parte do 2 e do 3; o Arauto e o Boitatá mostram por que o resto é necessário.

1. **Técnica:** grade a 4x, nome na regex, categoria dentro do orçamento e importação conforme a tabela acima.
2. **Escala:** cabe na faixa da seção 5, com a medida registrada, e aparece em folha de mesmo chão com Freny, Golem e Ciclope.
3. **Leitura:** se lê em 1x, sem zoom, e como silhueta de cor única, com um único traço que não repita outro arquétipo.
4. **Fundo real:** testada sobre `#080810`, `#201830` e o céu do estado em que aparece, nunca só em placa de teste clara.
5. **Recorte:** inimigo cumpre borda e ponto focal, com a porcentagem medida no relatório.
6. **Semântica:** cada cor saturada corresponde à tabela da seção 4, e a peça diz qual é seu reforço para daltonismo.
7. **Direção:** quem olha sem legenda diria Dark Fantasy. Se a resposta for robô, bicho de desenho ou fliperama, volta.
8. **Na Unity:** uma captura real, com luz e, para inimigo, com o preparo de ataque legível, acompanha a peça.

## 9. Reference Direction

Hollow Knight e Resident Evil são a direção do produto. As outras três são propostas da direção de arte, cada uma para um trabalho diferente.

**Hollow Knight (obrigatória).** Tirar: a separação de planos com valores distintos, que mantém a clareza no escuro; a identidade de cor por área; o mundo que se explica sem texto. Evitar: o traço à mão, os insetos fofos e, principalmente, a imagem de máscara branca sobre corpo escuro. O elmo de Freny é osso quente, de viseira em cruz, sobre corpo humano; se a cabeça dele lembrar o Cavaleiro, algo saiu errado.

**Resident Evil (obrigatória).** Tirar: a sala segura como evento de luz e som, a única temperatura diferente num lugar hostil; o item que se destaca porque importa; a economia visual de quem conta balas; a clareza de pontuação de Mercenários. Evitar: horror realista, biotecnologia, armas e lugares modernos, e o tom de filme de zumbi.

**Mike Mignola, quadrinhos de Hellboy (proposta).** Tirar: o preto como forma, com massas enormes, silhuetas simples e um único ponto de cor. É o melhor modelo para os inimigos navy: corpo quase sem detalhe, e um olho ou aro que diz tudo. Evitar: humor pulp, citações diretas de personagens e o vermelho como cor do protagonista.

**Devil May Cry, medidor de estilo (proposta).** Tirar: a escada em que cada degrau é maior, mais colorido e tem uma palavra mais forte. É o modelo direto da escada de hype e dos banners de `MultiKill`. Evitar: o tom de rock e pose urbana, letras cromadas e a ostentação do herói. Aqui o fundo escurece em vez de brilhar.

**Blasphemous (proposta).** Tirar: a prova de que pixel art sustenta Dark Fantasy opressivo, e o peso das animações de morte e de chefe. Evitar: a iconografia católica espanhola, a violência ritual como tema central e o detalhamento denso. Nossa arte, a 25 pixels por unidade e em cor chapada, é mais simples, e deve continuar assim.

## Decisões do José

Respostas de 2026-11-05. **O foco agora é só a arena.** "Testar" quer dizer: tirar foto na Unity, e o José aprova antes de entrar no jogo. Ele pode mudar qualquer item pelo número.

| # | Assunto | Decisão | Próximo passo |
|---|---|---|---|
| 1 | Dia azul da arena | Seguir a recomendação: ciclo fica, dia de cinza no lugar do azul | Foto enviada; aguarda aprovação |
| 2 | Menus em roxo-magenta | **Fica.** O José gosta e vê Dark Fantasy nele; não tirar de uma vez | Nenhum |
| 3 | Fonte | Testar outra. Candidata: Jersey 10 no texto, Jacquard 12 nos títulos de página (ambas OFL, com todos os acentos do pt-BR). Pixelify Sans reprovada: o 5 lê como S. Jacquard no banner reprovada: "PENTA" fica ilegível | Foto enviada; aguarda aprovação |
| 4 | Câmera e escala | Testar. Em 7,2 o pixel fica uniforme, mas corta o alto e as laterais das torres e mexe no balanceamento e no ranking | Foto enviada; aguarda aprovação |
| 5 | Menu player | Não mexer | Nenhum |
| 6 | Aro dos inimigos | Testado. Nova recomendação: não aplicar na arena; reavaliar na campanha | Foto enviada; aguarda aprovação |
| 7 | Folclore brasileiro | Fica para depois. Os desenhos atuais estão feios; redesenhar, provavelmente com artista contratado | Depois da arena |
| 8 | Sino do Arauto | Sem preocupação agora | Depois da arena |
| 9 | Máscara do Arauto | Sem preocupação agora | Depois da arena |
| 10 | Mercador em vermelho | Aprovado: cabelo fica; roupa muda só em arte futura | Nenhum |
| 11 | Cosméticos que imitam inimigo | Cosméticos ainda não entram; virão separados e aos poucos | Depois da arena |
| 12 | Runner | Fica fora por enquanto | Nenhum |
| 13 | Foco de menu | Seguir a recomendação, sem trocar a paleta: moldura e barra em magenta | Quando o menu for mexido |
| 14 | Vermelho no pico do combo | Aprovado: pode ficar vermelho no HUD | Nenhum |
| 15 | Grade da fonte e pop de combate | **Em aberto:** o José pediu explicação. Recomendação: número fica no tamanho nítido em repouso, e só cresce fora dele durante o pulo curto de um golpe | Mostrar no teste de fonte |
| 16 | Fontes por papel | Separar as fontes e testar. Placar e banner seguem em Press Start 2P | Foto enviada; aguarda aprovação |
| 17 | Efeito VHS nas fases | Testar quando chegar a campanha | Depois da arena |
| 18 | Nome de sprite e código | Aprovado: manter `Tileset_N` por ora | Nenhum |

## Fora deste documento

- **Visto na Unity em 2026-11-05, só na arena:** dia e noite, aro, câmera e fontes, em fotos com inimigos parados. Fora disso, nenhuma regra de luz, clima, Lua de Sangue ou hype foi vista em jogo. Luzes 2D mudam as cores e as razões de contraste, e isso não foi medido.
- **As cores são dos arquivos.** Os hex medidos estão arredondados a múltiplos de 8, o verde ácido é aproximado, e os de `MenuKit` e de combate foram convertidos dos valores do código.
- **Contrastes:** as 42 razões principais, as 7 violações de orçamento e a regex foram conferidas por script em 2026-11-04. As razões de UX vieram da revisão, também por script. Tudo é cor de arquivo.
- **A regra de 40%, a borda e o ponto focal não foram rodados** em nenhum sprite. O número é uma proposta.
- **Vistos:** folha de contato, Freny, céu, Menu player, `fundo.png`, `Tileset.png`, Mina-aranha, Tentáculo e as folhas v2, a geral em tamanho reduzido. **Não vistos:** céus de dia, Lua de Sangue, a maior parte da UI, retratos, cenas montadas.
- **Escala:** os números vêm de `conceitos_v2.md`, sem nova medição. Os demais inimigos não foram medidos, e o Tentáculo é estimativa visual.
- **`.meta`:** o pivô foi lido só em `Player idle`, `Menu player`, `Cyclops Smash` e `Monster attack`. O tamanho máximo foi medido nos 297.
- **Navegação:** foco por controle, toque e nitidez das fontes não foram testados. Em cenas há 106 cores parecidas com roxo, ainda sem classificar.
- **Código:** `CombatHud` não foi lido por inteiro, então cores e limiares atuais do combo não entram aqui. O Licker foi procurado só em `ArenaDirector.cs`. A carga de skins por nome é inferência do inventário.
- **Divergência:** o brief cita filtro não Point em imagens de menu, mas o inventário só mostra `Scanlines`, `Monitor` e `Player jump kick` fora do Runner. Usei o inventário.
- **Fontes do texto:** o game brief está no PR #193, e ainda não em `dev`. As fontes candidatas não tiveram licença ou acentos verificados, e as referências da seção 9 vêm de memória.
- **Papéis:** o kit só traz nove agentes, então a direção de arte foi feita pelo papel `creative-director`, UX por `ui-programmer` e técnica por `unity-specialist`, como manda `CLAUDE.md`.

## Para o mapa de sistemas

Estes itens criam ou tocam sistemas e devem aparecer em `/map-systems`. Cada um depende de uma decisão acima ou de teste na Unity. A ordem de trabalho é arena primeiro; iluminação de campanha, interativos do cenário e cosméticos vêm depois.

| Sistema | O que este documento pede | Seção |
|---|---|---|
| Estados visuais de jogo | Exploração, área segura, combate, chefe, arena, hype, Lua de Sangue, morte, vitória, menus, cada um com clima próprio | 2 |
| Retorno de combate | Escada de hype; fica fora da pontuação, pela regra do projeto | 2, 7 |
| Dia e noite da arena | Céu do dia e torchas; puramente visual | 2 |
| Iluminação de campanha | Uma fonte por trecho; calor significa segurança | 2, 6 |
| Leitura de inimigos | Aro assado, olhos, teto de contraste do tile | 4, 5 |
| Interativos do cenário | Brilho ou borda no que se usa; vasos e barris | 6 |
| Interface | Foco de menu, estados de botão, fontes por papel, escala do Canvas; paleta roxa fica | 4, 7 |
| Acessibilidade | Reduzir tremor e flash, tamanho de texto, modo daltônico, teclas por dispositivo | 2, 4, 7 |
| Câmera | Tamanho e pixel exato; `ArenaBounds` e ranking dependem disso | 8 |
| Cosméticos | Limites de cabeça para Freny não virar inimigo | 5 |
| Produção de arte | Critério de aceitação e geradores em `Tools/ArtGen` | 8 |
