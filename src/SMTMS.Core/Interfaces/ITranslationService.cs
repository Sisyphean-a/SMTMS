namespace SMTMS.Core.Interfaces;

public interface ITranslationService
{
    Task<string> TranslateAsync(string text, string targetLang = "ZH");
}
