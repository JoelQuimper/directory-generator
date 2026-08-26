# Directory Profiles

Each JSON file in this folder defines one directory population and its Graph projection. Profiles are loaded and validated when the API starts, so restart the API after editing a file.

The current fields are:

- `id`: URL-safe lowercase identifier used by the API.
- `displayNames`: localized names keyed by `en-CA` or `fr-CA`.
- `filter`: trusted OData filter expression without `$filter=` or other query parameters.
- `properties`: Microsoft Graph user properties to retrieve.
- `sort.groupBy`: optional grouping property and direction.
- `sort.entries`: ordered entry sorting rules.

API callers select a profile by `id`; they cannot supply or override its filter or properties.