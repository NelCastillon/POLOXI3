using System.Collections.Generic;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// POLOXI Assertion → Proposition Alignment → Evidence Relation (AER). Verifies the pure classifier
// separates "could this be relevant?" (RetrievalRank) from "same thing?" (AlignmentResult) from "what
// does it establish?" (EvidenceRelation), using the blueprint's P32 phone example. Ambiguous/hedged
// assertions resolve to INSUFFICIENT rather than a wrong SUPPORT, and a different actor is disqualifying.
public sealed class LegalAssertionEvidenceRelationTests
{
    private const string P32 = "Defendant used his phone immediately before impact.";

    private static LegalAssertionAlignmentResult Resolve(
        string assertion,
        double retrievalRank = 0.9,
        IReadOnlyCollection<string>? propActors = null,
        IReadOnlyCollection<string>? assertionActors = null)
        => LegalAssertionEvidenceRelation.Resolve(new LegalAssertionAlignmentInput(
            assertion, P32, propActors, assertionActors, new RetrievalRank(retrievalRank)));

    // ── "I used my phone immediately before impact." → DIRECT_SUPPORT ──
    [Fact]
    public void OnPointPositiveAssertion_ProducesDirectSupport()
    {
        var r = Resolve(
            "Defendant stated he used his phone immediately before impact.",
            propActors: ["defendant"], assertionActors: ["defendant"]);

        Assert.Equal(LegalEvidenceRelations.DirectSupport, r.EvidenceRelationCode);
        Assert.Equal(LegalDocumentRelationshipTypes.Supports, r.RelationshipTypeCode);
        Assert.True(r.EntailmentSatisfied);
        Assert.Equal(LegalDimensionAlignment.Match, r.Polarity);
        Assert.Equal(LegalDimensionAlignment.Match, r.Actor);
    }

    // ── "I was NOT using my phone before impact." → CONTRADICTS ──
    [Fact]
    public void OpposedPolarity_ProducesContradiction()
    {
        var r = Resolve(
            "Defendant was not using his phone before impact.",
            propActors: ["defendant"], assertionActors: ["defendant"]);

        Assert.Equal(LegalEvidenceRelations.Contradicts, r.EvidenceRelationCode);
        Assert.Equal(LegalDocumentRelationshipTypes.Contradicts, r.RelationshipTypeCode);
        Assert.False(r.EntailmentSatisfied);
        Assert.Equal(LegalDimensionAlignment.Mismatch, r.Polarity);
    }

    // ── "I don't remember whether I used my phone." → INSUFFICIENT ──
    [Fact]
    public void HedgedAssertion_ProducesInsufficient()
    {
        var r = Resolve(
            "Defendant said he does not remember whether he used his phone before impact.",
            propActors: ["defendant"], assertionActors: ["defendant"]);

        Assert.Equal(LegalEvidenceRelations.Insufficient, r.EvidenceRelationCode);
        Assert.Equal(LegalDocumentRelationshipTypes.Insufficient, r.RelationshipTypeCode);
        Assert.False(r.EntailmentSatisfied);
        Assert.Equal(LegalDimensionAlignment.Unknown, r.Polarity);
    }

    // ── "My passenger was using her phone." → WRONG_ACTOR ──
    [Fact]
    public void DifferentActor_ProducesWrongActor()
    {
        var r = Resolve(
            "The passenger was using her phone immediately before impact.",
            propActors: ["defendant"], assertionActors: ["passenger"]);

        Assert.Equal(LegalEvidenceRelations.WrongActor, r.EvidenceRelationCode);
        Assert.Equal(LegalDocumentRelationshipTypes.Insufficient, r.RelationshipTypeCode);
        Assert.False(r.EntailmentSatisfied);
        Assert.Equal(LegalDimensionAlignment.Mismatch, r.Actor);
    }

    // ── Actor mismatch inferred from the closed actor lexicon (no explicit actor sets) ──
    [Fact]
    public void ActorMismatchInferredFromText_ProducesWrongActor()
    {
        var r = Resolve("The passenger used a phone immediately before impact.");

        Assert.Equal(LegalEvidenceRelations.WrongActor, r.EvidenceRelationCode);
        Assert.Equal(LegalDimensionAlignment.Mismatch, r.Actor);
    }

    // ── Off-topic assertion (right actor, wrong event) → INSUFFICIENT ──
    [Fact]
    public void OffTopicAssertion_ProducesInsufficient()
    {
        var r = Resolve(
            "Defendant paid the parking meter earlier that morning.",
            propActors: ["defendant"], assertionActors: ["defendant"]);

        Assert.Equal(LegalEvidenceRelations.Insufficient, r.EvidenceRelationCode);
    }

    // ── The three concepts must remain independent: RetrievalRank is carried, never fused ──
    [Fact]
    public void RetrievalRankIsCarriedButNeverFusedIntoRelation()
    {
        // A high retrieval rank on a contradicting assertion must NOT read as support.
        var high = Resolve(
            "Defendant was not using his phone before impact.",
            retrievalRank: 0.98, propActors: ["defendant"], assertionActors: ["defendant"]);

        Assert.Equal(0.98, high.RetrievalRank.Value, precision: 6);
        Assert.Equal(LegalEvidenceRelations.Contradicts, high.EvidenceRelationCode);
        Assert.False(high.EntailmentSatisfied);
    }

    // ── RetrievalRank validates its range ──
    [Fact]
    public void RetrievalRankOutOfRange_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new RetrievalRank(1.5));

    // ── Conditional qualifier keeps an on-point positive assertion at PARTIAL_SUPPORT ──
    [Fact]
    public void ConditionalQualifier_ProducesPartialSupport()
    {
        var r = Resolve(
            "Defendant used his phone immediately before impact, unless the log timestamps are wrong.",
            propActors: ["defendant"], assertionActors: ["defendant"]);

        Assert.Equal(LegalEvidenceRelations.PartialSupport, r.EvidenceRelationCode);
        Assert.Equal(LegalDocumentRelationshipTypes.Qualifies, r.RelationshipTypeCode);
        Assert.False(r.EntailmentSatisfied);
    }
}
