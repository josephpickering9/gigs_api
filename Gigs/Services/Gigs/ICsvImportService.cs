namespace Gigs.Services;

public interface ICsvImportService
{
    Task<int> ImportGigsAsync(Stream csvStream);
}
