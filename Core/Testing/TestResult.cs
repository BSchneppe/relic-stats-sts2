#if DEBUG
namespace RelicStats.Core.Testing;

public record TestResult(bool Passed, string? Message = null);
#endif
