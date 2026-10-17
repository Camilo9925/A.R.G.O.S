using Microsoft.JSInterop;

namespace Argos.Web.Theme;

/// <summary>
/// Guarda no navegador a escolha entre tema escuro (padrão) e claro (Art. X.4).
/// Se o armazenamento local não estiver disponível, mantém o tema escuro sem falhar.
/// </summary>
public sealed class ThemePreference(IJSRuntime jsRuntime)
{
    private const string StorageKey = "argos.theme";
    private const string Light = "light";
    private const string Dark = "dark";

    public async Task<bool> LoadIsDarkModeAsync()
    {
        try
        {
            return await jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey) != Light;
        }
        catch (JSException)
        {
            return true;
        }
    }

    public async Task SaveAsync(bool isDarkMode)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, isDarkMode ? Dark : Light);
        }
        catch (JSException)
        {
            // Sem armazenamento local: a escolha vale só para esta sessão.
        }
    }
}
