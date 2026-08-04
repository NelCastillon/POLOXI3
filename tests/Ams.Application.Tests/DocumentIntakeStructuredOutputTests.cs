using System.Text.Json;
using Ams.Application.Features.DocumentIntake;
using Xunit;

namespace Ams.Application.Tests;

public sealed class DocumentIntakeStructuredOutputTests
{
    [Fact]
    public void ExtractedField_DeserializesFromStrictStructuredJson()
    {
        const string json = """
        {
          "entityTypeCode": "SUBMISSION",
          "entityKey": "root",
          "path": "submission.businessName",
          "value": "Contoso Manufacturing",
          "valueTypeCode": "STRING",
          "confidence": 0.97,
          "sourcePage": 1,
          "boundingBoxJson": "[0,0,1,1]"
        }
        """;

        var field = JsonSerializer.Deserialize<ExtractedDocumentField>(json);

        Assert.NotNull(field);
        Assert.Equal("submission.businessName", field.Path);
        Assert.Equal(0.97m, field.Confidence);
        Assert.Equal(1, field.SourcePage);
    }

    [Fact]
    public void Classification_RequiresStronglyTypedConfidence()
    {
        const string json = "{\"documentTypeCode\":\"ACORD_125\",\"confidence\":0.94}";
        var result = JsonSerializer.Deserialize<DocumentClassificationOutput>(json);
        Assert.Equal("ACORD_125", result!.DocumentTypeCode);
        Assert.Equal(0.94m, result.Confidence);
    }
}
