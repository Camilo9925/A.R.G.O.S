namespace Argos.Web.Services.Api;

/// <summary>
/// Envio de um formulário pequeno (diálogos dos cadastros de apoio): erros de campo vão para <see cref="FieldErrors"/>;
/// o resto (409, 404, falha de rede) volta como mensagem para o aviso do diálogo (Art. X.6).
/// </summary>
public static class SaveForm
{
    public static async Task<(string? Error, T? Saved)> RunAsync<T>(
        FieldErrors errors, IReadOnlyCollection<string> shownFields, Func<Task<T>> save)
        where T : class
    {
        errors.Clear();

        try
        {
            return (null, await save());
        }
        catch (ApiProblemException problem) when (problem.IsValidation)
        {
            errors.Load(problem.Errors);
            return (errors.Except(shownFields).FirstOrDefault(), null);
        }
        catch (ApiProblemException problem)
        {
            return (problem.Message, null);
        }
        catch (HttpRequestException)
        {
            return ("Não foi possível falar com o servidor. Tente novamente.", null);
        }
    }
}
