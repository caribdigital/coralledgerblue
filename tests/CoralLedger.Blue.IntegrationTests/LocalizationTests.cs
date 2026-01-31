using System.Globalization;
using System.Xml.Linq;

namespace CoralLedger.Blue.IntegrationTests;

/// <summary>
/// Tests to verify Haitian Creole language support is properly implemented.
/// These tests validate:
/// 1. Haitian Creole culture is supported by .NET
/// 2. Resource files exist with proper translations
/// 3. Cultural formatting (dates, numbers) works correctly
/// </summary>
public class LocalizationTests
{

    [Fact]
    public void SupportedCultures_IncludesEnglish()
    {
        // Arrange
        var culture = new CultureInfo("en");
        
        // Assert
        Assert.NotNull(culture);
        Assert.Equal("en", culture.Name);
        Assert.Equal("English", culture.EnglishName);
    }

    [Fact]
    public void SupportedCultures_IncludesSpanish()
    {
        // Arrange
        var culture = new CultureInfo("es");
        
        // Assert
        Assert.NotNull(culture);
        Assert.Equal("es", culture.Name);
        Assert.Equal("Spanish", culture.EnglishName);
    }

    [Fact]
    public void SupportedCultures_IncludesHaitianCreole()
    {
        // Arrange
        var culture = new CultureInfo("ht");
        
        // Assert
        Assert.NotNull(culture);
        Assert.Equal("ht", culture.Name);
        Assert.Equal("Haitian Creole", culture.EnglishName);
        Assert.Equal("créole haïtien", culture.NativeName);
    }

    [Fact]
    public void HaitianCreole_UsesCorrectDateFormat()
    {
        // Arrange
        var culture = new CultureInfo("ht");
        var testDate = new DateTime(2026, 1, 31);
        
        // Act
        var formattedDate = testDate.ToString("d", culture);
        
        // Assert - Haitian Creole uses dd/MM/yyyy format
        Assert.Equal("31/01/2026", formattedDate);
    }

    [Fact]
    public void HaitianCreole_UsesCorrectNumberFormat()
    {
        // Arrange
        var culture = new CultureInfo("ht");
        var testNumber = 1234567.89;
        
        // Act
        var formattedNumber = testNumber.ToString("N2", culture);
        
        // Assert - Haitian Creole uses comma as decimal separator
        // and non-breaking space as group separator
        Assert.Contains("567,89", formattedNumber);
        Assert.Contains("234", formattedNumber);
    }

    [Fact]
    public void ResourceFiles_ExistForAllLanguages()
    {
        // Arrange - Use absolute path to resource files
        var solutionDir = FindSolutionDirectory();
        var resourceDir = Path.Combine(solutionDir, "src", "CoralLedger.Blue.Web", "Resources");
        
        // Act & Assert
        Assert.True(Directory.Exists(resourceDir), 
            $"Resource directory should exist at {resourceDir}");
        Assert.True(File.Exists(Path.Combine(resourceDir, "SharedResources.resx")), 
            "English resource file should exist");
        Assert.True(File.Exists(Path.Combine(resourceDir, "SharedResources.es.resx")), 
            "Spanish resource file should exist");
        Assert.True(File.Exists(Path.Combine(resourceDir, "SharedResources.ht.resx")), 
            "Haitian Creole resource file should exist");
    }

    [Fact]
    public void HaitianCreoleResourceFile_ContainsAllRequiredKeys()
    {
        // Arrange
        var solutionDir = FindSolutionDirectory();
        var resourceDir = Path.Combine(solutionDir, "src", "CoralLedger.Blue.Web", "Resources");
        var htResourceFile = Path.Combine(resourceDir, "SharedResources.ht.resx");
        var enResourceFile = Path.Combine(resourceDir, "SharedResources.resx");
        
        // Act
        var htDoc = XDocument.Load(htResourceFile);
        var enDoc = XDocument.Load(enResourceFile);
        
        var htKeys = htDoc.Descendants("data")
            .Select(d => d.Attribute("name")?.Value)
            .Where(k => k != null)
            .ToHashSet();
        
        var enKeys = enDoc.Descendants("data")
            .Select(d => d.Attribute("name")?.Value)
            .Where(k => k != null)
            .ToHashSet();
        
        // Assert - Haitian Creole should have all the same keys as English
        Assert.Equal(enKeys.Count, htKeys.Count);
        
        foreach (var key in enKeys)
        {
            Assert.Contains(key, htKeys);
        }
    }

