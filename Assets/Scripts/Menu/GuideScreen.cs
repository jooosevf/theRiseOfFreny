using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The Guia page (main menu and pause menu): what every weapon, item, buff and shop upgrade does
// and where to get it, one topic at a time. The Ajuda page covers the buttons; this covers the
// rules. Numbers that live in code as public constants are read from there; the rest copy
// Player, ItemPickup, ArenaChest and ArenaDirector, so update both when tuning those.
public static class GuideScreen {

    private const float IconBox = 34f;
    private static readonly string[] Topics = { "Armas", "Fúria e defesa", "Itens", "Onde achar", "Armeiro", "Inimigos", "Arena",
                                                "Poço de Ácido", "Missões e títulos", "Bênçãos", "Mutadores", "Renascer e visual", "Conta e amigos" };
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

        List<int> shown = new List<int>();
        List<string> names = new List<string>();
        for (int i = 0; i < Topics.Length; i++) {
            if (TopicOn(i)) {
                shown.Add(i);
                names.Add(i == 1 && !Features.Fury ? "Defesa" : Topics[i]);
            }
        }
        lastTopic = shown[Mathf.Max(0, shown.IndexOf(lastTopic))];
        page.Choice("Assunto", names.ToArray(), shown.IndexOf(lastTopic), i => {
            lastTopic = shown[i];
            Fill(entries, lastTopic);
        });
        // the picker is created after the entries' container but goes above it
        entries.SetAsLastSibling();
        Fill(entries, lastTopic);

