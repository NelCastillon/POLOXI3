-- Corrects the Azure Maps endpoint configuration for existing Address & Location provider rows.
-- The '/geocode:autocomplete' route returns HTTP 404 on this Azure Maps account, and '/geocode'
-- only matches complete addresses. The Search typeahead endpoint (api-version 1.0, verified
-- working with this account) returns partial-match suggestions, which is what autocomplete needs.
UPDATE Location.AddressProviderConfiguration
SET AutocompletePath = N'/search/address/json?api-version=1.0&typeahead=true',
	GeocodePath = N'/geocode',
	ApiVersion = N'2025-01-01',
	ModifiedDateUtc = SYSUTCDATETIME()
WHERE ProviderCode = N'AzureMaps'
  AND (AutocompletePath <> N'/search/address/json?api-version=1.0&typeahead=true' OR GeocodePath <> N'/geocode' OR ApiVersion <> N'2025-01-01')
  AND IsDeleted = 0;
