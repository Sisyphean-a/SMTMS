using System.Threading.Tasks;

namespace SMTMS.Avalonia.Services;

public interface ICommitMessageService
{
    Task<string?> ShowDialogAsync();
}
