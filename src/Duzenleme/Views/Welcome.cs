namespace Duzenleme.Views;

/// <summary>Karşılamayı açar (App.ShowWelcome buradan geçer).</summary>
internal static class Welcome
{
    /// <param name="rerun">Yeniden kurulum: var olanlar silinmez, yalnızca eksikler eklenir; atlamak yalnızca pencereyi kapatır.</param>
    public static void Show(bool rerun) => WelcomeWindow.ShowOrActivate(rerun);
}
