using System.Data.Common;
using Gigs.Models;
using Gigs.Services;
using Gigs.Services.AI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace GigsTests;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var dbName = Guid.NewGuid().ToString();

        builder.ConfigureServices(services =>
        {
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<Database>));

            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            var dbConnectionDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbConnection));

            if (dbConnectionDescriptor != null)
            {
                services.Remove(dbConnectionDescriptor);
            }

            services.AddDbContext<Database>((container, options) =>
            {
                options.UseInMemoryDatabase(dbName);
            });

            var aiServiceDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAiEnrichmentService));
            if (aiServiceDescriptor != null)
            {
                services.Remove(aiServiceDescriptor);
            }

            var mockAiService = new Mock<IAiEnrichmentService>();
            mockAiService.Setup(x => x.EnrichGig(It.IsAny<Gig>()))
                .ReturnsAsync(new AiEnrichmentResult());
            services.AddScoped(_ => mockAiService.Object);
        });

        builder.UseEnvironment("Development");
    }
}
