using System;
using UnityEngine;
using UnityEngine.UI;
using Online;

// The account pages (José, 2026-11-02): the start screen the first time the game opens (Entrar,
// Criar conta, Jogar como convidado), and the Conta page from Configurações and the Perfil.
public static class AccountScreens {

    private const float Width = 680f;
    private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

    private static OnlineServices Online => OnlineServices.Instance;

    // ---- the start screen ----

    public static MenuPage Welcome(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Bem-vindo à Arena", Width, Middle);
        page.Label("Com uma conta, o seu progresso vai junto para outro computador, o seu recorde entra no ranking " +
                   "e você tem apelido, perfil e amigos. Como convidado, dá para jogar tudo, mas os recordes não vão para o ranking.");
        page.Space(4f);
        page.Button("Entrar", () => stack.Push(Login(stack, stack.Back)));
        page.Button("Criar conta", () => stack.Push(Create(stack, stack.Back)));
        page.Button("Jogar como convidado", () => {
            Online.ChooseGuest();
            stack.Back();
        });
        return page;
    }

    // ---- Entrar ----

    // done: what happens after it worked (the page below it comes back)
    public static MenuPage Login(MenuStack stack, Action done) {
        MenuPage page = new MenuPage(stack.transform, "Entrar", Width, Middle, 6f);
        InputField email = page.Input("Email", "");
        InputField password = page.Input("Senha", "", true);
        Text message = null;
        Button enter = null;
        enter = page.Button("Entrar", () => {
            message.text = "Aguarde...";
            enter.interactable = false;
            Online.SignInWithEmail(email.text, password.text, error => {
                if (page.Root == null) return;
                enter.interactable = true;
                if (error != null) {
                    message.text = error;
                    return;
                }
                stack.Back();   // this page
                done?.Invoke();
            });
        });
        page.Button("Esqueci a senha", () => {
            if (string.IsNullOrEmpty(email.text.Trim())) {
                message.text = "Digite o seu email primeiro";
                return;
            }
            message.text = "Aguarde...";
            Online.SendPasswordReset(email.text, error => {
                if (page.Root != null) message.text = error ?? "Se esse email tem conta, mandamos o link para trocar a senha.";
            });
        });
        message = page.Label("");
        page.Button("Voltar", stack.Back);
        return page;
    }

    // ---- Criar conta ----

    public static MenuPage Create(MenuStack stack, Action done) {
        MenuPage page = new MenuPage(stack.transform, "Criar conta", Width, Middle, 6f);
        page.Label("O seu progresso de convidado vem junto: moedas, melhorias, conquistas e estatísticas.");
        InputField nick = page.Input("Apelido", "", false, ProfileService.MaxNickLength);
        InputField email = page.Input("Email", "");
        InputField password = page.Input("Senha", "", true);
        Text message = null;
        Button create = null;
        create = page.Button("Criar conta", () => {
            message.text = "Aguarde...";
            create.interactable = false;
            Online.CreateAccount(nick.text, email.text, password.text, error => {
                if (page.Root == null) return;
                create.interactable = true;
                if (error != null && !error.StartsWith("Conta criada", StringComparison.Ordinal)) {
                    message.text = error;
                    return;
                }
                stack.Back();   // this page
                done?.Invoke();
            });
        });
        page.Button("Já tenho conta: entrar", () => stack.ReplaceTop(Login(stack, done)));
        message = page.Label("");
        page.Button("Voltar", stack.Back);
        return page;
    }

    // ---- Conta ----

    public static MenuPage Account(MenuStack stack) {
        OnlineServices online = Online;
        MenuPage page = new MenuPage(stack.transform, "Conta", Width, Middle, 6f);
        FirebaseAuthService auth = online != null ? online.Auth : null;
        if (auth == null || online.State == OnlineServices.Status.Disabled) {
            page.Label("O online está desligado nesta versão.", MenuKit.TextSize);
            page.Button("Voltar", stack.Back);
            return page;
        }
        if (!auth.IsSignedIn) {
            page.Label(online.State == OnlineServices.Status.Connecting ? "Conectando..." : "Sem conexão agora.", MenuKit.TextSize);
            page.Button("Voltar", stack.Back);
            return page;
        }
        if (auth.IsAnonymous) {
            page.Label("Você está jogando como convidado. Crie uma conta para salvar os recordes no ranking e ter apelido, perfil e amigos.");
            page.Button("Criar conta", () => stack.Push(Create(stack, () => stack.ReplaceTop(Account(stack)))));
            page.Button("Entrar", () => stack.Push(Login(stack, () => stack.ReplaceTop(Account(stack)))));
            page.Button("Voltar", stack.Back);
            return page;
        }

        Text mail = page.Label("Email: " + auth.Email, MenuKit.TextSize);
        Text verified = page.Label(VerifiedText(auth));
        Text message = null;
        Button resend = null;
        resend = page.Button("Reenviar email de verificação", () => {
            message.text = "Aguarde...";
            online.SendVerifyEmail(error => {
                if (page.Root != null) message.text = error ?? "Mandamos o link para " + auth.Email;
            });
        }, auth.EmailVerified != true);
        online.StartCoroutine(RefreshVerified(auth, page, verified, resend));

        if (online.Profiles.HasNick) page.Label("Código de amigo: " + online.Profiles.Code);
        else page.Label("Esta conta ainda não tem apelido: escolha um para ter perfil e amigos.");
        InputField nick = page.Input("Apelido", online.Profiles.Nick ?? online.Save.playerName, false, ProfileService.MaxNickLength);
        page.Button(online.Profiles.HasNick ? "Trocar apelido" : "Salvar apelido", () => {
            message.text = "Aguarde...";
            online.ChangeNick(nick.text, error => {
                if (page.Root != null) message.text = error ?? "Apelido salvo: " + online.Profiles.Nick;
            });
        });
        page.Button("Sair da conta", () => {
            online.SignOut();
            stack.ReplaceTop(Welcome(stack));
        });
        message = page.Label("");
        page.Button("Voltar", stack.Back);
        return page;
    }

    private static string VerifiedText(FirebaseAuthService auth) {
        return auth.EmailVerified == null ? "Conferindo o email..."
            : auth.EmailVerified == true ? "<color=#7CFC8A>Email verificado</color>"
            : "<color=#FFD84A>Email ainda não verificado: veja a sua caixa de entrada</color>";
    }

    private static System.Collections.IEnumerator RefreshVerified(FirebaseAuthService auth, MenuPage page, Text verified, Button resend) {
        verified.supportRichText = true;
        yield return auth.RefreshEmailVerified();
        if (page.Root == null) yield break;
        verified.text = VerifiedText(auth);
        resend.interactable = auth.EmailVerified != true;
    }
}
