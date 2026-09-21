using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Decision.Core;

public static class DecisionModelRouteSelector
{
    public const string DefaultFeatureCode = "DECISION_DEFAULT";
    public const string SchemaRepairFeatureCode = "DECISION_SCHEMA_REPAIR";

    public static DecisionModelRouteDto Select(
        IReadOnlyCollection<DecisionModelRouteDto> routes,
        string featureCode,
        string? modelCode = null)
    {
        if (routes.Count == 0)
            throw new InvalidOperationException("No active AI routes are configured in POLOXI.Legal_DecisionModelRoute.");

        if (!string.IsNullOrWhiteSpace(modelCode))
        {
            var requested = routes
                .Where(route => route.ModelCode.Equals(modelCode, StringComparison.OrdinalIgnoreCase))
                .OrderBy(route => FeatureRank(route.FeatureCode, featureCode))
                .ThenBy(route => route.Priority)
                .FirstOrDefault();
            if (requested is not null)
                return requested;
        }

        return routes
            .OrderBy(route => FeatureRank(route.FeatureCode, featureCode))
            .ThenBy(route => route.Priority)
            .First();
    }

    private static int FeatureRank(string configuredFeatureCode, string requestedFeatureCode)
    {
        if (configuredFeatureCode.Equals(requestedFeatureCode, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (configuredFeatureCode.Equals(DefaultFeatureCode, StringComparison.OrdinalIgnoreCase))
            return 1;
        return 2;
    }
}
