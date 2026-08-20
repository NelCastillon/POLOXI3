SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @SystemUserId UNIQUEIDENTIFIER='00000000-0000-0000-0000-000000000002';
DECLARE @Now DATETIME2(7)=SYSUTCDATETIME();

DECLARE @Concepts TABLE(SchemeCode VARCHAR(100),ConceptCode VARCHAR(100),ConceptTypeCode VARCHAR(50),PreferredLabel NVARCHAR(250),ParentCode VARCHAR(100),IsAbstract BIT,PRIMARY KEY(SchemeCode,ConceptCode));
INSERT @Concepts VALUES
('RISK_EXPOSURE','EXPOSURE.AUTO_DRIVER','EXPOSURE',N'Driver','EXPOSURE.DRIVER_COUNT',0),
('RISK_EXPOSURE','EXPOSURE.GARAGING_ADDRESS','EXPOSURE',N'Garaging Address','EXPOSURE.AREA',0),
('COVERAGE_TYPE','COVERAGE.PHYSICAL_DAMAGE','COVERAGE',N'Physical Damage','COVERAGE.AUTO',1);

INSERT knowledge.KnowledgeConcept
(KnowledgeConceptId,ConceptSchemeId,ConceptCode,ConceptTypeCode,PreferredLabel,NormalizedPreferredLabel,Definition,ParentConceptId,IsAbstract,IsSelectable,StatusCode,EffectiveFromUtc,EffectiveToUtc,VersionNumber,SupersedesConceptId,TenantId,IsSystemDefined,OwnerUserId,BusinessStewardUserId,TechnicalStewardUserId,DefinitionSource,LicensingNotes,CreatedByUserId,CreatedDateUtc,IsDeleted)
SELECT NEWID(),scheme.ConceptSchemeId,source.ConceptCode,source.ConceptTypeCode,source.PreferredLabel,UPPER(source.PreferredLabel),N'AgencyBinder curated insurance reference concept for '+source.PreferredLabel+N'.',parent.KnowledgeConceptId,source.IsAbstract,CASE WHEN source.IsAbstract=1 THEN 0 ELSE 1 END,N'PUBLISHED',@Now,NULL,1,NULL,NULL,1,@SystemUserId,@SystemUserId,@SystemUserId,N'AgencyBinder curated insurance reference catalog',N'AgencyBinder-authored reference terminology; no proprietary standard or carrier dataset is reproduced.',@SystemUserId,@Now,0
FROM @Concepts source
JOIN knowledge.ConceptScheme scheme ON scheme.SchemeCode=source.SchemeCode AND scheme.TenantId IS NULL AND scheme.IsDeleted=0
LEFT JOIN knowledge.KnowledgeConcept parent ON parent.ConceptSchemeId=scheme.ConceptSchemeId AND parent.ConceptCode=source.ParentCode AND parent.StatusCode=N'PUBLISHED' AND parent.IsDeleted=0
WHERE NOT EXISTS(SELECT 1 FROM knowledge.KnowledgeConcept target WHERE target.ConceptSchemeId=scheme.ConceptSchemeId AND target.ConceptCode=source.ConceptCode AND target.VersionNumber=1);

INSERT knowledge.ConceptLabel(ConceptLabelId,KnowledgeConceptId,Label,NormalizedLabel,LabelTypeCode,LanguageCode,Source,IsSearchable,IsDeprecated,TenantId,IsSystemDefined,CreatedByUserId,CreatedDateUtc,IsDeleted)
SELECT NEWID(),concept.KnowledgeConceptId,concept.PreferredLabel,UPPER(concept.PreferredLabel),N'PREFERRED',N'en-US',N'AgencyBinder curated insurance reference catalog',1,0,NULL,1,@SystemUserId,@Now,0
FROM knowledge.KnowledgeConcept concept JOIN @Concepts source ON source.ConceptCode=concept.ConceptCode
WHERE concept.TenantId IS NULL AND concept.IsDeleted=0 AND NOT EXISTS(SELECT 1 FROM knowledge.ConceptLabel label WHERE label.KnowledgeConceptId=concept.KnowledgeConceptId AND label.NormalizedLabel=UPPER(concept.PreferredLabel) AND label.IsDeleted=0);

DECLARE @Relationships TABLE(SubjectCode VARCHAR(100),PredicateCode VARCHAR(100),ObjectCode VARCHAR(100),PRIMARY KEY(SubjectCode,PredicateCode,ObjectCode));
INSERT @Relationships VALUES
('LOB.COMMERCIAL_AUTO','RELATED_TO','ASSET.VEHICLE'),
('LOB.COMMERCIAL_AUTO','RELATED_TO','EXPOSURE.AUTO_DRIVER'),
('LOB.COMMERCIAL_AUTO','RELATED_TO','EXPOSURE.GARAGING_ADDRESS'),
('LOB.COMMERCIAL_AUTO','COVERS','COVERAGE.LIABILITY'),
('LOB.COMMERCIAL_AUTO','COVERS','COVERAGE.PHYSICAL_DAMAGE'),
('COVERAGE.PHYSICAL_DAMAGE','RELATED_TO','COVERAGE.COLLISION'),
('COVERAGE.PHYSICAL_DAMAGE','RELATED_TO','COVERAGE.COMPREHENSIVE');

