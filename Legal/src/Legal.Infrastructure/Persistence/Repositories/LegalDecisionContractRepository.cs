using System.Data;
using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Infrastructure.Persistence.Repositories;

// DB-backed persistence for the Decision Contract aggregate (POLOXI.Legal_DecisionContract*). Tenant-
// scoped on every query/mutation. POLOXI remains the authoritative evaluator; nothing here scores.
public sealed class LegalDecisionContractRepository(ISqlConnectionFactory connectionFactory) : ILegalDecisionContractRepository
{
    private const string ContractColumns = """
        c.DecisionContractId, c.MatterId, c.VersionNumber, c.StatusCode,
        c.DecisionQuestion, c.ClientObjective, c.SuccessDefinition, c.DecisionDate,
        c.Jurisdiction, c.CourtOrForum, c.GoverningLaw, c.ProceduralPosture, c.CaseType, c.ApplicableLegalFramework,
        c.MovingParty, c.InitialBurden, c.UltimateBurden, c.StandardOfProofOrReview, c.BurdenNotes,
        c.EvidenceBoundary, c.AuthorityBoundary, c.SourceRestrictions, c.AuthorityCutoffDate,
        c.DecisionHorizon, c.ExternalResearchPermitted, c.ReviewBeforeActivation, c.ReviewBeforeFinal, c.SemanticValidationMode, c.Notes,
        c.CreatedByUserId, c.CreatedDateUtc, c.ApprovedByUserId, c.ApprovedDateUtc, c.RowVersion
        """;

    public async Task<DecisionContractDto?> GetCurrentContractAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var id = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            SELECT TOP 1 c.DecisionContractId
            FROM POLOXI.Legal_DecisionContract c
            WHERE c.TenantId = @TenantId AND c.MatterId = @MatterId AND c.IsDeleted = 0
            ORDER BY CASE WHEN c.StatusCode = N'ACTIVE' THEN 0 ELSE 1 END, c.VersionNumber DESC;
            """,
            new { TenantId = tenantId, MatterId = matterId }, cancellationToken: cancellationToken));

        return id is { } contractId ? await GetContractByIdAsync(tenantId, contractId, cancellationToken) : null;
    }

    public async Task<DecisionContractDto?> GetContractByIdAsync(Guid tenantId, Guid decisionContractId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await LoadContractAsync(connection, tenantId, decisionContractId, cancellationToken);
    }

    private static async Task<DecisionContractDto?> LoadContractAsync(IDbConnection connection, Guid tenantId, Guid decisionContractId, CancellationToken cancellationToken)
    {
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(
            $"""
            SELECT {ContractColumns},
                   LTRIM(RTRIM(CONCAT(ISNULL(cu.FirstName, N''), N' ', ISNULL(cu.LastName, N'')))) AS CreatedByDisplayName,
                   LTRIM(RTRIM(CONCAT(ISNULL(au.FirstName, N''), N' ', ISNULL(au.LastName, N'')))) AS ApprovedByDisplayName
            FROM POLOXI.Legal_DecisionContract c
            LEFT JOIN dbo.AspNetUsers cu ON cu.Id = c.CreatedByUserId
            LEFT JOIN dbo.AspNetUsers au ON au.Id = c.ApprovedByUserId
            WHERE c.TenantId = @TenantId AND c.DecisionContractId = @Id AND c.IsDeleted = 0;

            SELECT DecisionContractCandidateId, CandidateCode, OutcomeText, CandidateTypeCode, StateCode, DisplayOrder
            FROM POLOXI.Legal_DecisionContractCandidate
            WHERE DecisionContractId = @Id AND IsDeleted = 0
            ORDER BY DisplayOrder, CandidateCode;

            SELECT DecisionContractFactBoundaryId, FactId, FactStateCode, SnapshotText, DisplayOrder
            FROM POLOXI.Legal_DecisionContractFactBoundary
            WHERE DecisionContractId = @Id AND IsDeleted = 0
            ORDER BY DisplayOrder;

