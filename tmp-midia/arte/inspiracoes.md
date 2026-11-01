# Inspirações de arte: The Rise of Freny

Referências para a próxima leva de arte (cosméticos, armas e inimigos). A regra é **tirar princípios, não copiar desenho**: nada de silhueta, personagem ou paleta pega de outro jogo. O que importa é *por que* aquilo funciona numa tela pequena e escura.

## O que o jogo já é (ponto de partida)

Antes de olhar para fora, o que os sprites atuais fazem (análise completa em `Tools/ArtGen/freny_palette.py`):

- **Grade**: 1 pixel de arte = 4 texels, 100 PPU (25 pixels de arte por unidade). O Freny tem uns 14x32 pixels de arte, o Golem 22x31, o Morcego 19x18.
- **Paleta curtíssima**: cada sprite usa 4 a 7 cores. Nos sprites de jogo aparecem 146 cores, mas a arte é feita com umas 60 (rampas em `freny_palette.py`, swatch em `docs/arte/paleta.png`, `.gpl` para Aseprite/GIMP).
- **Sem contorno preto**: cada forma fecha com a cor mais escura da própria rampa, só no lado da sombra (baixo/direita). É um *selective outline*.
- **Código de cor**: inimigo é **frio** (azul-marinho + brilho violeta nos olhos e pontos fracos); o Freny e o que ele pega são **quentes** (ameixa, areia, pele laranja, ouro, madeira). Vermelho é sangue e fúria, verde-ácido é o Poço de Ácido.

## Jogos de referência

