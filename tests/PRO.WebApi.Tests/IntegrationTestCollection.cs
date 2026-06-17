using Xunit;

namespace PRO.WebApi.Tests;

/// <summary>
/// 集成测试集合 — 所有使用 TestWebApplicationFactory 的测试类加入此集合，
/// 确保仅构建单个 Host 实例，避免并发导致的 IServiceProvider disposed 问题。
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<TestWebApplicationFactory>
{
}
