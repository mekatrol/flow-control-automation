namespace Server.Services;

public interface IStartupDataValidator
{
    Task ValidateAsync(CancellationToken cancellationToken);
}