using System.Collections.Generic;
using System.Linq;
using Legal.Application;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal — Phase E2 proposition-first retrieval query construction.
//
// BuildPropositionRetrievalQuery is retrieval INTENT only: it shapes the SUPPORT/COUNTER semantic
// query from the atomic proposition plus structured search concepts. It must be deterministic, must
// never drop the caller's fallback when the proposition yields nothing usable, and must never leak an
// evidence relation. These tests pin that pure behavior so the feature-gated Stage 2 path stays sound.
// ────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class PropositionRetrievalQueryTests
{
    [Fact]
    public void Support_UsesProposition_AndAppendsDistinctConcepts()
    {
        var query = LegalDecisionService.BuildPropositionRetrievalQuery(
            "The lease was terminated for non-payment",
            ["lease termination", "non-payment"],
            RetrievalDirection.Support,
            fallbackQuery: "FALLBACK");

        Assert.Equal(RetrievalDirection.Support, query.Direction);
        Assert.Equal("The lease was terminated for non-payment", query.PropositionText);
        Assert.Contains("lease termination", query.QueryText);
        Assert.DoesNotContain("FALLBACK", query.QueryText);
    }

    [Fact]
    public void EmptyProposition_FallsBackToCallerQuery()
    {
        var query = LegalDecisionService.BuildPropositionRetrievalQuery(
            "   ",
            [],
            RetrievalDirection.Support,
            fallbackQuery: "FALLBACK");

        Assert.Equal("FALLBACK", query.QueryText);
    }

    [Fact]
    public void NullConcepts_AreTreatedAsEmpty_AndDoNotThrow()
    {
        var query = LegalDecisionService.BuildPropositionRetrievalQuery(
            "Claim proposition",
            null!,
            RetrievalDirection.Counter,
            fallbackQuery: "FALLBACK");

        Assert.Equal(RetrievalDirection.Counter, query.Direction);
        Assert.Empty(query.SearchConcepts);
        Assert.Contains("Claim proposition", query.QueryText);
    }

    [Fact]
    public void BlankConcepts_AreFilteredOut()
    {
        var query = LegalDecisionService.BuildPropositionRetrievalQuery(
            "Proposition text",
            ["", "   ", "valid concept"],
            RetrievalDirection.Support,
            fallbackQuery: "FALLBACK");

        Assert.Single(query.SearchConcepts);
        Assert.Equal("valid concept", query.SearchConcepts[0]);
    }

    [Fact]
    public void Construction_IsDeterministic()
    {
        var first = LegalDecisionService.BuildPropositionRetrievalQuery(
            "Same proposition", ["a", "b"], RetrievalDirection.Support, "FALLBACK");
        var second = LegalDecisionService.BuildPropositionRetrievalQuery(
            "Same proposition", ["a", "b"], RetrievalDirection.Support, "FALLBACK");

        Assert.Equal(first.QueryText, second.QueryText);
        Assert.True(first.SearchConcepts.SequenceEqual(second.SearchConcepts));
    }
}