            SELECT DecisionContractTagId, TagKindCode, TagText, DisplayOrder
            FROM POLOXI.Legal_DecisionContractTag
            WHERE DecisionContractId = @Id AND IsDeleted = 0
            ORDER BY TagKindCode, DisplayOrder;
            """,
            new { TenantId = tenantId, Id = decisionContractId }, cancellationToken: cancellationToken));

        var head = await multi.ReadSingleOrDefaultAsync<ContractHeadRow>();
        if (head is null) return null;

        var candidates = (await multi.ReadAsync<DecisionContractCandidateDto>()).ToArray();
        var facts = (await multi.ReadAsync<DecisionContractFactBoundaryDto>()).ToArray();
        var tags = (await multi.ReadAsync<DecisionContractTagDto>()).ToArray();

        return new DecisionContractDto(
            head.DecisionContractId, head.MatterId, head.VersionNumber, head.StatusCode,
            head.DecisionQuestion, head.ClientObjective, head.SuccessDefinition, head.DecisionDate,
            head.Jurisdiction, head.CourtOrForum, head.GoverningLaw, head.ProceduralPosture, head.CaseType, head.ApplicableLegalFramework,
            head.MovingParty, head.InitialBurden, head.UltimateBurden, head.StandardOfProofOrReview, head.BurdenNotes,
            head.EvidenceBoundary, head.AuthorityBoundary, head.SourceRestrictions, head.AuthorityCutoffDate,
            head.DecisionHorizon, head.ExternalResearchPermitted, head.ReviewBeforeActivation, head.ReviewBeforeFinal, head.SemanticValidationMode, head.Notes,
            head.CreatedByUserId, NullIfBlank(head.CreatedByDisplayName), head.CreatedDateUtc,
            head.ApprovedByUserId, NullIfBlank(head.ApprovedByDisplayName), head.ApprovedDateUtc,
            head.RowVersion, candidates, facts, tags);
    }

    public async Task<DecisionContractDto> ProvisionAsync(Guid tenantId, Guid userId, Guid matterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var existing = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            SELECT TOP 1 DecisionContractId FROM POLOXI.Legal_DecisionContract
            WHERE TenantId = @TenantId AND MatterId = @MatterId AND IsDeleted = 0
            ORDER BY VersionNumber DESC;
            """,
            new { TenantId = tenantId, MatterId = matterId }, cancellationToken: cancellationToken));

        if (existing is null)
        {
            using var tx = connection.BeginTransaction();

            // Pre-fill from existing matter context.
            var matter = await connection.QuerySingleOrDefaultAsync<MatterContextRow>(new CommandDefinition(
                """
                SELECT Title, Jurisdiction, Posture, MatterTypeCode, Description
                FROM POLOXI.Legal_DecisionMatter
                WHERE TenantId = @TenantId AND DecisionMatterId = @MatterId AND IsDeleted = 0;
                """,
                new { TenantId = tenantId, MatterId = matterId }, transaction: tx, cancellationToken: cancellationToken));

            var contractId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionContract
                    (DecisionContractId, MatterId, VersionNumber, StatusCode, DecisionQuestion,
                     Jurisdiction, ProceduralPosture, CaseType, TenantId, CreatedByUserId)
                VALUES
                    (@DecisionContractId, @MatterId, 1, N'DRAFT', @DecisionQuestion,
                     @Jurisdiction, @ProceduralPosture, @CaseType, @TenantId, @UserId);
                """,
                new
                {
                    DecisionContractId = contractId,
                    MatterId = matterId,
                    DecisionQuestion = matter?.Title,
                    Jurisdiction = matter?.Jurisdiction,
                    ProceduralPosture = matter?.Posture,
                    CaseType = matter?.MatterTypeCode,
                    TenantId = tenantId,
                    UserId = userId
                }, transaction: tx, cancellationToken: cancellationToken));

            // Pre-fill competing outcomes from existing L1 candidate decision nodes, if present.
            var candidateNodes = (await connection.QueryAsync<(string NodeText, string? PlacementKey)>(new CommandDefinition(
                """
                SELECT TOP 6 NodeText, PlacementKey
                FROM POLOXI.Legal_DecisionNode
                WHERE TenantId = @TenantId AND MatterId = @MatterId AND IsDeleted = 0
                  AND (NodeKindCode = N'Candidate' OR NodeLevel = 1)
                ORDER BY PlacementKey, CanonicalKey;
                """,
                new { TenantId = tenantId, MatterId = matterId }, transaction: tx, cancellationToken: cancellationToken))).ToArray();

            var order = 0;
            foreach (var node in candidateNodes)
            {
                order++;
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO POLOXI.Legal_DecisionContractCandidate
                        (DecisionContractCandidateId, DecisionContractId, CandidateCode, OutcomeText, StateCode, DisplayOrder, TenantId, CreatedByUserId)
                    VALUES
                        (NEWID(), @DecisionContractId, @CandidateCode, @OutcomeText, N'ACTIVE', @DisplayOrder, @TenantId, @UserId);
                    """,
                    new
                    {
                        DecisionContractId = contractId,
                        CandidateCode = $"C{order}",
                        OutcomeText = node.NodeText,
                        DisplayOrder = order * 10,
                        TenantId = tenantId,
                        UserId = userId
                    }, transaction: tx, cancellationToken: cancellationToken));
            }

            await WriteAuditAsync(connection, tx, tenantId, userId, contractId, matterId, "CREATED", null, "Draft contract provisioned from matter context.", cancellationToken);
            tx.Commit();
            existing = contractId;
        }

        return (await LoadContractAsync(connection, tenantId, existing.Value, cancellationToken))!;
    }

    public async Task<IReadOnlyList<DecisionContractVersionSummaryDto>> GetVersionHistoryAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<DecisionContractVersionSummaryDto>(new CommandDefinition(
            """
            SELECT c.DecisionContractId, c.VersionNumber, c.StatusCode,
                   c.CreatedByUserId, LTRIM(RTRIM(CONCAT(ISNULL(cu.FirstName, N''), N' ', ISNULL(cu.LastName, N'')))) AS CreatedByDisplayName,
                   c.CreatedDateUtc,
                   c.ApprovedByUserId, LTRIM(RTRIM(CONCAT(ISNULL(au.FirstName, N''), N' ', ISNULL(au.LastName, N'')))) AS ApprovedByDisplayName,
                   c.ApprovedDateUtc
            FROM POLOXI.Legal_DecisionContract c
            LEFT JOIN dbo.AspNetUsers cu ON cu.Id = c.CreatedByUserId
            LEFT JOIN dbo.AspNetUsers au ON au.Id = c.ApprovedByUserId
            WHERE c.TenantId = @TenantId AND c.MatterId = @MatterId AND c.IsDeleted = 0
            ORDER BY c.VersionNumber DESC;
            """,
            new { TenantId = tenantId, MatterId = matterId }, cancellationToken: cancellationToken))).ToArray();
    }

    public async Task<IReadOnlyList<DecisionContractReviewDto>> GetReviewsAsync(Guid tenantId, Guid decisionContractId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<DecisionContractReviewDto>(new CommandDefinition(
            """
            SELECT r.DecisionContractReviewId, r.ReviewActionCode, r.ReviewerUserId,
                   LTRIM(RTRIM(CONCAT(ISNULL(u.FirstName, N''), N' ', ISNULL(u.LastName, N'')))) AS ReviewerDisplayName,
                   r.Comment, r.CreatedDateUtc
            FROM POLOXI.Legal_DecisionContractReview r
            LEFT JOIN dbo.AspNetUsers u ON u.Id = r.ReviewerUserId
            WHERE r.TenantId = @TenantId AND r.DecisionContractId = @Id AND r.IsDeleted = 0
            ORDER BY r.CreatedDateUtc DESC;
            """,
            new { TenantId = tenantId, Id = decisionContractId }, cancellationToken: cancellationToken))).ToArray();
    }

    public async Task<IReadOnlyList<DecisionContractOptionDto>> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<DecisionContractOptionDto>(new CommandDefinition(
            """
            SELECT FieldCode, Value, ISNULL(DisplayName, Value) AS DisplayName, SortOrder
            FROM POLOXI.Legal_DecisionMatterOption
            WHERE IsDeleted = 0 AND (FieldCode LIKE N'DC[_]%' OR FieldCode = N'JURISDICTION')
            ORDER BY FieldCode, SortOrder, Value;
            """,
            cancellationToken: cancellationToken))).ToArray();
    }

    public async Task<string?> GetMatterTitleAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            """
            SELECT TOP 1 Title FROM POLOXI.Legal_DecisionMatter
            WHERE TenantId = @TenantId AND DecisionMatterId = @MatterId AND IsDeleted = 0;
            """,
            new { TenantId = tenantId, MatterId = matterId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PoloxiWeightDto>> GetPoloxiWeightsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<(string SettingKey, string SettingValue, string? Description)>(new CommandDefinition(
            """
            SELECT SettingKey, SettingValue, Description
            FROM POLOXI.Legal_DecisionSetting
            WHERE IsDeleted = 0 AND SettingKey LIKE N'%Weight%'
            ORDER BY SettingKey;
            """,
            cancellationToken: cancellationToken))).ToArray();

        var result = new List<PoloxiWeightDto>();
        foreach (var (key, value, description) in rows)
        {
            if (!decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                continue;
            result.Add(new PoloxiWeightDto(key, HumanizeWeightKey(key), parsed, description));
        }
        return result;
    }

    private static string HumanizeWeightKey(string key)
    {
        var leaf = key.Contains('.') ? key[(key.LastIndexOf('.') + 1)..] : key;
        if (leaf.EndsWith("Weight", StringComparison.OrdinalIgnoreCase))
            leaf = leaf[..^"Weight".Length];
        var spaced = System.Text.RegularExpressions.Regex.Replace(leaf, "(?<=[a-z0-9])(?=[A-Z])", " ");
        return spaced.Length == 0 ? key : spaced.Trim();
    }


    public Task UpdateDecisionAsync(Guid tenantId, Guid userId, DecisionContractDecisionCommand command, CancellationToken cancellationToken = default)
        => UpdateSectionAsync(tenantId, userId, command.DecisionContractId, command.RowVersion, "Decision",
            """
            DecisionQuestion = @DecisionQuestion, ClientObjective = @ClientObjective,
            SuccessDefinition = @SuccessDefinition, DecisionDate = @DecisionDate
            """,
            new { command.DecisionQuestion, command.ClientObjective, command.SuccessDefinition, command.DecisionDate },
            cancellationToken);

    public Task UpdateBurdenAsync(Guid tenantId, Guid userId, DecisionContractBurdenCommand command, CancellationToken cancellationToken = default)
        => UpdateSectionAsync(tenantId, userId, command.DecisionContractId, command.RowVersion, "Burden & Standard",
            """
            MovingParty = @MovingParty, InitialBurden = @InitialBurden, UltimateBurden = @UltimateBurden,
            StandardOfProofOrReview = @StandardOfProofOrReview, BurdenNotes = @BurdenNotes
            """,
            new { command.MovingParty, command.InitialBurden, command.UltimateBurden, command.StandardOfProofOrReview, command.BurdenNotes },
            cancellationToken);

    public Task UpdateSettingsAsync(Guid tenantId, Guid userId, DecisionContractSettingsCommand command, CancellationToken cancellationToken = default)
        => UpdateSectionAsync(tenantId, userId, command.DecisionContractId, command.RowVersion, "Additional Settings",
            """
            DecisionHorizon = @DecisionHorizon, ExternalResearchPermitted = @ExternalResearchPermitted,
            ReviewBeforeActivation = @ReviewBeforeActivation, ReviewBeforeFinal = @ReviewBeforeFinal,
            SemanticValidationMode = @SemanticValidationMode, Notes = @Notes
            """,
            new { command.DecisionHorizon, command.ExternalResearchPermitted, command.ReviewBeforeActivation, command.ReviewBeforeFinal, command.SemanticValidationMode, command.Notes },
            cancellationToken);

    public async Task UpdateLegalContextAsync(Guid tenantId, Guid userId, DecisionContractLegalContextCommand command, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();
        await EnsureEditableAsync(connection, tx, tenantId, command.DecisionContractId, cancellationToken);
        await ApplyUpdateAsync(connection, tx, tenantId, userId, command.DecisionContractId, command.RowVersion,
            """
            Jurisdiction = @Jurisdiction, CourtOrForum = @CourtOrForum, GoverningLaw = @GoverningLaw,
            ProceduralPosture = @ProceduralPosture, CaseType = @CaseType, ApplicableLegalFramework = @ApplicableLegalFramework
            """,
            new { command.Jurisdiction, command.CourtOrForum, command.GoverningLaw, command.ProceduralPosture, command.CaseType, command.ApplicableLegalFramework },
            cancellationToken);

        await ReplaceTagsAsync(connection, tx, tenantId, userId, command.DecisionContractId, DecisionContractTagKinds.StatuteRule, command.StatutesOrRules, cancellationToken);
        await ReplaceTagsAsync(connection, tx, tenantId, userId, command.DecisionContractId, DecisionContractTagKinds.KeyIssue, command.KeyIssues, cancellationToken);
        await WriteAuditAsync(connection, tx, tenantId, userId, command.DecisionContractId, null, "EDITED", "Legal Context", null, cancellationToken);
        tx.Commit();
    }

    public async Task UpdateBoundariesAsync(Guid tenantId, Guid userId, DecisionContractBoundariesCommand command, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();
        await EnsureEditableAsync(connection, tx, tenantId, command.DecisionContractId, cancellationToken);
        await ApplyUpdateAsync(connection, tx, tenantId, userId, command.DecisionContractId, command.RowVersion,
            """
            EvidenceBoundary = @EvidenceBoundary, AuthorityBoundary = @AuthorityBoundary,
            SourceRestrictions = @SourceRestrictions, AuthorityCutoffDate = @AuthorityCutoffDate
            """,
            new { command.EvidenceBoundary, command.AuthorityBoundary, command.SourceRestrictions, command.AuthorityCutoffDate },
            cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE POLOXI.Legal_DecisionContractFactBoundary SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId WHERE DecisionContractId = @Id AND IsDeleted = 0;",
            new { Id = command.DecisionContractId, UserId = userId }, transaction: tx, cancellationToken: cancellationToken));

        var order = 0;
        foreach (var fact in command.Facts ?? [])
        {
            if (string.IsNullOrWhiteSpace(fact.SnapshotText)) continue;
            order++;
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionContractFactBoundary
                    (DecisionContractFactBoundaryId, DecisionContractId, FactId, FactStateCode, SnapshotText, DisplayOrder, TenantId, CreatedByUserId)
                VALUES (NEWID(), @Id, @FactId, @FactStateCode, @SnapshotText, @DisplayOrder, @TenantId, @UserId);
                """,
                new { Id = command.DecisionContractId, fact.FactId, fact.FactStateCode, fact.SnapshotText, DisplayOrder = order * 10, TenantId = tenantId, UserId = userId },
                transaction: tx, cancellationToken: cancellationToken));
        }

        await WriteAuditAsync(connection, tx, tenantId, userId, command.DecisionContractId, null, "EDITED", "Decision Boundaries", null, cancellationToken);
        tx.Commit();
    }

    public async Task UpdateCandidatesAsync(Guid tenantId, Guid userId, DecisionContractCandidatesCommand command, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();
        await EnsureEditableAsync(connection, tx, tenantId, command.DecisionContractId, cancellationToken);
        await ApplyUpdateAsync(connection, tx, tenantId, userId, command.DecisionContractId, command.RowVersion, "ModifiedDateUtc = SYSUTCDATETIME()", new { }, cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE POLOXI.Legal_DecisionContractCandidate SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId WHERE DecisionContractId = @Id AND IsDeleted = 0;",
            new { Id = command.DecisionContractId, UserId = userId }, transaction: tx, cancellationToken: cancellationToken));

        var order = 0;
        foreach (var candidate in command.Candidates ?? [])
        {
            if (string.IsNullOrWhiteSpace(candidate.OutcomeText)) continue;
            order++;
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionContractCandidate
                    (DecisionContractCandidateId, DecisionContractId, CandidateCode, OutcomeText, CandidateTypeCode, StateCode, DisplayOrder, TenantId, CreatedByUserId)
                VALUES (NEWID(), @Id, @CandidateCode, @OutcomeText, @CandidateTypeCode, @StateCode, @DisplayOrder, @TenantId, @UserId);
                """,
                new
                {
                    Id = command.DecisionContractId,
                    CandidateCode = string.IsNullOrWhiteSpace(candidate.CandidateCode) ? $"C{order}" : candidate.CandidateCode,
                    candidate.OutcomeText,
                    candidate.CandidateTypeCode,
                    StateCode = string.IsNullOrWhiteSpace(candidate.StateCode) ? "ACTIVE" : candidate.StateCode,
                    DisplayOrder = order * 10,
                    TenantId = tenantId,
                    UserId = userId
                }, transaction: tx, cancellationToken: cancellationToken));
        }

        await WriteAuditAsync(connection, tx, tenantId, userId, command.DecisionContractId, null, "EDITED", "Competing Outcomes", null, cancellationToken);
        tx.Commit();
    }

    private async Task UpdateSectionAsync(Guid tenantId, Guid userId, Guid contractId, byte[] rowVersion, string section, string setClause, object parameters, CancellationToken cancellationToken)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();
        await EnsureEditableAsync(connection, tx, tenantId, contractId, cancellationToken);
        await ApplyUpdateAsync(connection, tx, tenantId, userId, contractId, rowVersion, setClause, parameters, cancellationToken);
        await WriteAuditAsync(connection, tx, tenantId, userId, contractId, null, "EDITED", section, null, cancellationToken);
        tx.Commit();
    }

    private static async Task ApplyUpdateAsync(IDbConnection connection, IDbTransaction tx, Guid tenantId, Guid userId, Guid contractId, byte[] rowVersion, string setClause, object parameters, CancellationToken cancellationToken)
    {
        var dp = new DynamicParameters(parameters);
        dp.Add("Id", contractId);
        dp.Add("TenantId", tenantId);
        dp.Add("UserId", userId);
        dp.Add("RowVersion", rowVersion, DbType.Binary);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            $"""
            UPDATE POLOXI.Legal_DecisionContract
            SET {setClause}, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
            WHERE DecisionContractId = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND RowVersion = @RowVersion;
            """,
            dp, transaction: tx, cancellationToken: cancellationToken));

        if (affected == 0)
            throw new DecisionContractConcurrencyException("This Decision Contract changed after you opened it. Reload and try again.");
    }

    private static async Task EnsureEditableAsync(IDbConnection connection, IDbTransaction tx, Guid tenantId, Guid contractId, CancellationToken cancellationToken)
    {
        var status = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT StatusCode FROM POLOXI.Legal_DecisionContract WHERE DecisionContractId = @Id AND TenantId = @TenantId AND IsDeleted = 0;",
            new { Id = contractId, TenantId = tenantId }, transaction: tx, cancellationToken: cancellationToken));

        if (status is null)
            throw new DecisionContractStateException("Decision Contract not found.");
        if (status != DecisionContractStatuses.Draft)
            throw new DecisionContractStateException($"Only DRAFT contracts are editable. Create a new version to modify a {status} contract.");
    }

    private static async Task ReplaceTagsAsync(IDbConnection connection, IDbTransaction tx, Guid tenantId, Guid userId, Guid contractId, string kind, IReadOnlyList<string>? values, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE POLOXI.Legal_DecisionContractTag SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId WHERE DecisionContractId = @Id AND TagKindCode = @Kind AND IsDeleted = 0;",
            new { Id = contractId, Kind = kind, UserId = userId }, transaction: tx, cancellationToken: cancellationToken));

        var order = 0;
        foreach (var value in values ?? [])
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            order++;
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionContractTag
                    (DecisionContractTagId, DecisionContractId, TagKindCode, TagText, DisplayOrder, TenantId, CreatedByUserId)
                VALUES (NEWID(), @Id, @Kind, @TagText, @DisplayOrder, @TenantId, @UserId);
                """,
                new { Id = contractId, Kind = kind, TagText = value.Trim(), DisplayOrder = order * 10, TenantId = tenantId, UserId = userId },
                transaction: tx, cancellationToken: cancellationToken));
        }
    }

    // ── Lifecycle transitions ───────────────────────────────────────────────────────────────────────

    public async Task TransitionStatusAsync(Guid tenantId, Guid userId, Guid decisionContractId, byte[] rowVersion, string fromStatus, string toStatus, string reviewAction, string? comment, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();

        var dp = new DynamicParameters();
        dp.Add("Id", decisionContractId);
        dp.Add("TenantId", tenantId);
        dp.Add("UserId", userId);
        dp.Add("From", fromStatus);
        dp.Add("To", toStatus);
        dp.Add("RowVersion", rowVersion, DbType.Binary);

        var approvalSet = toStatus == DecisionContractStatuses.Approved
            ? ", ApprovedByUserId = @UserId, ApprovedDateUtc = SYSUTCDATETIME()"
            : toStatus == DecisionContractStatuses.ReadyForReview
                ? ", SubmittedByUserId = @UserId, SubmittedDateUtc = SYSUTCDATETIME()"
                : string.Empty;

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            $"""
            UPDATE POLOXI.Legal_DecisionContract
            SET StatusCode = @To, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId{approvalSet}
            WHERE DecisionContractId = @Id AND TenantId = @TenantId AND IsDeleted = 0
              AND StatusCode = @From AND RowVersion = @RowVersion;
            """,
            dp, transaction: tx, cancellationToken: cancellationToken));

        if (affected == 0)
            throw new DecisionContractStateException($"Cannot move the contract from {fromStatus} to {toStatus}; it may have changed. Reload and try again.");

        await WriteReviewAsync(connection, tx, tenantId, userId, decisionContractId, reviewAction, comment, cancellationToken);
        await WriteAuditAsync(connection, tx, tenantId, userId, decisionContractId, null, reviewAction, null, comment, cancellationToken);
        tx.Commit();
    }

    public async Task<DecisionContractDto> CreateNewVersionAsync(Guid tenantId, Guid userId, Guid decisionContractId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();

        var source = await connection.QuerySingleOrDefaultAsync<ContractHeadRow>(new CommandDefinition(
            $"SELECT {ContractColumns} FROM POLOXI.Legal_DecisionContract c WHERE c.DecisionContractId = @Id AND c.TenantId = @TenantId AND c.IsDeleted = 0;",
            new { Id = decisionContractId, TenantId = tenantId }, transaction: tx, cancellationToken: cancellationToken));
        if (source is null)
            throw new DecisionContractStateException("Decision Contract not found.");

        // If a DRAFT version already exists for this matter, do not create another.
        var existingDraft = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT TOP 1 DecisionContractId FROM POLOXI.Legal_DecisionContract WHERE TenantId = @TenantId AND MatterId = @MatterId AND StatusCode = N'DRAFT' AND IsDeleted = 0 ORDER BY VersionNumber DESC;",
            new { TenantId = tenantId, source.MatterId }, transaction: tx, cancellationToken: cancellationToken));
        if (existingDraft is { } draftId)
        {
            tx.Commit();
            return (await GetContractByIdAsync(tenantId, draftId, cancellationToken))!;
        }

        var nextVersion = await connection.QuerySingleAsync<int>(new CommandDefinition(
            "SELECT ISNULL(MAX(VersionNumber), 0) + 1 FROM POLOXI.Legal_DecisionContract WHERE TenantId = @TenantId AND MatterId = @MatterId AND IsDeleted = 0;",
            new { TenantId = tenantId, source.MatterId }, transaction: tx, cancellationToken: cancellationToken));

        var newId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_DecisionContract
                (DecisionContractId, MatterId, VersionNumber, StatusCode, DecisionQuestion, ClientObjective, SuccessDefinition, DecisionDate,
                 Jurisdiction, CourtOrForum, GoverningLaw, ProceduralPosture, CaseType, ApplicableLegalFramework,
                 MovingParty, InitialBurden, UltimateBurden, StandardOfProofOrReview, BurdenNotes,
                 EvidenceBoundary, AuthorityBoundary, SourceRestrictions, AuthorityCutoffDate,
                 DecisionHorizon, ExternalResearchPermitted, ReviewBeforeActivation, ReviewBeforeFinal, SemanticValidationMode, Notes,
                 TenantId, CreatedByUserId)
            VALUES
                (@NewId, @MatterId, @NextVersion, N'DRAFT', @DecisionQuestion, @ClientObjective, @SuccessDefinition, @DecisionDate,
                 @Jurisdiction, @CourtOrForum, @GoverningLaw, @ProceduralPosture, @CaseType, @ApplicableLegalFramework,
                 @MovingParty, @InitialBurden, @UltimateBurden, @StandardOfProofOrReview, @BurdenNotes,
                 @EvidenceBoundary, @AuthorityBoundary, @SourceRestrictions, @AuthorityCutoffDate,
                 @DecisionHorizon, @ExternalResearchPermitted, @ReviewBeforeActivation, @ReviewBeforeFinal, @SemanticValidationMode, @Notes,
                 @TenantId, @UserId);
            """,
            new
            {
                NewId = newId, source.MatterId, NextVersion = nextVersion,
                source.DecisionQuestion, source.ClientObjective, source.SuccessDefinition, source.DecisionDate,
                source.Jurisdiction, source.CourtOrForum, source.GoverningLaw, source.ProceduralPosture, source.CaseType, source.ApplicableLegalFramework,
                source.MovingParty, source.InitialBurden, source.UltimateBurden, source.StandardOfProofOrReview, source.BurdenNotes,
                source.EvidenceBoundary, source.AuthorityBoundary, source.SourceRestrictions, source.AuthorityCutoffDate,
                source.DecisionHorizon, source.ExternalResearchPermitted, source.ReviewBeforeActivation, source.ReviewBeforeFinal, source.SemanticValidationMode, source.Notes,
                TenantId = tenantId, UserId = userId
            }, transaction: tx, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_DecisionContractCandidate
                (DecisionContractCandidateId, DecisionContractId, CandidateCode, OutcomeText, CandidateTypeCode, StateCode, DisplayOrder, TenantId, CreatedByUserId)
            SELECT NEWID(), @NewId, CandidateCode, OutcomeText, CandidateTypeCode, StateCode, DisplayOrder, @TenantId, @UserId
            FROM POLOXI.Legal_DecisionContractCandidate WHERE DecisionContractId = @SourceId AND IsDeleted = 0;
            """,
            new { NewId = newId, SourceId = decisionContractId, TenantId = tenantId, UserId = userId }, transaction: tx, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_DecisionContractFactBoundary
                (DecisionContractFactBoundaryId, DecisionContractId, FactId, FactStateCode, SnapshotText, DisplayOrder, TenantId, CreatedByUserId)
            SELECT NEWID(), @NewId, FactId, FactStateCode, SnapshotText, DisplayOrder, @TenantId, @UserId
            FROM POLOXI.Legal_DecisionContractFactBoundary WHERE DecisionContractId = @SourceId AND IsDeleted = 0;
            """,
            new { NewId = newId, SourceId = decisionContractId, TenantId = tenantId, UserId = userId }, transaction: tx, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_DecisionContractTag
                (DecisionContractTagId, DecisionContractId, TagKindCode, TagText, DisplayOrder, TenantId, CreatedByUserId)
            SELECT NEWID(), @NewId, TagKindCode, TagText, DisplayOrder, @TenantId, @UserId
            FROM POLOXI.Legal_DecisionContractTag WHERE DecisionContractId = @SourceId AND IsDeleted = 0;
            """,
            new { NewId = newId, SourceId = decisionContractId, TenantId = tenantId, UserId = userId }, transaction: tx, cancellationToken: cancellationToken));

        await WriteAuditAsync(connection, tx, tenantId, userId, newId, source.MatterId, "CLONED", null, $"New version v{nextVersion} created from v{source.VersionNumber}.", cancellationToken);
        tx.Commit();

        return (await GetContractByIdAsync(tenantId, newId, cancellationToken))!;
    }

    public async Task ActivateAsync(Guid tenantId, Guid userId, Guid decisionContractId, byte[] rowVersion, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();

        var head = await connection.QuerySingleOrDefaultAsync<(Guid MatterId, string StatusCode)?>(new CommandDefinition(
            "SELECT MatterId, StatusCode FROM POLOXI.Legal_DecisionContract WHERE DecisionContractId = @Id AND TenantId = @TenantId AND IsDeleted = 0;",
            new { Id = decisionContractId, TenantId = tenantId }, transaction: tx, cancellationToken: cancellationToken));
        if (head is null)
            throw new DecisionContractStateException("Decision Contract not found.");
        if (head.Value.StatusCode != DecisionContractStatuses.Approved)
            throw new DecisionContractStateException("Only APPROVED contracts can be activated.");

        // Supersede any current ACTIVE version for the matter.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_DecisionContract
            SET StatusCode = N'SUPERSEDED', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
            WHERE TenantId = @TenantId AND MatterId = @MatterId AND StatusCode = N'ACTIVE' AND IsDeleted = 0;
            """,
            new { TenantId = tenantId, head.Value.MatterId, UserId = userId }, transaction: tx, cancellationToken: cancellationToken));

        var dp = new DynamicParameters();
        dp.Add("Id", decisionContractId);
        dp.Add("TenantId", tenantId);
        dp.Add("UserId", userId);
        dp.Add("RowVersion", rowVersion, DbType.Binary);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_DecisionContract
            SET StatusCode = N'ACTIVE', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
            WHERE DecisionContractId = @Id AND TenantId = @TenantId AND IsDeleted = 0
              AND StatusCode = N'APPROVED' AND RowVersion = @RowVersion;
            """,
            dp, transaction: tx, cancellationToken: cancellationToken));
        if (affected == 0)
            throw new DecisionContractStateException("Cannot activate the contract; it may have changed. Reload and try again.");

        await WriteAuditAsync(connection, tx, tenantId, userId, decisionContractId, head.Value.MatterId, "ACTIVATED", null, "Contract activated as authoritative.", cancellationToken);
        tx.Commit();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────────

    private static Task WriteReviewAsync(IDbConnection connection, IDbTransaction tx, Guid tenantId, Guid userId, Guid contractId, string action, string? comment, CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_DecisionContractReview
                (DecisionContractReviewId, DecisionContractId, ReviewActionCode, ReviewerUserId, Comment, TenantId, CreatedByUserId)
            VALUES (NEWID(), @Id, @Action, @UserId, @Comment, @TenantId, @UserId);
            """,
            new { Id = contractId, Action = action, UserId = userId, Comment = comment, TenantId = tenantId },
            transaction: tx, cancellationToken: cancellationToken));

    private static async Task WriteAuditAsync(IDbConnection connection, IDbTransaction tx, Guid tenantId, Guid userId, Guid contractId, Guid? matterId, string action, string? section, string? detail, CancellationToken cancellationToken)
    {
        var resolvedMatterId = matterId ?? await connection.QuerySingleOrDefaultAsync<Guid>(new CommandDefinition(
            "SELECT MatterId FROM POLOXI.Legal_DecisionContract WHERE DecisionContractId = @Id;",
            new { Id = contractId }, transaction: tx, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_DecisionContractAudit
                (DecisionContractAuditId, DecisionContractId, MatterId, ActionCode, SectionCode, Detail, TenantId, CreatedByUserId)
            VALUES (NEWID(), @Id, @MatterId, @Action, @Section, @Detail, @TenantId, @UserId);
            """,
            new { Id = contractId, MatterId = resolvedMatterId, Action = action, Section = section, Detail = detail, TenantId = tenantId, UserId = userId },
            transaction: tx, cancellationToken: cancellationToken));
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record MatterContextRow(string? Title, string? Jurisdiction, string? Posture, string? MatterTypeCode, string? Description);

    private sealed class ContractHeadRow
    {
        public Guid DecisionContractId { get; init; }
        public Guid MatterId { get; init; }
        public int VersionNumber { get; init; }
        public string StatusCode { get; init; } = "DRAFT";
        public string? DecisionQuestion { get; init; }
        public string? ClientObjective { get; init; }
        public string? SuccessDefinition { get; init; }
        public DateOnly? DecisionDate { get; init; }
        public string? Jurisdiction { get; init; }
        public string? CourtOrForum { get; init; }
        public string? GoverningLaw { get; init; }
        public string? ProceduralPosture { get; init; }
        public string? CaseType { get; init; }
        public string? ApplicableLegalFramework { get; init; }
        public string? MovingParty { get; init; }
        public string? InitialBurden { get; init; }
        public string? UltimateBurden { get; init; }
        public string? StandardOfProofOrReview { get; init; }
        public string? BurdenNotes { get; init; }
        public string? EvidenceBoundary { get; init; }
        public string? AuthorityBoundary { get; init; }
        public string? SourceRestrictions { get; init; }
        public DateOnly? AuthorityCutoffDate { get; init; }
        public string? DecisionHorizon { get; init; }
        public bool ExternalResearchPermitted { get; init; }
        public bool ReviewBeforeActivation { get; init; }
        public bool ReviewBeforeFinal { get; init; }
        public string SemanticValidationMode { get; init; } = "STRICT";
        public string? Notes { get; init; }
        public Guid? CreatedByUserId { get; init; }
        public DateTime CreatedDateUtc { get; init; }
        public Guid? ApprovedByUserId { get; init; }
        public DateTime? ApprovedDateUtc { get; init; }
        public byte[] RowVersion { get; init; } = [];
        public string? CreatedByDisplayName { get; init; }
        public string? ApprovedByDisplayName { get; init; }
    }
}
