using System;
using System.Collections.Generic;
using System.Linq;
using Legal.Application;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal — Phase E2 dual-direction retrieval merge.
//
// MergeMatterContextItems folds counter-oriented passages into the support-oriented candidate set
// without duplicating a passage already present, keyed on the strongest stable anchor (PassageId,
// then EvidenceItemId, then normalized SourceReference+Title). Support order must be preserved and
// counter items appended, so Stage 1 support ranking is never disturbed.
// ────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DualDirectionMergeTests
{
    private static LegalMatterContextItem Item(
        Guid? passageId = null,
        Guid? evidenceItemId = null,
        string sourceReference = "REF",
        string title = "TITLE") =>
        new(
            MatterId: Guid.NewGuid(),
            LegalDocumentId: Guid.NewGuid(),
            LegalDocumentVersionId: Guid.NewGuid(),
            PassageId: passageId,
            EvidenceItemId: evidenceItemId,
            FactPropositionId: null,
            Title: title,
            Text: "text",
            SourceReference: sourceReference,
            PageNumber: null,
            ExtractionMethodCode: "M",
            EvidenceStateCode: "PROPOSED",
            FactStateCode: "UNKNOWN",
            IsDecisionAuthoritative: false,
            RelevanceScore: 0.5m,
            DocumentTypeCode: null,
            DimensionCode: null);

    [Fact]
    public void Merge_AppendsCounterItems_PreservingSupportOrder()
    {
        var s1 = Item(passageId: Guid.NewGuid());
        var s2 = Item(passageId: Guid.NewGuid());
        var c1 = Item(passageId: Guid.NewGuid());

        var merged = LegalDecisionService.MergeMatterContextItems([s1, s2], [c1]).ToArray();

        Assert.Equal(3, merged.Length);
        Assert.Same(s1, merged[0]);
        Assert.Same(s2, merged[1]);
        Assert.Same(c1, merged[2]);
    }

    [Fact]
    public void Merge_DeduplicatesByPassageId()
    {
        var passageId = Guid.NewGuid();
        var support = Item(passageId: passageId);
        var counterDuplicate = Item(passageId: passageId);

        var merged = LegalDecisionService.MergeMatterContextItems([support], [counterDuplicate]).ToArray();

        Assert.Single(merged);
        Assert.Same(support, merged[0]);
    }

    [Fact]
    public void Merge_DeduplicatesByEvidenceItemId_WhenNoPassageId()
    {
        var evidenceId = Guid.NewGuid();
        var support = Item(passageId: null, evidenceItemId: evidenceId);
        var counterDuplicate = Item(passageId: null, evidenceItemId: evidenceId);

        var merged = LegalDecisionService.MergeMatterContextItems([support], [counterDuplicate]).ToArray();

        Assert.Single(merged);
    }

    [Fact]
    public void Merge_DeduplicatesBySourceReferenceAndTitle_WhenNoIds()
    {
        var support = Item(passageId: null, evidenceItemId: null, sourceReference: "doc-1", title: "Clause 4");
        var counterDuplicate = Item(passageId: null, evidenceItemId: null, sourceReference: "DOC-1", title: "clause 4");

        var merged = LegalDecisionService.MergeMatterContextItems([support], [counterDuplicate]).ToArray();

        Assert.Single(merged);
    }

    [Fact]
    public void Merge_EmptyCounter_ReturnsSupportUnchanged()
    {
        var s1 = Item(passageId: Guid.NewGuid());

        var merged = LegalDecisionService.MergeMatterContextItems([s1], []).ToArray();

        Assert.Single(merged);
        Assert.Same(s1, merged[0]);
    }
}
