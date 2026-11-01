using UnityEngine;
using UnityEngine.UI;

// The Guia page (main menu and pause menu): what every weapon, item, buff and shop upgrade does
// and where to get it, one topic at a time. The Ajuda page covers the buttons; this covers the
// rules. Numbers that live in code as public constants are read from there; the rest copy
// Player, ItemPickup, ArenaChest and ArenaDirector, so update both when tuning those.
public static class GuideScreen {

    private const float IconBox = 34f;
    private static readonly string[] Topics = { "Armas", "Fúria e defesa", "Itens", "Onde achar", "Armeiro", "Inimigos", "Arena" };
    private static int lastTopic;

    public static MenuPage Guide(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Guia", 900f, new Vector2(0.5f, 0.5f), 4f);

        RectTransform entries = MenuKit.MakeRect("Entries", page.Content);
        VerticalLayoutGroup column = entries.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = 4f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;

        page.Choice("Assunto", Topics, lastTopic, i => {
            lastTopic = i;
            Fill(entries, i);
        });
        // the picker is created after the entries' container but goes above it
        entries.SetAsLastSibling();
        Fill(entries, lastTopic);

        page.Space(6f);
        page.Button("Voltar", stack.Back);
        return page;
    }

    private static void Fill(RectTransform entries, int topic) {
        for (int i = entries.childCount - 1; i >= 0; i--) {
            Object.Destroy(entries.GetChild(i).gameObject);
        }
        MenuArt art = MenuArt.Get;
        switch (topic) {
            case 0: Weapons(entries, art); break;
            case 1: Defense(entries, art); break;
            case 2: Items(entries, art); break;
            case 3: Drops(entries, art); break;
            case 4: Shop(entries, art); break;
            case 5: Enemies(entries, art); break;
            default: Arena(entries, art); break;
        }
    }

    private static void Weapons(Transform parent, MenuArt art) {
        Entry(parent, art ? art.sword : null, "Espada",
            "Combo de 3 golpes: aperte de novo logo depois de cada um (até 0,4 s). Cada golpe tira " +
            "1 de dano e empurra o inimigo; o 3º empurra 3x mais, com corte e alcance 35% maiores. " +
            "Os golpes 1 e 2 recarregam em 0,25 s, o 3º em 0,65 s. Funciona no ar e agachado.");
        Entry(parent, art ? art.greatSword : null, "Espadão",
            "Corte e alcance 1,8x maiores (mesmo dano): pega mais inimigos de uma vez. " +
            "Do chão vale 6 golpes (2 combos); comprado no Armeiro, é para sempre.");
        Entry(parent, art ? art.revolver : null, "Revólver",
            "4 tiros por tambor; vazio, recarrega sozinho em 1,6 s com a reserva de balas " +
            "(começa com 8, máximo 16). Sem reserva, fica sem tiro. Cima atira para cima. Não empurra o inimigo.");
        Entry(parent, art ? art.shotgun : null, "Escopeta",
            "3 balas em leque, 1 cartucho por tiro; cada bala empurra o inimigo. Troque com o botão Escopeta. Vem de baús " +
            "da Arena ou do Armeiro (6 cartuchos por fase). Até 30 cartuchos.");
        Entry(parent, art ? art.grenade : null, "Granada",
            "Segure para carregar a força, solte para lançar em arco; sem espera entre uma e outra. " +
            "Explode em 1 s, ao tocar um inimigo ou ao cair no chão: 3 de dano em todos num raio " +
            "grande (mata quase todos; o Golem Vermelho aguenta uma) e quebra baús e vasos. Cuidado: perto demais, o Freny " +
            "também leva 2 de dano e é arremessado. Até 6.");
    }

