using System.Threading;
using System.Threading.Tasks;

namespace Hgerman.ContentStudio.Application.Interfaces;

public interface ISubtitleService
{
    Task<string?> GenerateAssSubtitleAsync(
        string bilingualScript,
        string outputFolderPath,
        CancellationToken cancellationToken = default);
}