using System.Threading.Tasks;

namespace SMTMS.Avalonia.Services;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync();
}