    private static void Defense(Transform parent, MenuArt art) {
        Entry(parent, art ? art.heart : null, "Vida",
            "4 corações. Golpe comum tira 1; Golem e a bola do Feiticeiro tiram 2. Depois de " +
            "apanhar ele pisca 1,2 s sem levar dano. Queda alta também tira vida.");
        Entry(parent, art ? art.rageHelmet : null, "Fúria",
            "A barra Fúria enche lutando: matar, acertar a espada e apanhar. Parada 2 s, ela " +
            "esvazia. Cheia, dá 20 s de FÚRIA: a espada tira 2 de dano.");
        Entry(parent, art ? art.berserkerHelmet : null, "Armadura (Capacete Berserker)",
            Player.ArmorMax + " corações cinza que levam os golpes antes da vida (queda não). " +
            "Cai de inimigos e vasos; comprado no Armeiro, cada fase começa com ela cheia.");
        Entry(parent, art ? art.armorHeart : null, "Itens que sobram",
            "Com vida, balas ou armadura cheias, o item fica no chão até você precisar. " +
            "Itens duram 25 s e piscam antes de sumir.");
    }

    private static void Items(Transform parent, MenuArt art) {
        Entry(parent, art ? art.potion : null, "Poção",
            "Do chão: cura 1 coração na hora (com vida cheia não pega). Do Mercador " +
            "(150 moedas): fica guardada, beba com o botão Poção.");
        Entry(parent, art ? art.revolver : null, "Balas",
            "+4 na reserva do revólver. Só caem quando a reserva não está cheia.");
        Entry(parent, art ? art.shotgun : null, "Cartuchos",
            "+3 para a escopeta. Só caem se você já tem a escopeta.");
        Entry(parent, art ? art.diamond : null, "Diamante",
            "Vale " + ItemPickup.DiamondValue + " moedas.");
        Entry(parent, art ? art.coin : null, "Moedas",
            "Caem de todo inimigo e ficam salvas. Compram melhorias no Armeiro.");
    }

    private static void Drops(Transform parent, MenuArt art) {
        Entry(parent, art ? art.coin : null, "Drops conforme a necessidade",
            "Os drops seguem o que falta ao Freny: com vida cheia não cai poção, com granadas cheias " +
            "não cai granada, sem escopeta ela pode cair. Quanto mais faltar, mais cai (até o dobro). " +
            "O mesmo item já no chão deixa outro bem mais raro, e com muitos itens no chão caem menos.");
        Entry(parent, art ? art.coin : null, "Inimigos (Arena)",
            "Além da moeda, com metade faltando: poção 15%, balas 12%, cartuchos 10%, granada 6%, " +
            "escopeta 3% (sem escopeta), espadão 4%, armadura 4%, diamante 4%. Na campanha só deixam moedas.");
        Entry(parent, art ? art.grenade : null, "Baú (Arena)",
            "Surge a cada 30 s e some em 20 s; quebra com golpe ou tiro. Dá 2 granadas, a escopeta com 6 " +
            "cartuchos ou 10-25 moedas (às vezes com diamante); granadas e escopeta conforme a necessidade.");
        Entry(parent, art ? art.diamond : null, "Vasos, caixas e barris",
            "Quebram com golpe, tiro ou granada. Na Arena surge um vaso novo a cada 12 s (até 3), e os " +
            "do cenário voltam 90 s depois de quebrados. Pode ter diamante 12%, poção 18%, balas 10%, " +
            "granada 8% ou armadura 3%.");
    }

    private static void Shop(Transform parent, MenuArt art) {
        Entry(parent, art ? art.jump : null, "Pulo duplo · " + FrenyProgress.Cost(FrenyUpgrade.DoubleJump),
            "Um segundo pulo no ar.");
        Entry(parent, art ? art.grenade : null, "Granada · " + FrenyProgress.Cost(FrenyUpgrade.Grenade),
            "Começa cada fase com 2 granadas.");
        Entry(parent, art ? art.greatSword : null, "Espadão · " + FrenyProgress.Cost(FrenyUpgrade.GreatSword),
            "A espada fica 1,8x maior para sempre.");
        Entry(parent, art ? art.shotgun : null, "Escopeta · " + FrenyProgress.Cost(FrenyUpgrade.Shotgun),
            "Começa cada fase com a escopeta e 6 cartuchos.");
        Entry(parent, art ? art.berserkerHelmet : null, "Capacete Berserker · " + Loadout.HelmetCost,
            "Começa cada fase com " + Player.ArmorMax + " de armadura.");
        Entry(parent, art ? art.heart : null, "Vida, Velocidade e Recarga",
            "+1 coração, +8% de velocidade, -10% de recarga por nível. Custam 100, +50 a cada " +
            "nível. O Inventário deixa armas compradas em casa.");
    }