        page.Space(6f);
        page.Button("Voltar", stack.Back);
        return page;
    }

    // a mechanic switched off (Features) has no page here
    private static bool TopicOn(int topic) {
        switch (topic) {
            case 7: return Features.AcidPit;
            case 9: return Features.Blessings;
            case 10: return Features.WeeklyMutators;
            case 11: return Features.Rebirth || Features.Looks;
            default: return true;
        }
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
            case 6: Arena(entries, art); break;
            case 7: AcidPit(entries); break;
            case 8: Missions(entries, art); break;
            case 9: BlessingsTopic(entries); break;
            case 10: MutatorsTopic(entries); break;
            case 12: AccountTopic(entries, art); break;
            default: RebirthTopic(entries, art); break;
        }
    }

    private static void Weapons(Transform parent, MenuArt art) {
        Entry(parent, art ? art.sword : null, "Espada",
            "Combo de 3 golpes: aperte de novo logo depois de cada um (até 0,4 s). Cada golpe tira " +
            "1 de dano e empurra o inimigo; o 3º empurra 3x mais, com corte e alcance 35% maiores. " +
            "Os golpes 1 e 2 recarregam em 0,25 s, o 3º em 0,65 s. Funciona no ar e agachado.");
        Entry(parent, art ? art.greatSword : null, "Espadão",
            "Corte e alcance 1,8x maiores (mesmo dano): pega mais inimigos de uma vez. " +
            "Só cai do chão: vale 6 golpes (2 combos), mais 3 por nível de \"Espadão: +3 golpes\" no Armeiro.");
        Entry(parent, art ? art.revolver : null, "Revólver",
            "4 tiros por tambor; vazio, recarrega sozinho em 1,6 s com a reserva de balas " +
            "(começa com 8, máximo 16). Sem reserva, fica sem tiro. Cima atira para cima. Não empurra o inimigo.");
        Entry(parent, art ? art.shotgun : null, "Escopeta",
            "3 balas em leque, 1 cartucho por tiro; cada bala empurra o inimigo. Troque com o botão Escopeta. Vem de baús " +
            "e inimigos da Arena. Até 30 cartuchos (40 ou 50 com o Armeiro).");
        Entry(parent, art ? art.grenade : null, "Granada",
            "Aperte para lançar em arco, sempre à mesma distância (uns 6 m); sem espera entre uma e outra. " +
            (Features.GrenadeFuse ?
            "Ela quica, rola e só explode 5 s depois de puxar o pino, onde estiver (os segundos aparecem em cima dela). " +
            "Segurar o botão já puxa o pino: o pavio queima na mão e, ao soltar, ela vai com o tempo que sobrou; " +
            "segurou 5 s, explode na mão. " : "Ela explode onde bater. ") +
            "3 de dano em todos num raio " +
            "grande (mata quase todos; o Golem Vermelho aguenta uma) e quebra baús e vasos. Cuidado: perto demais, o Freny " +
            "também leva 2 de dano e é arremessado. Até 6 (8 ou 10 com o Armeiro).");
        if (Features.Net) Entry(parent, NetThrower.Icon, "Rede",
            "Comprada no Armeiro: " + NetThrower.NetsPerStage + " por fase (até " + NetThrower.MaxNets + "), botão Rede. Voa em arco e abre no " +
            "primeiro inimigo ou no chão: todos embaixo dela ficam presos " + NetTrap.Seconds + " s, parados e sem atacar (o morcego " +
            "fica pendurado no ar). O Ciclope é grande demais: só fica mais lento por " + NetTrap.CyclopsSeconds + " s. Não tira vida.");
        if (Features.MageStaff) Entry(parent, MageStaff.Icon, "Cajado do Mago",
            "Às vezes (" + Mathf.RoundToInt(MageStaff.DropChance * 100f) + "%) um feiticeiro morto na Arena deixa o cajado. Com ele, por " +
            MageStaff.Duration + " s, o botão Atirar lança bolas de feitiço sem gastar munição: " + MageStaff.Damage +
            " de dano, ferem os inimigos (até o Ciclope) e nunca o Freny. O cajado aparece nas costas dele e no HUD, " +
            "e pisca nos últimos segundos. Pegar outro recomeça o tempo.");
        if (Features.WeaponBuffs) Entry(parent, art ? art.revolver : null, "Melhorias do Armeiro",
            "O Armeiro não vende armas: vende melhorias para elas (dano, munição, recarga, crítico). Elas valem " +
            "para sempre e para a arma que cair do chão também (veja Armeiro).");
    }

    private static void Defense(Transform parent, MenuArt art) {
        Entry(parent, art ? art.heart : null, "Vida",
            "4 corações. Golpe comum tira 1; Golem e a bola do Feiticeiro tiram 2. Depois de " +
            "apanhar ele pisca 1,2 s sem levar dano. Queda alta também tira vida.");
        if (Features.Fury) Entry(parent, art ? art.rageHelmet : null, "Fúria",
            "A barra Fúria enche lutando: matar, acertar a espada e apanhar. Parada 2 s, ela " +
            "esvazia. Cheia, ela espera: aperte F (troque em Configurações > Controles) na hora certa para " +
            "20 s de FÚRIA (25 ou 30 com o Armeiro): a espada tira 2 de dano. O piloto automático ativa sozinho com um inimigo perto.");
        Entry(parent, art ? art.berserkerHelmet : null, "Armadura (Capacete Berserker)",
            Player.ArmorMax + " corações cinza que levam os golpes antes da vida (queda não). " +
            "Cai de inimigos e vasos.");
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
        if (Features.InstaKill) Entry(parent, ArenaPowerUps.Icon(ItemPickup.Kind.InstaKill), "Insta-kill",
            "Raro. Por " + ArenaPowerUps.InstaKillSeconds + " s todos os inimigos ficam com 1 coração, inclusive os que chegarem: " +
            "qualquer golpe mata, até no escudo do Escudeiro. Não pega no Ciclope, e não cai enquanto ele está vivo.");
        if (Features.Nuke) Entry(parent, ArenaPowerUps.Icon(ItemPickup.Kind.Nuke), "Nuke",
            "Raro. Mata na hora todos os inimigos da tela, menos o Ciclope. Dá moedas e, no máximo, " +
            ArenaPowerUps.NukeMaxPoints.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", ".") + " pontos. " +
            "Nada surge por " + ArenaPowerUps.NukeSpawnPause + " s, e os portais abertos fecham vazios.");
        if (Features.Magnet) Entry(parent, ArenaPowerUps.Icon(ItemPickup.Kind.Magnet), "Ímã",
            "Raro. Por " + ArenaPowerUps.MagnetSeconds + " s moedas, diamantes e os itens de que o Freny precisa voam até ele, de qualquer lugar da tela.");
        Entry(parent, art ? art.coin : null, "Moedas",
            "Caem de todo inimigo e ficam salvas. Compram melhorias no Armeiro (Jogar > Armeiro, ou direto da pausa no meio da partida).");
    }

    private static void Drops(Transform parent, MenuArt art) {
        Entry(parent, art ? art.coin : null, "Drops conforme a necessidade",
            "Os drops seguem o que falta ao Freny: com vida cheia não cai poção, com granadas cheias " +
            "não cai granada, sem escopeta ela pode cair. Quanto mais faltar, mais cai (até o dobro). " +
            "O mesmo item já no chão deixa outro bem mais raro, e com muitos itens no chão caem menos.");
        Entry(parent, art ? art.coin : null, "Inimigos (Arena)",
            "Além da moeda, com metade faltando: poção 15%, balas 12%, cartuchos 10%, granada 6%, " +
            "escopeta 3% (sem escopeta), espadão 4%, armadura 4%, diamante 4%. Na campanha só deixam moedas.");
        Entry(parent, art ? art.grenade : null, "Baú prata (Arena)",
            "Surge a cada 30 s e some em 20 s; quebra com golpe ou tiro. Dá 2 granadas, a escopeta com 6 " +
            "cartuchos ou 10-25 moedas (às vezes com diamante); granadas e escopeta conforme a necessidade.");
        Entry(parent, art ? art.diamond : null, "Vasos, caixas e barris",
            "Quebram com golpe, tiro ou granada. Na Arena surge um vaso novo a cada 12 s (até 3), e os " +
            "do cenário voltam 90 s depois de quebrados. Pode ter diamante 12%, poção 18%, balas 10%, " +
            "granada 8% ou armadura 3%.");
        if (Features.GoldenChest) Entry(parent, BossKey.Icon, "Chave do Ciclope",
            "O primeiro Ciclope da corrida sempre deixa a chave; os seguintes, " + Mathf.RoundToInt(ArenaBossLoot.KeyChance * 100f) + "% das vezes. " +
            "Ela brilha no chão e não some: encoste para pegar. Aparece ao lado das armas enquanto o Freny a leva. " +
            "Vale só nesta corrida: não vai para o Inventário.");
        if (Features.GoldenChest) Entry(parent, GoldenChest.Icon, "Baú dourado",
            "Surge por um portal dourado numa das plataformas mais altas quando a chave cai, e espera até ser aberto. " +
            "Só abre com a chave (encoste ou bata). Dá cerca de 145 moedas (moedas e 2 diamantes), enche a vida, a armadura, " +
            "as balas, os cartuchos e as granadas, " + (Features.Fury ? "solta a Fúria na hora " : "") + "e, se você não tem o Espadão, uns golpes com ele. Não dá pontos. " +
            "E tem " + Mathf.RoundToInt(FrenyLook.ChestLookChance * 100f) + "% de chance de trazer um visual que só ele dá " +
            "(Elmo do Ciclope ou Capa dourada), um que você ainda não tem." + (Features.Blessings ? " Ao abrir, escolha 1 de 3 bênçãos." : "") + " " +
            "Cada chave traz um baú novo, então dá para ter vários na mesma corrida.");
        if (Features.InstaKill || Features.Nuke || Features.Magnet) Entry(parent, ArenaPowerUps.Icon(ItemPickup.Kind.Nuke), "Insta-kill, Nuke e Ímã",
            "Inimigos que o Freny mata na Arena têm cerca de " + (ArenaPowerUps.DropChance * 100f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') +
            "% de chance de deixar um deles (o Ímã é o mais comum, o Nuke o mais raro). Só um por vez no chão; os mortos pelo Nuke não deixam nenhum.");
        if (Features.MageStaff) Entry(parent, MageStaff.Icon, "Feiticeiros (Arena)",
            "Cada feiticeiro que morre tem " + Mathf.RoundToInt(MageStaff.DropChance * 100f) + "% de chance de deixar o Cajado do Mago.");
    }

    // the Armeiro's buffs (2026-11-01), prices read from FrenyProgress
    private static void Shop(Transform parent, MenuArt art) {
        if (Features.WeaponBuffs) ShopBuffs(parent, art);
        if (Features.Looks) Entry(parent, art ? art.berserkerHelmet : null, "Visual",
            "Elmos (o Capacete Berserker custa 1.000 e só muda o visual: um chifre quebrado, inteiro quando ele tem armadura), " +
            "capas e cores ficam em Armeiro > Cosméticos ou Inventário > Visual. O Armeiro e o Inventário também abrem direto da pausa.");
        Entry(parent, art ? art.heart : null, "Freny",
            "Vida máxima +1 (" + Prices(FrenyUpgrade.MaxHealth) + "), velocidade +5% (" + Prices(FrenyUpgrade.MoveSpeed) +
            "), pulo duplo (" + Prices(FrenyUpgrade.DoubleJump) + ")" +
            (Features.Net ? ", Rede (" + Prices(FrenyUpgrade.Net) + ", " + NetThrower.NetsPerStage + " por fase)" : "") +
            ". Espadão, escopeta, granada e capacete só caem do chão.");
    }

    private static void ShopBuffs(Transform parent, MenuArt art) {
        Entry(parent, art ? art.revolver : null, "Pistola",
            "Dano +25% por nível (" + Prices(FrenyUpgrade.PistolDamage) + "), tambor 4→5→6 (" + Prices(FrenyUpgrade.PistolDrum) +
            "), reserva 16→24→32 (" + Prices(FrenyUpgrade.PistolReserve) + "), recarga 20% mais rápida por nível (" + Prices(FrenyUpgrade.PistolReload) + ").");
        Entry(parent, art ? art.shotgun : null, "Escopeta",
            "Dano +25% por nível (" + Prices(FrenyUpgrade.ShotgunDamage) + "), até 40→50 cartuchos (" + Prices(FrenyUpgrade.ShotgunShells) +
            "), leque de 5 balas (" + Prices(FrenyUpgrade.ShotgunSpread) + "). Valem para a escopeta que cair.");
        Entry(parent, art ? art.sword : null, "Espada",
            "Dano +25% por nível, espadão também (" + Prices(FrenyUpgrade.SwordDamage) + "), golpe 10% mais rápido por nível (" +
            Prices(FrenyUpgrade.AttackSpeed) + "), espadão do chão +3 golpes por nível (" + Prices(FrenyUpgrade.GreatSwordSwings) + ").");
        Entry(parent, art ? art.grenade : null, "Granada",
            "Explosão 30% maior (" + Prices(FrenyUpgrade.GrenadeBlast) + "), 4 de dano (" + Prices(FrenyUpgrade.GrenadePower) +
            "), carrega 8→10 (" + Prices(FrenyUpgrade.GrenadeCarry) + "). Cuidado: a explosão maior também pega o Freny de mais longe.");
        Entry(parent, art ? art.sword : null, "Crítico e Fúria",
            "Crítico: +5% de chance por nível de um golpe ou tiro tirar o dobro (" + Prices(FrenyUpgrade.Critical) + ")." +
            (Features.Fury ? " Fúria: +5 s por nível (" + Prices(FrenyUpgrade.RageTime) + ")." : ""));
    }

    // "1.500 / 3.000 / 6.000"
    private static string Prices(FrenyUpgrade upgrade) {
        List<string> parts = new List<string>();
        for (int level = 0; level < FrenyProgress.MaxLevel(upgrade); level++) {
            parts.Add(FrenyProgress.CostAt(upgrade, level).ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", "."));
        }
        return string.Join(" / ", parts);
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
        if (Features.SpiderMine) Entry(parent, SpiderMineIcon(), "Mina-aranha",
            "Uma mina com seis pernas: anda e pula entre as plataformas atrás do Freny, e a luz vermelha pisca e apita " +
            "mais rápido quanto mais perto ela chega. Encostada nele, para, arma por " + SpiderMine.ArmTime.ToString("0.0") + " s e explode: " +
            "2 corações, empurrão, e fere os outros inimigos em volta (outra mina-aranha explode junto). Morre com um golpe, " +
            "mas explode sempre que morre: de longe (tiro, granada) não pega o Freny; de espada, ele leva 1 coração. " +
            "Vale 250 pontos. Na Arena, a partir do meio da curva de dificuldade.");
        if (Features.Cyclops) Entry(parent, null, "Ciclope",
            "O chefe gigante: prefere ficar no meio, embaixo da primeira plataforma (bom para pular no olho). Chega no centro do chão aos 50 mil pontos e cada próximo 150 mil pontos depois de o anterior morrer; " +
            "dois de uma vez só a partir de 1 milhão, com as barras embaixo, uma de cada lado. " +
            (Features.BossEscalation ? "Cada luta traz mais " +
            "ajuda: 2 feiticeiros comuns na primeira, um a mais em cada uma seguinte (até 5), e a partir da segunda " +
            "os outros inimigos também continuam vindo. " : "Cada luta traz 2 feiticeiros comuns. ") + "Sempre vem um morcego e um inseto atrás do Freny, para manter o combo vivo. O feitiço dos feiticeiros não fere o Ciclope. 100 de " +
            "vida e barra de chefe. Não pula: fica no chão. Soco: 1 coração e arremessa o Freny longe. " +
            "Pancada no chão: uma onda pelo piso, 2 corações e o mesmo arremesso (pule!). Bomba: de longe, uma mina " +
            "3 vezes maior jogada com força, 2 corações. Vômito: ele vomita no chão e alaga o andar de baixo inteiro por 4 s; " +
            "quem estiver no chão queima (os outros inimigos também, menos ele), então suba numa plataforma. " +
            "Na Lua de Sangue ele solta até 3 raios vermelhos do olho em direção ao Freny (3 corações), que desintegram tudo no caminho. " +
            "Na pancada ele dá um pulinho e a tela treme. Depois de cada golpe ele descansa " +
            "3 s: é a hora de bater. Acertar o olho tira o dobro. Vale 5 000 pontos, 30 moedas e deixa um baú" +
            (Features.GoldenChest ? " (e às vezes a chave do baú dourado)" : "") + ". " +
            (Features.CyclopsFlatScore ? "Enquanto um Ciclope estiver vivo, os outros inimigos valem só 1 ponto cada (com ou sem combo), " : "Enquanto um Ciclope estiver vivo, ") +
            "toca a música dele e as tochas tremem vermelhas.");
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
        Entry(parent, art ? art.sword : null, "Mouse",
            "Durante a partida o ponteiro some e fica preso no jogo, para um clique não sair da tela " +
            "(no navegador, ele prende no primeiro clique). Esc solta o mouse e pausa; ele volta nos menus.");
        Entry(parent, null, "Lua de Sangue",
            "Dia de 1 minuto, noite de 4. A cada 3 noites a lua fica vermelha por 30 s no meio da noite (a primeira uns 10 minutos de corrida). Nesse tempo, " +
            "os inimigos ficam 25% mais rápidos, tiram 1 coração a mais, recuperam vida (1 a cada 3 s, o Ciclope também) " +
            "e ficam de olhos vermelhos, deixando um rastro vermelho.");
        Entry(parent, art ? art.rageHelmet : null, "Dificuldade",
            "Fácil, Médio ou Freny: quanto maior, mais inimigos, mais rápidos e mais cedo. " +
            "Com o tempo e os pontos a Arena aperta, mas para de apertar no primeiro Ciclope (50 mil) e só volta em 1 milhão." +
            (Features.EnemyToughening ? " E cada Ciclope derrotado deixa os outros inimigos mais fortes até o fim da corrida: +25% de vida nos 4 primeiros, depois +10% por Ciclope, " +
            "até o máximo de 3x (quem tinha 1 de vida vai no máximo a 2), e um pouco mais rápidos." : ""));
        if (Features.Cyclops) Entry(parent, null, "Música do chefe",
            "Quando o Ciclope chega, a música da Arena dá lugar à dele, mais pesada e rápida, e as tochas piscam " +
            "rápido e ficam vermelhas. Quando ele cai, tudo volta ao normal.");
        Entry(parent, Achievements.Icon(Achievement.Cyclops), "Conquistas",
            "10 desafios (pausa > Conquistas). Só valem corridas manuais: depois que o piloto joga, a corrida não " +
            "dá mais conquistas. No fim da corrida, o servidor confere cada conquista nova e o site mostra quantos % " +
            "dos jogadores têm cada uma.");
        Entry(parent, art ? art.run : null, "XP e piloto automático",
            "Matar dá XP, e cada nível pede mais XP que o anterior. Cada nível vale 1 ponto para o piloto no " +
            "Armeiro: reação (1 a 5 pontos por nível), esquiva com dash (3), pegar o item certo (4: busca armadura, poção com " +
            "pouca vida e munição acabando, até no meio da luta), melhor alvo (5) e caçar chefe (6: granada e rede no Ciclope, " +
            "e entra no olho mais cedo). O piloto abaixa das bolas de feitiço retas, bebe a poção no último coração (ou no penúltimo com um Golem perto), " +
            "atira na mina-aranha de longe e foge quando ela chega perto, e usa a rede e o cajado quando tem. " +
            "Enquanto o piloto joga, cai 1/3 das moedas e as mortes dão metade do XP. Corridas no Automático " +
            "têm ranking próprio e recomeçam sozinhas.");
    }

    private static Sprite spiderIcon;

    private static Sprite SpiderMineIcon() {
        if (spiderIcon == null) {
            Sprite[] idle = FrameAnimator.Load("SpiderMine/SpiderMine Idle");
            spiderIcon = idle.Length > 0 ? idle[0] : null;
        }
        return spiderIcon;
    }

    // the daily missions, the titles and the stats (DailyMissions, PlayerTitles, AccountStats)
    private static void Missions(Transform parent, MenuArt art) {
        if (Features.DailyMissions) Entry(parent, art ? art.coin : null, "Missões do dia",
            "3 missões por dia (fácil, média e difícil), as mesmas para todos os jogadores; mudam à meia-noite " +
            "de Brasília. Pagam " + DailyMissions.Rewards[0] + ", " + DailyMissions.Rewards[1] + " e " +
            DailyMissions.Rewards[2] + " moedas, na hora em que você cumpre. Só valem corridas manuais da Arena: " +
            "depois que o piloto joga, a corrida não conta mais. \"Numa corrida\" pede tudo na mesma corrida; " +
            "as outras somam as corridas do dia. Veja o progresso em Missões do dia, no menu.");
        Entry(parent, Achievements.Icon(Achievement.Supreme), "Títulos",
            "Escolha um em Conquistas > Títulos e ele aparece ao lado do seu nome no ranking do jogo e do site. " +
            "Cada conquista confirmada pelo servidor libera um (o Ciclope dá Matador de Gigantes, o Supremo " +
            "Caçador de Magos...), e terminar uma corrida manual libera Gladiador. Rei da Arena só aparece " +
            "enquanto você for o 1º do Freny, no geral ou no do dia.");
        Entry(parent, art ? art.run : null, "Estatísticas",
            "Em Conquistas > Estatísticas: corridas, tempo de jogo, melhor combo, recorde de cada dificuldade " +
            "e inimigos derrotados de cada tipo, Ciclopes também. Contam todas as corridas do save, com o piloto " +
            "automático também, e vão para a nuvem com o save.");
    }

    // ---- cada corrida diferente (Blessings, WeeklyMutators, Rebirth, FrenyLook) ----

    private static void BlessingsTopic(Transform parent) {
        Entry(parent, null, "Bênçãos",
            "Vêm do baú dourado: ao abrir, o jogo para e mostra 3 bênçãos sorteadas entre as que você ainda não tem. Escolha uma (clique, " +
            "toque, setas e Enter, ou 1, 2, 3). Ela vale até o fim da corrida, soma com as outras e aparece embaixo das armas. " +
            "No automático o piloto escolhe sozinho. Nenhuma mexe nos pontos, então a corrida vale para o ranking.");
        // two per row, so the ten fit on a 720p screen
        for (int i = 0; i < Blessings.All.Length; i += 2) {
            Blessings.Info a = Blessings.All[i];
            Blessings.Info b = i + 1 < Blessings.All.Length ? Blessings.All[i + 1] : null;
            Entry(parent, Blessings.Icon(a.id), a.name + (b != null ? " · " + b.name : ""),
                a.name + ": " + a.what + (b != null ? "\n" + b.name + ": " + b.what : ""));
        }
    }

    private static void MutatorsTopic(Transform parent) {
        Entry(parent, null, "Mutador da semana",
            "Toda semana (segunda-feira, horário de Brasília) entra uma regra nova na Arena, a mesma para todo " +
            "mundo, e vale para o ranking normal. A da semana aparece em Jogar > Arena e no começo da corrida. " +
            "Esta semana: " + WeeklyMutators.Of(WeeklyMutators.ThisWeek).name + ".");
        foreach (WeeklyMutators.Info info in WeeklyMutators.All) {
            Entry(parent, null, info.name, info.what);
        }
    }

    private static void RebirthTopic(Transform parent, MenuArt art) {
        if (Features.Rebirth) Entry(parent, null, "Renascer " + Rebirth.Star(1),
            "Com tudo do Armeiro comprado e no nível máximo, Jogar > Renascer troca todas as melhorias (as do " +
            "Armeiro e as do piloto automático) e o nível, que volta ao 1, por um bônus para sempre: +5% de dano " +
            "e +5% de moedas a cada renascimento, somando, e uma estrela ★ ao lado do nome no ranking (no jogo " +
            "e no site). Moedas, recordes, conquistas e o visual ficam. Alguns visuais só abrem renascendo.");
        if (!Features.Looks) {
            return;
        }
        Entry(parent, art ? art.berserkerHelmet : null, "Visual",
            "Em Jogar > Visual o Freny troca de elmo, de capa e de cor da roupa. Uns se compram com moedas, " +
            "outros abrem com uma conquista, renascendo ou só no baú dourado do Ciclope. Só muda o visual, não a luta. Na Fúria e com " +
            "armadura ele ainda mostra os capacetes deles.");
        foreach (FrenyLook.Item item in FrenyLook.All) {
            if (item.unlock != FrenyLook.Unlock.Free && item.unlock != FrenyLook.Unlock.Coins && FrenyLook.Reachable(item)) {
                Entry(parent, null, item.name, FrenyLook.HowToGet(item) + ".");
            }
        }
    }

    // the second arena (AcidPitArena, Tentacle, Crawler, CrumbleBlock, FallingSpike)
    private static void AccountTopic(Transform parent, MenuArt art) {
        Entry(parent, art ? art.frenyIdle != null && art.frenyIdle.Length > 0 ? art.frenyIdle[0] : null : null, "Conta ou convidado",
            "O convidado joga tudo, mas o recorde dele não entra no ranking: a caixa de NOVO RECORDE oferece Criar conta para salvar. " +
            "Ao criar a conta, o progresso, as conquistas e as estatísticas vêm junto, e o recorde daquela corrida vai para o ranking.");
        Entry(parent, null, "Apelido",
            "Só um jogador pode ter cada apelido (José e Jose contam como o mesmo): 3 a 16 letras, números, _ ou -. " +
            "Troque em Perfil > Conta, onde também dá para sair da conta.");
        Entry(parent, null, "Perfil",
            "No menu e na pausa: abates por inimigo e por arma, tempo no manual e no automático, quedas por causa, recordes, noites e Luas de Sangue. " +
            "Atualiza no fim de cada corrida. Ver meu perfil no site abre a sua página pública.");
        Entry(parent, null, "Amigos",
            "Cada conta tem um código, como FRENY-4F7K. Adicione pelo código ou pelo apelido, no jogo ou no site; o outro aceita ou recusa. " +
            "Até " + Online.FriendsService.MaxFriends + " amigos. No Ranking, o modo Amigos mostra vocês por dificuldade, e Ver compara as estatísticas.");
    }

    private static void AcidPit(Transform parent) {
        Entry(parent, FirstFrame("Liquids/Acid 100x100"), "Poço de Ácido",
            "A segunda arena: abre quando você faz " + ArenaVariant.AcidPitScore / 1000 + " mil pontos numa corrida da Arena " +
            "(escolha em Arena, no menu, ou em Nova corrida). É a mesma arena tomada pelo ácido, com os mesmos rankings. " +
            "O chão tem poças de ácido que nunca secam: queimam (1 coração) e deixam o Freny lento. Pule por cima.");
        Entry(parent, FirstFrame("Tentacle/Tentacle Idle"), "Tentáculo",
            "Sai de uma poça de ácido e fica lá. Se o Freny fica parado 1 s, ele cospe um jato de ácido para o alto " +
            "que cai onde o Freny estava (1 coração) e deixa uma poça no chão: não fique parado. Pisca antes de cuspir. " +
            "Um golpe que não mata faz ele mergulhar e voltar em outra poça; depois de uns 12 s ele afunda de vez. " +
            "2 de vida (um combo mata), 250 pontos.");
        Entry(parent, FirstFrame("Crawler/Crawler Walk"), "Rastejante",
            "Um olho com pernas que anda pelo chão, pelas paredes e pelo teto. Do teto, bem em cima do Freny, ele treme " +
            "e se solta em cima dele (1 coração); no chão, morde. Atire para cima quando ele estiver no teto. " +
            "2 de vida, 200 pontos.");
        Entry(parent, FirstFrame("Traps/Falling spike"), "Espinhos do teto",
            "De tempos em tempos um espinho se solta do teto em cima de onde o Freny vai: ele treme 1 s antes de cair. " +
            "Tira 1 coração do Freny e machuca os inimigos (sem pontos). Mais frequentes com a Arena mais difícil.");
        Entry(parent, FirstFrame("Traps/Crumbling block"), "Plataformas que desmoronam",
            "Duas plataformas por corrida são de pedra rachada: pisou, elas tremem e desmoronam logo depois. " +
            "Voltam 5 s mais tarde.");
        Entry(parent, null, "Plataformas que se movem",
            "Até duas plataformas por corrida, de cor mais clara, deslizam de um lado para o outro " +
            "e levam junto quem estiver em cima. Calcule o pulo.");
    }

    private static Sprite FirstFrame(string path) {
        Sprite[] frames = FrameAnimator.Load(path);
        return frames.Length > 0 ? frames[0] : null;
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
