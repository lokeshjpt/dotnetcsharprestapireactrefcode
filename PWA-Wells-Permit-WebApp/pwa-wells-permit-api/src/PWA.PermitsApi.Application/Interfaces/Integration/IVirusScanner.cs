namespace PWA.PermitsApi.Application.Interfaces.Integration;

public interface IVirusScanner
{
    Task<bool> ScanAsync(string fileName, byte[] content, CancellationToken cancellationToken = default);
}