    // the tough ones; the Arena brings them in as the run heats up (ArenaDirector's list in the scene)
    private static void Enemies(Transform parent, MenuArt art) {
        Entry(parent, null, "Golem",
            "Lento e teimoso: depois que vê o Freny, não larga. O soco tira 2 corações e derruba. " +
            "2 de vida.");
        Entry(parent, null, "Golem Vermelho",
            "Pedra vermelha e olhos acesos. Um tanque: 8 de vida (aguenta duas granadas), quase não " +
            "recua com os golpes, e o soco tira 2 e derruba: ele carrega o soco e avança num dash junto. De longe, joga uma mina que gruda no chão, " +
            "pisca e explode como uma granada 2 s depois de jogada (1,5 s quando a Arena fica difícil); encostar nela não detona. " +
            "Tire o Freny de perto, ou acerte a mina antes. " +
            "No máximo 3 de uma vez; quando um joga a mina, nenhum joga outra por 3 s. " +
            "Vale 500 pontos e 6 moedas. Na Arena, só depois de 50 mil pontos.");
        Entry(parent, null, "Escudeiro",
            "Anda atrás de um escudo de espinhos virado para o Freny. No escudo o dano é pequeno (12 espadadas " +
            "ou 36 tiros); nas costas, 2 espadadas ou 6 tiros matam. Tudo que vem da frente bate no escudo, até de cima. " +
            "Ele vira rápido (0,4 s, até 0,15 s no difícil) e se afasta se o Freny chega por trás: pule por cima e bata logo. " +
            "6 de vida, 300 pontos.");
        Entry(parent, null, "Ciclope",
            "O chefe gigante: prefere ficar no meio, embaixo da primeira plataforma (bom para pular no olho). Chega no centro do chão aos 50 mil pontos e cada próximo 150 mil pontos depois de o anterior morrer; " +
            "dois de uma vez só a partir de 1 milhão, com as barras embaixo, uma de cada lado. Cada luta traz mais " +
            "ajuda: 2 feiticeiros comuns na primeira, um a mais em cada uma seguinte (até 5), e a partir da segunda " +
            "os outros inimigos também continuam vindo. Sempre vem um morcego e um inseto atrás do Freny, para manter o combo vivo. O feitiço dos feiticeiros não fere o Ciclope. 100 de " +
            "vida e barra de chefe. Não pula: fica no chão. Soco: 1 coração e arremessa o Freny longe. " +
            "Pancada no chão: uma onda pelo piso, 2 corações e o mesmo arremesso (pule!). Bomba: de longe, uma mina " +
            "3 vezes maior jogada com força, 2 corações. Vômito: ele vomita no chão e alaga o andar de baixo inteiro por 4 s; " +
            "quem estiver no chão queima (os outros inimigos também, menos ele), então suba numa plataforma. " +
            "Na Lua de Sangue ele solta até 3 raios vermelhos do olho em direção ao Freny (3 corações), que desintegram tudo no caminho. " +
            "Na pancada ele dá um pulinho e a tela treme. Depois de cada golpe ele descansa " +
            "3 s: é a hora de bater. Acertar o olho tira o dobro. Vale 5 000 pontos, 30 moedas e deixa um baú.");
        Entry(parent, null, "Feiticeiro",
            "Fica parado e lança uma bola a cada 5 segundos (o Supremo a cada 3,5); ela tira 2 e empurra, e abaixar desvia " +
            "de um tiro reto. " +
            "Nas torres, muda de lugar a cada 3 feitiços e some depois de 8. 2 de vida. Na Arena, " +
            "contando os dois tipos, há no máximo 1 feiticeiro até 50 mil pontos, 2 depois disso e " +
            "3 só depois de 1 milhão; quando um morre, outro só aparece no lugar dele 15 s depois. " +
            "De dia, as bolas " +
            "ganham uma borda escura para aparecer no céu claro.");
        Entry(parent, null, "Feiticeiro Supremo",
            "Olho e manto vermelhos. Mira onde o Freny vai estar, não onde ele está: mude de " +
            "direção. Na Arena, só ele tem bolas que seguem o Freny por 2,5 s (fazem curva, mas pular ou " +
            "abaixar na hora certa ainda desvia). 3 de vida; na Arena só aparece depois de 100 mil pontos.");
    }