INSERT knowledge.ConceptRelationship(ConceptRelationshipId,SubjectConceptId,PredicateCode,ObjectConceptId,RelationshipStrength,Source,EffectiveFromUtc,EffectiveToUtc,StatusCode,TenantId,IsSystemDefined,CreatedByUserId,CreatedDateUtc,IsDeleted)
SELECT NEWID(),subject.KnowledgeConceptId,source.PredicateCode,object.KnowledgeConceptId,1,N'AgencyBinder curated insurance reference catalog',@Now,NULL,N'PUBLISHED',NULL,1,@SystemUserId,@Now,0
FROM @Relationships source
JOIN knowledge.KnowledgeConcept subject ON subject.ConceptCode=source.SubjectCode AND subject.TenantId IS NULL AND subject.IsDeleted=0
JOIN knowledge.KnowledgeConcept object ON object.ConceptCode=source.ObjectCode AND object.TenantId IS NULL AND object.IsDeleted=0
WHERE NOT EXISTS(SELECT 1 FROM knowledge.ConceptRelationship target WHERE target.SubjectConceptId=subject.KnowledgeConceptId AND target.PredicateCode=source.PredicateCode AND target.ObjectConceptId=object.KnowledgeConceptId AND target.IsDeleted=0);

INSERT knowledge.ConceptVersion(ConceptVersionId,KnowledgeConceptId,VersionNumber,StatusCode,SnapshotJson,ChangeReason,CreatedByUserId,CreatedDateUtc)
SELECT NEWID(),concept.KnowledgeConceptId,concept.VersionNumber,concept.StatusCode,(SELECT concept.KnowledgeConceptId,concept.ConceptSchemeId,concept.ConceptCode,concept.ConceptTypeCode,concept.PreferredLabel,concept.Definition,concept.ParentConceptId,concept.StatusCode,concept.EffectiveFromUtc,concept.VersionNumber FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'Insurance Intelligence ontology completion.',@SystemUserId,@Now
FROM knowledge.KnowledgeConcept concept JOIN @Concepts source ON source.ConceptCode=concept.ConceptCode
WHERE concept.TenantId IS NULL AND concept.IsDeleted=0 AND NOT EXISTS(SELECT 1 FROM knowledge.ConceptVersion version WHERE version.KnowledgeConceptId=concept.KnowledgeConceptId AND version.VersionNumber=concept.VersionNumber);

MERGE knowledge.Configuration target USING(VALUES
(N'DOCUMENT_FIELD_SCHEME_ROUTING',N'[{"pathContains":"lineOfBusiness","schemeCode":"INSURANCE_PRODUCT"},{"pathSuffix":".lob","schemeCode":"INSURANCE_PRODUCT"},{"pathContains":"coverage","schemeCode":"COVERAGE_TYPE"},{"pathContains":"industry","schemeCode":"BUSINESS_CLASSIFICATION"},{"pathContains":"naics","schemeCode":"BUSINESS_CLASSIFICATION"},{"pathSuffix":".state","schemeCode":"US_JURISDICTION"},{"pathContains":"stateCode","schemeCode":"US_JURISDICTION"},{"pathContains":"documentType","schemeCode":"DOCUMENT_TYPE"},{"pathContains":"carrier","schemeCode":"CARRIER_PRODUCT"}]',N'JSON',N'Ordered, allowlisted extracted-document field path patterns mapped to canonical Knowledge scheme codes.')) source(ConfigurationCode,ConfigurationValue,DataTypeCode,Description)
ON target.TenantId IS NULL AND target.ConfigurationCode=source.ConfigurationCode
WHEN MATCHED THEN UPDATE SET ConfigurationValue=source.ConfigurationValue,DataTypeCode=source.DataTypeCode,Description=source.Description,IsActive=1,ModifiedDateUtc=@Now
WHEN NOT MATCHED THEN INSERT(TenantId,ConfigurationCode,ConfigurationValue,DataTypeCode,Description,IsSystemDefined,IsActive,CreatedDateUtc) VALUES(NULL,source.ConfigurationCode,source.ConfigurationValue,source.DataTypeCode,source.Description,1,1,@Now);

UPDATE knowledge.KnowledgeConcept SET DefinitionSource=N'AgencyBinder curated insurance reference catalog',LicensingNotes=N'AgencyBinder-authored reference terminology; no proprietary standard or carrier dataset is reproduced.',ModifiedDateUtc=@Now,ModifiedByUserId=@SystemUserId WHERE TenantId IS NULL AND IsSystemDefined=1 AND DefinitionSource=N'AMS enterprise synthetic insurance reference catalog';
UPDATE knowledge.ConceptLabel SET Source=N'AgencyBinder curated insurance reference catalog',ModifiedDateUtc=@Now WHERE TenantId IS NULL AND IsSystemDefined=1 AND Source=N'AMS enterprise synthetic reference catalog';
UPDATE knowledge.ConceptRelationship SET Source=N'AgencyBinder curated insurance reference catalog',ModifiedDateUtc=@Now,ModifiedByUserId=@SystemUserId WHERE TenantId IS NULL AND IsSystemDefined=1 AND Source=N'AMS enterprise synthetic reference catalog';
