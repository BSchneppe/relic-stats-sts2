#if DEBUG
using RelicStats.Core.Testing;

namespace RelicStats.Core;

public interface ITestableRelic
{
    string RelicId { get; }
    void RegisterTest(TestRunner runner);
}
#endif