### Blasphemous (The Game Kitchen)
- Links: [Hollywood Reporter: a abordagem "purista" do pixel art](https://www.hollywoodreporter.com/heat-vision/blasphemous-designer-purist-approach-games-pixel-art-style-1238370), [PC Zone: entrevista com o diretor de arte de Blasphemous 2](https://pczone.co.uk/?p=12693)
- **O que tirar**: o compromisso com o pixel do começo ao fim (efeitos, UI e cenário na mesma grade). O tema também ajuda: iconografia religiosa e mórbida (cultistas, mártires, máscaras, relicários). Daí vêm o **Cultista voador**, o **Arauto do Poço** e o **Amuleto do olho**.
- **O que evitar**: a escala de detalhe. Os sprites deles são muito maiores que os nossos 14x32.

### Dead Cells (Motion Twin)
- Links: [80.lv: entrevista com os desenvolvedores](https://80.lv/articles/interview-with-the-developers-of-dead-cells), [80.lv: pipeline dos personagens](https://80.lv/articles/case-study-dead-cells-character-art-pipeline/)
- **O que tirar**: o arsenal grande é viável porque cada arma tem **uma silhueta e um movimento próprios** (lança fura em linha, mangual faz arco e impacto, chicote alcança longe e fino). Os efeitos de golpe são poucos quadros, de alto contraste e curtos. Daí vêm a **estocada**, a **chicotada** e o **impacto**.
- Também: renderizar a arte por ferramenta (eles usam 3D, nós usamos Python) garante consistência e permite refazer tudo quando o corpo muda. É o mesmo raciocínio do `freny_cosmetics.py`.

### Shovel Knight (Yacht Club Games)
- Links: [Breaking the NES (blog da Yacht Club)](https://www.yachtclubgames.com/blog/breaking-the-nes/), [reedição na Game Developer](https://www.gamedeveloper.com/design/breaking-the-nes-for-shovel-knight)
- **O que tirar**: a paleta limitada como **regra de base**, decidida cedo, e "cores de trapaça" só quando nenhuma serve. Nós fizemos igual: tudo sai de `freny_palette.py`, e o gerador **recusa** qualquer cor fora dela (`check_palette`). Também: cosméticos que mudam a silhueta (chifres, crista, capa) e não só a cor.

### Hyper Light Drifter (Heart Machine)
- Links: [Mechanics of Magic: design visual](https://mechanicsofmagic.com/2022/04/16/visual-design-of-games-16/), [PlayStation Blog: criando o mundo](https://blog.playstation.com/2014/03/14/creating-the-world-of-hyper-light-drifter-on-ps4-and-ps-vita/)
- **O que tirar**: separar jogador e inimigos por **família de cor**, para ler a briga num relance. Segundo a análise, o herói e os aliados são avermelhados e os inimigos verde/marrom; é o nosso quente contra frio. Além disso, cada ambiente tem uma paleta marcante: o Poço de Ácido é o único lugar com verde, então inimigo verde = "é do Poço".

### Salt and Sanctuary (Ska Studios)
- Link: [análise na Push Square](https://www.pushsquare.com/reviews/ps4/salt_and_sanctuary)
- **O que tirar**: o bestiário *souls-like* em 2D lateral. Inimigos lentos e blindados com uma janela de abertura clara, e chefes em fases. Daí vêm o **Carniçal blindado** e as duas fases do **Arauto**.

### Castlevania (Konami)
- Links: [Bone Archer (wiki)](https://castlevania.fandom.com/wiki/Bone_Archer), [Hardcore Gaming 101: os inimigos](https://www.hardcoregaming101.net/cvbackup/enemy.htm)
- **O que tirar**: inimigos com **um padrão só, muito legível**: o esqueleto que atira de longe, a cabeça que voa em onda, a gárgula que mergulha. Cada inimigo novo nosso tem uma frase de comportamento e um quadro de ataque inconfundível.

### Moonlighter (Digital Sun)
- Link: [análise na KeenGamer](https://www.keengamer.com/articles/reviews/moonlighter-review-pc/)
- **O que tirar**: os inimigos de masmorra simples e "fofos-sinistros" (lodo, golem) que viram loot e ficam na memória. O **Lodo ácido** que se divide segue essa linha: forma redonda, um olho, leitura instantânea.

### Katana ZERO (Askiisoft)
- **O que tirar**: o *telegraph* (aviso antes do golpe). O inimigo muda de pose ou de brilho um instante antes de atacar, e o jogador aprende a reagir. Não achamos uma fonte primária sobre como eles fazem isso, então aqui vale o princípio geral. Nos conceitos, todo quadro de ataque **acende** algo: violeta vira lilás, a boca abre em vermelho, a flecha brilha.

### Dungeon crawlers e Eldritch
- Temas: ratos da peste, lodo, gárgulas de pedra, cultistas encapuzados, horror cósmico (o olho violeta). Encaixa no Ciclope e no Tentáculo que já existem.

## Referências de técnica de pixel art

- [Slynyrd: Pixelblog 1, paletas de cor](https://www.slynyrd.com/blog/2018/1/10/pixelblog-1-color-palettes): rampas com *hue shift*, sombra mais fria e luz mais quente. As rampas do jogo já fazem isso (marinho, depois índigo, depois violeta).
- [Wayline: pixel art para jogos](https://www.wayline.io/learn/pixel-art/4): **silhueta primeiro**. A forma chapada já tem que dizer o que é; o detalhe vem por último.
- [Bugnet: pixel art que se lê](https://bugnet.io/blog/how-to-make-pixel-art-that-reads-clearly): em tamanho pequeno, pose e um ou dois adereços valem mais que detalhe. Se o inimigo tem a mesma cor do cenário, o jogador sente como injustiça, não como camuflagem. Por isso o brilho violeta fica sempre nos olhos.

## Princípios que os conceitos seguem

1. **Silhueta antes de tudo**: cada inimigo tem uma forma única no chão (esqueleto fino e alto, carniçal baixo e largo, lodo redondo, gárgula com asas em V, rato comprido).
2. **Paleta travada**: só cores de `freny_palette.py`, e de 4 a 7 por peça.
3. **Contorno seletivo**: a cor mais escura da rampa, só no lado da sombra.
4. **Código de ameaça**: violeta = olho/ponto fraco; lilás/branco = vai atacar; vermelho = boca, mordida, fúria; verde = ácido (dano contínuo, Poço).
5. **Quente para o Freny, frio para o inimigo**: cosméticos e armas ficam nas cores quentes. As exceções (Troféu do Ciclope, Capa do abismo) são **troféus** de inimigo, de propósito.
6. **Teste do 1x**: toda folha termina com uma faixa a 1 pixel de arte por pixel. Se não se lê ali, não se lê no jogo.
