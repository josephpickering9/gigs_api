using Microsoft.EntityFrameworkCore;
using Gigs.Exceptions;
using Gigs.Models;
using Gigs.Services;
using Gigs.Types;
using Gigs.DTOs;

namespace Gigs.Repositories;

public class GigRepository(Database database) : IGigRepository
{
    public async Task<(List<Gig> Items, int TotalCount)> GetAllAsync(GetGigsFilter filter)
    {
        var query = database.Gig
            .Include(g => g.Venue)
            .Include(g => g.Festival)
            .Include(g => g.Acts).ThenInclude(ga => ga.Artist)
            .Include(g => g.Attendees).ThenInclude(a => a.Person)
            .AsNoTracking()
            .AsQueryable();

        if (filter.VenueId.HasValue)
        {
            query = query.Where(g => g.VenueId == filter.VenueId.Value);
        }

        if (filter.FestivalId.HasValue)
        {
            query = query.Where(g => g.FestivalId == filter.FestivalId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            query = query.Where(g => g.Venue != null && g.Venue.City.ToLower().Contains(filter.City.ToLower()));
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(g => g.Date >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(g => g.Date <= filter.ToDate.Value);
        }

        if (filter.ArtistId.HasValue)
        {
            query = query.Where(g => g.Acts.Any(a => a.ArtistId == filter.ArtistId.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var searchTerm = filter.Search.ToLower();
            query = query.Where(g =>
                (g.Venue != null && g.Venue.Name.ToLower().Contains(searchTerm)) ||
                g.Acts.Any(a => a.Artist != null && a.Artist.Name.ToLower().Contains(searchTerm))
            );
        }

        if (filter.AttendeeId.HasValue)
        {
            query = query.Where(g => g.Attendees.Any(a => a.PersonId == filter.AttendeeId.Value));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(g => g.Date)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Gig?> GetByIdAsync(GigId id)
    {
        return await database.Gig
            .Include(g => g.Venue)
            .Include(g => g.Festival)
            .Include(g => g.Acts).ThenInclude(ga => ga.Artist)
            .Include(g => g.Acts).ThenInclude(ga => ga.Songs).ThenInclude(s => s.Song)
            .Include(g => g.Attendees).ThenInclude(a => a.Person)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<Gig?> FindAsync(VenueId venueId, DateOnly date, ArtistId artistId)
    {
        return await database.Gig
            .Include(g => g.Venue)
            .Include(g => g.Festival)
            .Include(g => g.Acts).ThenInclude(ga => ga.Artist)
            .Include(g => g.Acts).ThenInclude(ga => ga.Songs).ThenInclude(s => s.Song)
            .Include(g => g.Attendees).ThenInclude(a => a.Person)
            .FirstOrDefaultAsync(g => g.VenueId == venueId && g.Date == date && g.Acts.Any(a => a.ArtistId == artistId && a.IsHeadliner));
    }

    public async Task<Gig> AddAsync(Gig gig)
    {
        await database.Gig.AddAsync(gig);
        await database.SaveChangesAsync();
        return gig;
    }

    public async Task<Gig> UpdateAsync(Gig gig)
    {
        database.Gig.Update(gig);
        await database.SaveChangesAsync();
        return gig;
    }

    public async Task DeleteAsync(GigId id)
    {
        var gig = await database.Gig.FindAsync(id);
        if (gig != null)
        {
            database.Gig.Remove(gig);
            await database.SaveChangesAsync();
        }
    }
}
