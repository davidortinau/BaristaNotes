using Microsoft.Extensions.Logging;
using Moq;

namespace BaristaNotes.Tests.Helpers;

/// <summary>
/// Helper factory for creating mock ILogger instances in unit tests.
/// </summary>
public static class MockLoggerFactory
{
    /// <summary>
    /// Creates a mock ILogger&lt;T&gt; instance for use in unit tests.
    /// </summary>
    /// <typeparam name="T">The type for the logger category.</typeparam>
    /// <returns>A mock ILogger instance.</returns>
    public static ILogger<T> Create<T>()
    {
        return new Mock<ILogger<T>>().Object;
    }

    /// <summary>
    /// Creates a Mock&lt;ILogger&lt;T&gt;&gt; instance for advanced verification in tests.
    /// </summary>
    /// <typeparam name="T">The type for the logger category.</typeparam>
    /// <returns>A Mock instance that can be used for verifications.</returns>
    public static Mock<ILogger<T>> CreateMock<T>()
    {
        return new Mock<ILogger<T>>();
    }
}