    [Fact]
    public void HaitianCreoleResourceFile_HasNoEmptyValues()
    {
        // Arrange
        var solutionDir = FindSolutionDirectory();
        var resourceDir = Path.Combine(solutionDir, "src", "CoralLedger.Blue.Web", "Resources");
        var htResourceFile = Path.Combine(resourceDir, "SharedResources.ht.resx");
        
        // Act
        var doc = XDocument.Load(htResourceFile);
        var emptyValues = doc.Descendants("data")
            .Where(d => string.IsNullOrWhiteSpace(d.Element("value")?.Value))
            .Select(d => d.Attribute("name")?.Value)
            .ToList();
        
        // Assert
        Assert.Empty(emptyValues);
    }

    [Theory]
    [InlineData("Nav_Dashboard", "Tablo")]
    [InlineData("Nav_Map", "Kat")]
    [InlineData("Nav_Home", "Lakay")]
    [InlineData("Map_Controls", "Kontwòl Kat")]
    [InlineData("Dashboard_Title", "Tablo Entèlijans Marin")]
    [InlineData("Common_Loading", "Chajman...")]
    [InlineData("Common_Save", "Sove")]
    [InlineData("Common_Cancel", "Anile")]
    public void HaitianCreoleResourceFile_ContainsExpectedTranslations(string key, string expectedValue)
    {
        // Arrange
        var solutionDir = FindSolutionDirectory();
        var resourceDir = Path.Combine(solutionDir, "src", "CoralLedger.Blue.Web", "Resources");
        var htResourceFile = Path.Combine(resourceDir, "SharedResources.ht.resx");
        
        // Act
        var doc = XDocument.Load(htResourceFile);
        var actualValue = doc.Descendants("data")
            .FirstOrDefault(d => d.Attribute("name")?.Value == key)
            ?.Element("value")?.Value;
        
        // Assert
        Assert.NotNull(actualValue);
        Assert.Equal(expectedValue, actualValue);
    }

    [Fact]
    public void AllResourceFiles_HaveSameNumberOfKeys()
    {
        // Arrange
        var solutionDir = FindSolutionDirectory();
        var resourceDir = Path.Combine(solutionDir, "src", "CoralLedger.Blue.Web", "Resources");
        var enResourceFile = Path.Combine(resourceDir, "SharedResources.resx");
        var esResourceFile = Path.Combine(resourceDir, "SharedResources.es.resx");
        var htResourceFile = Path.Combine(resourceDir, "SharedResources.ht.resx");
        
        // Act
        var enDoc = XDocument.Load(enResourceFile);
        var esDoc = XDocument.Load(esResourceFile);
        var htDoc = XDocument.Load(htResourceFile);
        
        var enCount = enDoc.Descendants("data").Count();
        var esCount = esDoc.Descendants("data").Count();
        var htCount = htDoc.Descendants("data").Count();
        
        // Assert
        Assert.Equal(enCount, esCount);
        Assert.Equal(enCount, htCount);
        Assert.True(enCount > 0, "Resource files should contain at least one key");
    }

    private static string FindSolutionDirectory()
    {
        var directory = Directory.GetCurrentDirectory();
        while (directory != null && !File.Exists(Path.Combine(directory, "CoralLedger.Blue.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }
        
        if (directory == null)
        {
            throw new InvalidOperationException("Could not find solution directory");
        }
        
        return directory;
    }
}
