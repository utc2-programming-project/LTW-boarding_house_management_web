using Web.Features.Properties;
namespace Web.Features.Search.Abstractions;
public sealed record SearchContext(PropertyCaller Caller, string Query, string NormalizedQuery, int Limit, int Offset);
