using System.Text.Json;
using System.Text.Json.Serialization;
using Gigs.DataModels;
using Xunit;

namespace GigsTests;

public class SerializationTests
{
    [Fact]
    public void GetFestivalResponse_SerializesDateOnly_Correctly()
    {
        // Arrange
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
        options.Converters.Add(new JsonStringEnumConverter());
        // Note: Program.cs adds IdJsonConverterFactory, but that shouldn't affect DateOnly.
        
        var response = new GetFestivalResponse
        {
            StartDate = new DateOnly(2023, 1, 15),
            EndDate = new DateOnly(2023, 1, 20)
        };

        // Act
        var json = JsonSerializer.Serialize(response, options);

        // Assert
        Assert.Contains("\"StartDate\": \"2023-01-15\"", json);
        Assert.Contains("\"EndDate\": \"2023-01-20\"", json);
    }
}
