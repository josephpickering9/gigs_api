using System.Data.Common;
using Gigs.Models;
using Gigs.Services;
using Gigs.Services.AI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Gigs.Services.SetlistFm;

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

            var aiServiceDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(AiEnrichmentService));
            if (aiServiceDescriptor != null)
            {
                services.Remove(aiServiceDescriptor);
            }

            var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            var mockConfigSection = new Mock<Microsoft.Extensions.Configuration.IConfigurationSection>();
            mockConfigSection.Setup(x => x.Value).Returns("dummy-project-id");
            mockConfig.Setup(x => x[It.Is<string>(s => s == "VertexAi:ProjectId")]).Returns("dummy-project-id");

            var mockAiService = new Mock<AiEnrichmentService>(
                Mock.Of<Microsoft.Extensions.Logging.ILogger<AiEnrichmentService>>(),
                mockConfig.Object,
                (ImageSearchService?)null, 
                (SetlistFmService?)null);
            mockAiService.Setup(x => x.EnrichGig(It.IsAny<Gig>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(new Gigs.Types.Success<AiEnrichmentResult>(new AiEnrichmentResult()));
            services.AddScoped(_ => mockAiService.Object);
        });

        builder.UseEnvironment("Development");
    }
}
