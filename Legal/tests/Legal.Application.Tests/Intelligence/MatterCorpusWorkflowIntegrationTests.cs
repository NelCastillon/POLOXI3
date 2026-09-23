using System.Reflection;
using System.Text;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class MatterCorpusWorkflowIntegrationTests
{
    [Fact]
    public async Task SyntheticAccidentReport_PersistsSourceLinkedPropositionsAndReachesDecisionContext()
    {
        var tenantId=Guid.NewGuid();
        var userId=Guid.NewGuid();
        var matterId=Guid.NewGuid();
        var bytes=Encoding.UTF8.GetBytes("Officer observed rear impact. Emily reported the signal was red. Witness account conflicts on speed.");
        var corpus=new RecordingCorpusRepository();
        var binaryStore=new RecordingBinaryStore();
        var interpreter=new AccidentReportInterpreter();
        var service=new LegalDocumentIntakeService(
            binaryStore,new AcceptingValidator(),new CleanScanner(),new AccidentReportExtractor(),interpreter,
            corpus,DecisionRepositoryProxy.Create());
        var request=new LegalDocumentIntakeRequest(
            tenantId,userId,matterId,"synthetic-accident-report.txt","text/plain",bytes.Length,"matter-corpus-e2e")
        {
            DomainPackCode="PERSONAL_INJURY",
            DocumentTypeCode="ACCIDENT_REPORT",
        };

        var document=await service.IngestAsync(request,new MemoryStream(bytes));
        var context=await new LegalMatterContextRetriever(corpus).RetrieveAsync(
            tenantId,userId,matterId,"rear impact red signal witness speed",corpus.Settings);

        Assert.Equal(LegalDocumentProcessingStates.Processed,document.StatusCode);
        Assert.Equal("PERSONAL_INJURY",document.DomainPackCode);
        Assert.Equal("ACCIDENT_REPORT",document.DocumentTypeCode);
        Assert.Equal(bytes,binaryStore.StoredBytes);
        Assert.StartsWith("immutable://",Assert.Single(document.Versions).StorageReference);
        Assert.NotEmpty(corpus.Passages);
        Assert.All(corpus.Passages,passage=>Assert.False(string.IsNullOrWhiteSpace(passage.SourceSpanJson)));
        Assert.Equal("ACCIDENT_REPORT",corpus.SemanticProposal?.DocumentTypeCode);
        Assert.Contains("Witness speed is ambiguous.",corpus.SemanticProposal!.Ambiguities);
        Assert.Equal("PERSONAL_INJURY",interpreter.DomainPackCode);
        Assert.Single(corpus.EvidenceItems);
        Assert.Single(corpus.FactPropositions);
        Assert.Equal(corpus.EvidenceItems[0].LegalEvidenceItemId,corpus.FactPropositions[0].Support.Single().LegalEvidenceItemId);
        Assert.Equal(DecisionResearchRouteCodes.MatterCorpus,context.SourceRouteCode);
        var item=Assert.Single(context.Items);
        Assert.Equal(corpus.FactPropositions[0].LegalFactPropositionId,item.FactPropositionId);
        Assert.False(item.IsDecisionAuthoritative);
        Assert.Equal(LegalEvidenceStates.Proposed,item.EvidenceStateCode);
    }

    private sealed class RecordingBinaryStore:ILegalDocumentBinaryStore
    {
        public byte[] StoredBytes{get;private set;}=[];
        public async Task<string> StoreImmutableAsync(Guid tenantId,Guid documentId,string fileName,Stream content,CancellationToken cancellationToken=default)
        {
            using var buffer=new MemoryStream();
            await content.CopyToAsync(buffer,cancellationToken);
            StoredBytes=buffer.ToArray();
            return $"immutable://{tenantId:N}/{documentId:N}/1";
        }
    }

    private sealed class AcceptingValidator:ILegalDocumentIntakeValidator
    {
        public Task ValidateAsync(LegalDocumentIntakeRequest request,Stream content,CancellationToken cancellationToken=default)=>Task.CompletedTask;
    }

    private sealed class CleanScanner:ILegalDocumentSecurityScanner
    {
        public Task<LegalDocumentSecurityScanResult> ScanAsync(string fileName,string contentType,Stream content,CancellationToken cancellationToken=default)=>
            Task.FromResult(new LegalDocumentSecurityScanResult("CLEAN"));
    }

    private sealed class AccidentReportExtractor:ILegalDocumentExtractionRouter
    {
        public Task<DocumentExtractionResult> ExtractAsync(DocumentExtractionRequest request,CancellationToken cancellationToken=default)=>
            Task.FromResult(new DocumentExtractionResult(
                "SYNTHETIC_NATIVE","ACCIDENT_REPORT","1",DateTime.UtcNow,
                [new DocumentExtractedPage(1,"Officer observed rear impact. Emily reported the signal was red. Witness account conflicts on speed.",LegalDocumentExtractionMethods.NativeText,1m,8.5m,11m,"inch",
                    [new DocumentExtractedParagraph(1,"Officer observed rear impact. Emily reported the signal was red. Witness account conflicts on speed.","BODY","{\"page\":1}",1m,"{\"offset\":0,\"length\":99}")])],
                [],[],"synthetic://extraction"));
    }

    private sealed class AccidentReportInterpreter:ILegalDocumentSemanticInterpreter
    {
        public string? DomainPackCode{get;private set;}
        public Task<LegalDocumentSemanticProposal> InterpretAsync(Guid tenantId,Guid matterId,Guid documentId,Guid documentVersionId,string? domainPackCode,IReadOnlyCollection<DecisionDomainConceptDto> domainConcepts,IReadOnlyCollection<LegalDocumentPassageDto> passages,string correlationId,CancellationToken cancellationToken=default)
        {
            DomainPackCode=domainPackCode;
            var passage=Assert.Single(passages);
            return Task.FromResult(new LegalDocumentSemanticProposal(
                "ACCIDENT_REPORT",0.99m,
                [new LegalEvidenceSemanticProposal("E1",passage.LegalDocumentPassageId,"OFFICER_OBSERVATION","LIABILITY","Officer observed a rear impact.",0.98m,"COLLISION_SEQUENCE","MATTER_EVIDENCE")],
                [new LegalFactSemanticProposal("F1","The vehicles were involved in a rear-impact collision.",LegalFactStates.Alleged,0.96m)],
                [new LegalSemanticRelationshipProposal("E1","F1",LegalDocumentRelationshipTypes.Supports,"Direct source-linked observation.")],
                ["Witness speed is ambiguous."],[]));
        }
    }

    private sealed class RecordingCorpusRepository:ILegalDocumentCorpusRepository
    {
        private readonly List<LegalDocumentDto> documents=[];
        public DecisionRetrievalArchitectureSettings Settings{get;}=new(true,true,10,10000,false,true,false,true);
        public List<LegalDocumentPassageDto> Passages{get;}=[];
        public LegalDocumentSemanticProposal? SemanticProposal{get;private set;}
        public List<LegalEvidenceItemDto> EvidenceItems{get;}=[];
        public List<LegalFactPropositionDto> FactPropositions{get;}=[];

        public Task<Guid> CreateDocumentAsync(Guid documentId,LegalDocumentIntakeRequest request,string sha256Hash,string storageReference,string malwareStatusCode,CancellationToken cancellationToken=default)
        {
            var versionId=Guid.NewGuid();
            documents.Add(new LegalDocumentDto(documentId,request.MatterId,request.FileName,request.ContentType,LegalDocumentProcessingStates.Processing,request.DocumentTypeCode,request.DomainPackCode,DateTime.UtcNow,
                [new LegalDocumentVersionDto(versionId,1,sha256Hash,storageReference,request.FileSizeBytes,malwareStatusCode,LegalDocumentProcessingStates.Processing,null,null,null,DateTime.UtcNow)]));
            return Task.FromResult(versionId);
        }

        public Task SaveExtractionAsync(Guid tenantId,Guid userId,Guid documentVersionId,string correlationId,DocumentExtractionResult extraction,IReadOnlyCollection<LegalDocumentPassageDto> passages,CancellationToken cancellationToken=default)
        {
            Passages.AddRange(passages);
            ReplaceDocumentStatus(LegalDocumentProcessingStates.EnrichmentPending,extraction);
            return Task.CompletedTask;
        }

        public Task SaveSemanticProposalAsync(Guid tenantId,Guid userId,Guid matterId,Guid documentId,Guid documentVersionId,LegalDocumentSemanticProposal proposal,CancellationToken cancellationToken=default)
        {
            SemanticProposal=proposal;
            foreach(var evidence in proposal.EvidenceItems)
                EvidenceItems.Add(new(Guid.NewGuid(),matterId,documentVersionId,evidence.PassageId,evidence.EvidenceTypeCode,evidence.DimensionCode,evidence.Summary,LegalEvidenceStates.Proposed,evidence.Confidence,"DYNAMIC_LLM",evidence.DomainConceptCode,evidence.VerificationProfileCode));
            foreach(var fact in proposal.FactPropositions)
            {
                var relationship=proposal.Relationships.Single(item=>item.TargetProposalKey==fact.ProposalKey);
                var evidenceIndex=proposal.EvidenceItems.ToList().FindIndex(item=>item.ProposalKey==relationship.SourceProposalKey);
                var evidence=EvidenceItems[evidenceIndex];
                FactPropositions.Add(new(Guid.NewGuid(),matterId,fact.PropositionText,LegalFactStates.Alleged,"DYNAMIC_LLM",fact.Confidence,false,
                    [new LegalPropositionSupportDto(Guid.NewGuid(),Guid.Empty,evidence.LegalEvidenceItemId,relationship.RelationshipTypeCode,relationship.Rationale)]));
            }
            ReplaceDocumentStatus(LegalDocumentProcessingStates.Processed,null);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<LegalMatterContextItem>> SearchMatterContextAsync(Guid tenantId,Guid userId,Guid matterId,string query,int maximumItems,int maximumCharacters,CancellationToken cancellationToken=default)=>
            Task.FromResult<IReadOnlyCollection<LegalMatterContextItem>>(FactPropositions.Select(fact=>
            {
                var document=documents.Single();
                var version=document.Versions.Single();
                var evidence=EvidenceItems.Single(item=>fact.Support.Any(support=>support.LegalEvidenceItemId==item.LegalEvidenceItemId));
                return new LegalMatterContextItem(matterId,document.LegalDocumentId,version.LegalDocumentVersionId,evidence.LegalDocumentPassageId,evidence.LegalEvidenceItemId,fact.LegalFactPropositionId,document.FileName,fact.PropositionText,version.StorageReference,1,LegalDocumentExtractionMethods.NativeText,evidence.EvidenceStateCode,fact.FactStateCode,false,1m,document.DocumentTypeCode,evidence.DimensionCode);
            }).ToArray());

        public Task<IReadOnlyCollection<LegalDocumentDto>> GetMatterDocumentsAsync(Guid tenantId,Guid matterId,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyCollection<LegalDocumentDto>>(documents);
        public Task<IReadOnlyCollection<LegalDocumentPassageDto>> GetDocumentPassagesAsync(Guid tenantId,Guid documentVersionId,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyCollection<LegalDocumentPassageDto>>(Passages);
        public Task<DecisionRetrievalArchitectureSettings> GetRetrievalArchitectureSettingsAsync(CancellationToken cancellationToken=default)=>Task.FromResult(Settings);
        public Task<IReadOnlyCollection<LegalMatterContextItem>> SearchRoutedMatterContextAsync(Guid tenantId,Guid matterId,string query,IReadOnlyCollection<string> documentTypeCodes,int maximumItems,CancellationToken cancellationToken=default)=>SearchMatterContextAsync(tenantId,Guid.Empty,matterId,query,maximumItems,int.MaxValue,cancellationToken);
        public Task<IReadOnlyCollection<LegalMatterContextItem>> SearchLegacyProjectionAsync(Guid tenantId,Guid userId,Guid matterId,string query,int maximumItems,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyCollection<LegalMatterContextItem>>([]);
        public Task MarkProcessingFailedAsync(Guid tenantId,Guid userId,Guid documentVersionId,string errorCode,string errorMessage,CancellationToken cancellationToken=default)=>Task.CompletedTask;
        public Task<Guid?> GetDocumentMatterIdAsync(Guid tenantId,Guid documentVersionId,CancellationToken cancellationToken=default)=>Task.FromResult<Guid?>(documents.SingleOrDefault()?.MatterId);
        public Task<IReadOnlyCollection<DecisionRetrievalTelemetryDto>> GetRetrievalTelemetryAsync(Guid tenantId,Guid? matterId,Guid? decisionSessionId,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyCollection<DecisionRetrievalTelemetryDto>>([]);
        public Task PersistRetrievalTelemetryAsync(Guid tenantId,Guid userId,DecisionRetrievalTelemetry telemetry,CancellationToken cancellationToken=default)=>Task.CompletedTask;

        private void ReplaceDocumentStatus(string status,DocumentExtractionResult? extraction)
        {
            var document=documents.Single();
            var version=document.Versions.Single();
            var updatedVersion=version with{ProcessingStatusCode=status,ExtractionProviderCode=extraction?.ProviderCode??version.ExtractionProviderCode,ExtractionModelCode=extraction?.ModelCode??version.ExtractionModelCode,ExtractionModelVersion=extraction?.ModelVersion??version.ExtractionModelVersion};
            documents[0]=document with{StatusCode=status,Versions=[updatedVersion]};
        }
    }

    private class DecisionRepositoryProxy:DispatchProxy
    {
        public static ILegalDecisionRepository Create()=>DispatchProxy.Create<ILegalDecisionRepository,DecisionRepositoryProxy>();
        protected override object? Invoke(MethodInfo? targetMethod,object?[]? args)
        {
            if(targetMethod?.Name==nameof(ILegalDecisionRepository.GetDomainPackAsync))
                return Task.FromResult<DecisionDomainPackDto?>(null);
            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
