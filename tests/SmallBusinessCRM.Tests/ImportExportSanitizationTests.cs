using SmallBusinessCRM.Api.Controllers;
using Xunit;

namespace SmallBusinessCRM.Tests;

public class ImportExportSanitizationTests
{
    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+cmd|' /C calc'!A0", "'+cmd|' /C calc'!A0")]
    [InlineData("-2+3+cmd|' /C calc'!A0", "'-2+3+cmd|' /C calc'!A0")]
    [InlineData("@SUM(A1:A10)", "'@SUM(A1:A10)")]
    [InlineData("\tmalicious", "'\tmalicious")]
    [InlineData("\rmalicious", "'\rmalicious")]
    [InlineData("  =FORMULA()", "'  =FORMULA()")]
    [InlineData("Normal Company Name", "Normal Company Name")]
    [InlineData("user@example.com", "user@example.com")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SanitizeForCsvExport_PrependsSingleQuoteToFormulaTriggers(string? input, string expected)
    {
        var result = ImportExportController.SanitizeForCsvExport(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("=cmd|' /C calc'!A0", "cmd|' /C calc'!A0")]
    [InlineData("+-@=payload", "payload")]
    [InlineData("@SUM(A1:A5)", "SUM(A1:A5)")]
    [InlineData("   =LeadingSpacesFormula", "LeadingSpacesFormula")]
    [InlineData("Clean Business Deal", "Clean Business Deal")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SanitizeCsvImportString_StripsLeadingFormulaCharacters(string? input, string expected)
    {
        var result = ImportExportController.SanitizeCsvImportString(input);
        Assert.Equal(expected, result);
    }
}
