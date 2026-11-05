# Checagem de compilação (sem Unity)

Compila os scripts do jogo contra as DLLs do `UnityEngine` (pacote NuGet `UnityEngine.Modules`)
para pegar erros de C# sem precisar abrir a Unity nem ter licença. Roda no GitHub Actions a cada push/PR.

Rodar localmente (precisa do .NET SDK 8):

```
dotnet build .ci/CompileCheck
```

Limites: não compila scripts de Editor nem `Assets/Scripts/Coop` (usa pacotes da Unity que não estão aqui;
`CoopStubs.cs` faz o papel do que o resto do jogo chama). O uGUI (`UnityEngine.UI`: `Button`, `Slider`, `Text`...) vem do
pacote NuGet `Unity3D.UnityEngine.UI` 2020.3 (a DLL real, não stubs), então os menus feitos em código são
checados também; TextMeshPro não está incluído. Isso não substitui testar na Unity: só garante que o
código compila.
