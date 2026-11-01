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
        page.Choice("Dificuldade", Array.ConvertAll(ArenaDifficulty.Ranked, ArenaDifficulty.Label), start, i => {
            board = ArenaDifficulty.Ranked[i];
            fill();
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
        page.Button("Conta e nome", () => stack.Push(Account(stack)));
        page.Button(closeLabel, stack.Back);
        page.SelectFirst = again;

        fill = () => {
            if (online == null) {
                status.text = "O online está desligado nesta versão";
                return;
            }
            status.text = online.State == OnlineServices.Status.Online ? "Só valem corridas manuais · o oficial é o do Difícil"
                : online.State == OnlineServices.Status.Connecting ? "Conectando..." : "Sem conexão: mostrando o último ranking";
            string self = online.Auth != null ? online.Auth.UserId : null;
            List<LeaderboardEntry> top = online.TopFor(board);
            for (int i = 0; i < names.Length; i++) {
                if (i >= top.Count) {
                    names[i].text = i == 0 ? "Ninguém no ranking ainda" : "";
                    scores[i].text = "";
                    continue;
                }
                LeaderboardEntry entry = top[i];
                bool me = entry.userId == self;
                names[i].text = Mark((i + 1) + ". " + Rebirth.RankingName(entry.name, entry.rebirths), me);
                scores[i].text = Mark(entry.score.ToString(), me);
            }
            best.text = "Seu recorde no " + ArenaDifficulty.Label(board) + ": " + online.BestFor(board) + " · corridas: " + online.Save.runsPlayed;
        };
        fill();
        if (host != null && online != null) {
            host.StartCoroutine(Refresh(online, page, fill));
        }
        return page;
    }

    private static string Mark(string text, bool me) {
        return me ? "<color=#EE8FFC>" + text + "</color>" : text;
    }

    private static IEnumerator Refresh(OnlineServices online, MenuPage page, Action fill) {
        if (online.State == OnlineServices.Status.Online) {
            yield return online.RefreshLeaderboard();
        }
        if (page.Root != null) {   // still open
            fill();
        }
    }

    // the name on the ranking, and an email to keep the progress on other computers
    public static MenuPage Account(MenuStack stack) {
        OnlineServices online = OnlineServices.Instance;
        MenuPage page = new MenuPage(stack.transform, "Conta", 640f, new Vector2(0.5f, 0.5f), 6f);
        if (online == null) {
            page.Label("O online está desligado nesta versão.", MenuKit.TextSize);
            page.Button("Voltar", stack.Back);
            return page;
        }
        Text message = null;
        Action<string> done = error => {
            if (message != null) {
                message.text = error == null ? "Pronto!" : "Erro: " + error;
            }
            stack.ReplaceTop(Account(stack));
        };

        InputField name = page.Input("Nome", online.Save.playerName, false, OnlineServices.MaxNameLength);
        page.Button("Salvar nome", () => {
            online.SetPlayerName(name.text);
            message.text = "Nome salvo: " + online.Save.playerName;
        });

        FirebaseAuthService auth = online.Auth;
        if (auth != null && auth.IsSignedIn && !auth.IsAnonymous) {
            page.Label("Conta: " + auth.Email, MenuKit.TextSize);
            page.Button("Sair da conta", () => {
                online.SignOut();
                stack.ReplaceTop(Account(stack));
            });
        } else if (auth != null && auth.IsSignedIn) {
            page.Label("Conta de visitante. Coloque um email para levar o progresso para outros computadores.");
            InputField email = page.Input("Email", "");
            InputField password = page.Input("Senha", "", true);
            Button link = null, signIn = null;
            Action<bool> busy = on => {
                link.interactable = !on;
                signIn.interactable = !on;
                if (on) {
                    message.text = "Aguarde...";
                }
            };
            link = page.Button("Salvar progresso neste email", () => {
                busy(true);
                online.LinkEmail(email.text, password.text, done);
            });
            signIn = page.Button("Entrar com este email", () => {
                busy(true);
                online.SignInWithEmail(email.text, password.text, done);
            });
        } else {
            page.Label(online.State == OnlineServices.Status.Connecting ? "Conectando..." : "Sem conexão agora.", MenuKit.TextSize);
        }
        message = page.Label("");
        page.Button("Voltar", stack.Back);
        return page;
    }

    private static string JumpKey() {
        return InputBindings.KeyName(InputBindings.Keyboard(GameAction.Jump)).Replace(" esq.", "").Replace(" dir.", "");
    }
}