    private static void Arena(Transform parent, MenuArt art) {
        Sprite face = art != null && art.frenyFace != null && art.frenyFace.Length > 0 ? art.frenyFace[0] : null;
        Entry(parent, face, "Automático ou Manual",
            "No automático o piloto joga por você e dá para assumir. Só corrida toda manual " +
            "vale para o Ranking. Com o piloto ligado, aparece um A grande ao lado da cabeça do Freny e uma moldura roxa em volta da tela. " +
            "Um recorde novo aparece no fim da corrida, com o botão Salvar recorde ou o motivo de não poder ir para o Ranking. " +
            "No automático, a próxima corrida começa 10 s depois (ou na hora, em Jogar agora); com 10 mil " +
            "pontos ou mais, o jogo pergunta antes.");
        Entry(parent, null, "Lua de Sangue",
            "Dia de 1 minuto, noite de 4. A cada 3 noites a lua fica vermelha por 30 s no meio da noite (a primeira uns 10 minutos de corrida). Nesse tempo, " +
            "os inimigos ficam 25% mais rápidos, tiram 1 coração a mais, recuperam vida (1 a cada 3 s, o Ciclope também) " +
            "e ficam de olhos vermelhos, deixando um rastro vermelho.");
        Entry(parent, art ? art.rageHelmet : null, "Dificuldade",
            "Fácil, Médio, Difícil ou Freny: quanto maior, mais inimigos, mais rápidos e mais cedo. " +
            "Com o tempo e os pontos a Arena aperta, mas para de apertar no primeiro Ciclope (50 mil) e só volta em 1 milhão.");
        Entry(parent, Achievements.Icon(Achievement.Cyclops), "Conquistas",
            "10 desafios (pausa > Conquistas). Só valem corridas manuais: depois que o piloto joga, a corrida não " +
            "dá mais conquistas. No fim da corrida, o servidor confere cada conquista nova e o site mostra quantos % " +
            "dos jogadores têm cada uma.");
        Entry(parent, art ? art.run : null, "XP e piloto automático",
            "Matar dá XP. Cada nível vale 1 ponto, que compra melhorias do piloto no Armeiro: " +
            "reação, esquiva com dash, poção sozinho e melhor alvo.");
    }

    // icon | name over what it does
    private static void Entry(Transform parent, Sprite icon, string name, string what) {
        Image row = MenuKit.MakeImage("Entry " + name, parent, new Color(1f, 1f, 1f, 0.05f));
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Image picture = MenuKit.MakeImage("Icon", row.transform, Color.white);
        picture.sprite = MenuArt.Trim(icon);
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        picture.enabled = icon != null;
        MenuKit.Size(picture, IconBox, IconBox);

        Text text = MenuKit.MakeText(row.transform, "", MenuKit.SmallTextSize, MenuKit.DimTextColor, TextAnchor.UpperLeft);
        text.supportRichText = true;
        text.text = "<color=#" + ColorUtility.ToHtmlStringRGB(MenuKit.TitleColor) + ">" + name + "</color>\n" + what;
        MenuKit.Size(text, -1f, -1f).flexibleWidth = 1f;
    }
}
