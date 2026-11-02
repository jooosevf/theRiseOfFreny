using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Online;

// The ranking and the online account, in the menus' style. Shared by the main menu and by the
// Tab panel in the arena (OnlineHUD).
public static class OnlineScreens {

    // the top 10 of one difficulty (Hard, the official one, first); refreshed from the server
    // when the page opens. newRun: the arena's "Nova corrida" after a death, first in the list
    public static MenuPage Ranking(MenuStack stack, MonoBehaviour host, ArenaLevel first, Action newRun = null, string closeLabel = "Voltar") {
        OnlineServices online = OnlineServices.Instance;
        MenuPage page = new MenuPage(stack.transform, "Ranking", 600f, new Vector2(0.5f, 0.5f), 4f);
        Button again = null;
        if (newRun != null) {
            again = page.Button("Nova corrida (" + JumpKey() + ")", newRun);
        }

        int start = Mathf.Max(0, Array.IndexOf(ArenaDifficulty.Ranked, first));
        ArenaLevel board = ArenaDifficulty.Ranked[start];
        Action fill = null;
        bool auto = false;   // the autopilot has its own board (José, 2026-11-01)
        bool friends = false;   // you and your friends, read from their profiles (José, 2026-11-02)
        Action reload = () => {
            fill();
            if (host == null || online == null) return;
            host.StartCoroutine(friends ? RefreshFriends(online, page, fill) : Refresh(online, page, board, auto, fill));
        };
        page.Choice("Dificuldade", Array.ConvertAll(ArenaDifficulty.Ranked, ArenaDifficulty.Label), start, i => {
            board = ArenaDifficulty.Ranked[i];
            reload();
        });
        page.Choice("Modo", new[] { "Manual", "Automático", "Amigos" }, 0, i => {
            auto = i == 1;
            friends = i == 2;
            reload();
        });
        Text status = page.Label("", MenuKit.SmallTextSize);
        Text[] names = new Text[OnlineServices.LeaderboardSize];
        Text[] scores = new Text[names.Length];
        for (int i = 0; i < names.Length; i++) {
            Image row = MenuKit.MakeRow(page.Content, "", out names[i]);
            row.color = new Color(1f, 1f, 1f, i % 2 == 0 ? 0.05f : 0.02f);
            MenuKit.Size(row, -1f, 26f);
            names[i].fontSize = MenuKit.SmallTextSize;
            names[i].supportRichText = true;
            scores[i] = MenuKit.MakeText(row.transform, "", MenuKit.SmallTextSize, MenuKit.TextColor, TextAnchor.MiddleRight);
            scores[i].supportRichText = true;
            MenuKit.Size(scores[i], 130f, -1f);
        }
        Text best = page.Label("", MenuKit.SmallTextSize);
        page.Space(4f);
        page.Button(PerfilScreens.ButtonLabel, () => stack.Push(PerfilScreens.Page(stack)));
        page.Button(closeLabel, stack.Back);
        page.SelectFirst = again;

        fill = () => {
            if (online == null) {
                status.text = "O online está desligado nesta versão";
                return;
            }
            status.text = online.State == OnlineServices.Status.Online
                ? auto ? "Corridas começadas no Automático" : "Só valem corridas manuais · o oficial é o do Freny"
                : online.State == OnlineServices.Status.Connecting ? "Conectando..." : "Sem conexão: mostrando o último ranking";
            string self = online.Auth != null ? online.Auth.UserId : null;
            if (friends) {
                FillFriends(online, board, names, scores, status, best, self);
                return;
            }
            List<LeaderboardEntry> top = auto ? online.AutoTopFor(board) : online.TopFor(board);
            for (int i = 0; i < names.Length; i++) {
                if (i >= top.Count) {
                    names[i].text = i == 0 ? "Ninguém no ranking ainda" : "";
                    scores[i].text = "";
                    continue;
                }
                LeaderboardEntry entry = top[i];
                bool me = entry.userId == self;
                names[i].text = Mark((i + 1) + ". " + Rebirth.RankingName(PlayerTitles.Decorate(entry.name, entry.userId), entry.rebirths), me);
                scores[i].text = Mark(entry.score.ToString(), me);
            }
            best.text = "Seu recorde no " + ArenaDifficulty.Label(board) + (auto ? " (automático)" : "") + ": " +
                        online.BestFor(board, auto) + " · corridas: " + online.Save.runsPlayed;
        };
        fill();
        if (host != null && online != null) {
            host.StartCoroutine(Refresh(online, page, board, auto, fill));
        }
        return page;
    }

    private static void FillFriends(OnlineServices online, ArenaLevel board, Text[] names, Text[] scores, Text status, Text best, string self) {
        bool guest = online.Auth == null || online.Auth.IsAnonymous;
        status.text = guest ? "Crie uma conta para ter amigos" : "Você e seus amigos no " + ArenaDifficulty.Label(board);
        List<PerfilInfo> all = guest ? new List<PerfilInfo>() : FriendsBoard(online, board);
        int place = all.FindIndex(p => p.uid == self);
        for (int i = 0; i < names.Length; i++) {
            if (i >= all.Count) {
                names[i].text = "";
                scores[i].text = "";
                continue;
            }
            bool me = all[i].uid == self;
            names[i].text = Mark((i + 1) + ". " + all[i].apelido, me);
            scores[i].text = Mark(all[i].Best(board).ToString(), me);
        }
        best.text = place >= 0 ? "Sua posição entre os amigos: " + (place + 1) + "º de " + all.Count : "";
    }

    private static string Mark(string text, bool me) {
        return me ? "<color=#EE8FFC>" + text + "</color>" : text;
    }

    private static IEnumerator Refresh(OnlineServices online, MenuPage page, ArenaLevel board, bool auto, Action fill) {
        if (online.State == OnlineServices.Status.Online) {
            yield return online.RefreshBoard(board, auto);
            yield return PlayerTitles.Refresh(online);
        }
        if (page.Root != null) {   // still open
            fill();
        }
    }

    // Amigos: this account and its friends by their best on this difficulty, from the friends'
    // public profiles (read when the tab opens; FriendsService keeps them for a minute)
    private static IEnumerator RefreshFriends(OnlineServices online, MenuPage page, Action fill) {
        if (online.State == OnlineServices.Status.Online && online.Friends != null) {
            yield return online.Friends.Refresh();
        }
        if (page.Root != null) {
            fill();
        }
    }

    private static List<PerfilInfo> FriendsBoard(OnlineServices online, ArenaLevel board) {
        List<PerfilInfo> all = new List<PerfilInfo>();
        if (online.Friends != null) all.AddRange(online.Friends.Perfis);
        if (online.Profiles != null && online.Auth != null && online.Auth.IsSignedIn) all.Add(online.Profiles.Own());
        all.Sort((a, b) => b.Best(board).CompareTo(a.Best(board)));
        return all;
    }

    private static string JumpKey() {
        return InputBindings.KeyName(InputBindings.Keyboard(GameAction.Jump)).Replace(" esq.", "").Replace(" dir.", "");
    }
}
